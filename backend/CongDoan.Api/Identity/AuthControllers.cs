using System.Security.Claims;
using CongDoan.Api.Shared;
using CongDoan.Application.Identity;
using CongDoan.Domain.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CongDoan.Api.Identity;

public sealed record LoginRequest(string UserName, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record MeResponse(Guid Id, string UserName, string DisplayName, bool MustChangePassword);

[Route("api/antiforgery")]
public sealed class AntiforgeryController : ApiControllerBase
{
    [HttpGet]
    public IActionResult Get([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Headers.CacheControl = "no-store";
        return Envelope(new { token = tokens.RequestToken });
    }
}

public abstract class AuthControllerBase : ApiControllerBase
{
    private readonly AuthService _auth;

    protected AuthControllerBase(AuthService auth) => _auth = auth;

    protected abstract AccountKind Kind { get; }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSchemes.LoginRateLimit)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(Kind, req.UserName, req.Password, ct);
        await HttpContext.SignInAsync(AuthSchemes.For(Kind), AuthSchemes.Principal(Kind, result.Account, result.SessionId),
            new AuthenticationProperties { IsPersistent = false });
        return Envelope(ToMe(result.Account));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var auth = await HttpContext.AuthenticateAsync(AuthSchemes.For(Kind));
        if (auth.Succeeded && Guid.TryParse(auth.Principal!.FindFirstValue(AuthSchemes.SessionClaim), out var sid))
            await _auth.LogoutAsync(sid, ct);
        await HttpContext.SignOutAsync(AuthSchemes.For(Kind));
        return Envelope(new { loggedOut = true });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var (_, sessionId) = Current();
        var info = await _auth.ValidateSessionAsync(Kind, sessionId, ct);
        return info is null ? Unauthorized() : Envelope(ToMe(info));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var (accountId, sessionId) = Current();
        await _auth.ChangePasswordAsync(Kind, accountId, sessionId, req.CurrentPassword, req.NewPassword, ct);
        return Envelope(new { changed = true });
    }

    private (Guid AccountId, Guid SessionId) Current()
    {
        var id = User.IdentityOf(Kind) ?? throw new UnauthorizedAccessException();
        return (Guid.Parse(id.FindFirst(ClaimTypes.NameIdentifier)!.Value), Guid.Parse(id.FindFirst(AuthSchemes.SessionClaim)!.Value));
    }

    private static MeResponse ToMe(AccountInfo a) => new(a.Id, a.UserName, a.DisplayName, a.MustChangePassword);
}

[Route("api/admin/auth")]
[Authorize(Policy = AuthSchemes.AdminPolicy)]
public sealed class AdminAuthController : AuthControllerBase
{
    public AdminAuthController(AuthService auth) : base(auth) { }
    protected override AccountKind Kind => AccountKind.Admin;

    [HttpGet("permissions")]
    public async Task<IActionResult> Permissions([FromServices] IPermissionChecker checker, CancellationToken ct)
    {
        var id = Guid.Parse(User.IdentityOf(AccountKind.Admin)!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        return Envelope(await checker.CodesAsync(id, ct));
    }
}

[Route("api/thi/auth")]
[Authorize(Policy = AuthSchemes.CandidatePolicy)]
public sealed class CandidateAuthController : AuthControllerBase
{
    public CandidateAuthController(AuthService auth) : base(auth) { }
    protected override AccountKind Kind => AccountKind.Candidate;
}
