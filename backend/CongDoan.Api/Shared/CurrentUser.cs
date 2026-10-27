using System.Security.Claims;
using CongDoan.Api.Identity;
using CongDoan.Application.Shared;
using CongDoan.Domain.Identity;

namespace CongDoan.Api.Shared;

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Ctx => _accessor.HttpContext;

    public Guid? AdminId => IdOf(AccountKind.Admin);
    public Guid? CandidateId => IdOf(AccountKind.Candidate);
    public string? UserName => (Ctx?.User.IdentityOf(AccountKind.Admin) ?? Ctx?.User.IdentityOf(AccountKind.Candidate))?.Name;
    public string? RequestId => Ctx?.TraceIdentifier;
    public string? IpAddress => Ctx?.Connection.RemoteIpAddress?.ToString();

    private Guid? IdOf(AccountKind kind) =>
        Guid.TryParse(Ctx?.User.IdentityOf(kind)?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
