using System.Security.Claims;
using System.Threading.RateLimiting;
using CongDoan.Api.Shared;
using CongDoan.Application.Identity;
using CongDoan.Domain.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CongDoan.Api.Identity;

public static class AuthSchemes
{
    public const string Admin = "Admin";
    public const string Candidate = "Candidate";
    public const string AdminPolicy = "AdminOnly";
    public const string CandidatePolicy = "CandidateOnly";
    public const string KindClaim = "cd:kind";
    public const string SessionClaim = "cd:sid";
    public const string MustChangeClaim = "cd:mcp";
    public const string LoginRateLimit = "login";

    public static string For(AccountKind kind) => kind == AccountKind.Admin ? Admin : Candidate;

    public static ClaimsPrincipal Principal(AccountKind kind, AccountInfo a, Guid sessionId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, a.Id.ToString()),
            new(ClaimTypes.Name, a.UserName),
            new(KindClaim, kind == AccountKind.Admin ? "admin" : "candidate"),
            new(SessionClaim, sessionId.ToString()),
        };
        if (a.MustChangePassword) claims.Add(new Claim(MustChangeClaim, "1"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, For(kind)));
    }

    /// <summary>Identity of one scheme even when both admin and candidate cookies are present.</summary>
    public static ClaimsIdentity? IdentityOf(this ClaimsPrincipal user, AccountKind kind) =>
        user.Identities.FirstOrDefault(i => i.IsAuthenticated && i.AuthenticationType == For(kind));
}

public static class AuthSetup
{
    public static IServiceCollection AddPortalAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication()
            .AddCookie(AuthSchemes.Admin, o => Configure(o, AccountKind.Admin, "cd_admin", AuthService.AdminSessionLifetime))
            .AddCookie(AuthSchemes.Candidate, o => Configure(o, AccountKind.Candidate, "cd_thi", AuthService.CandidateSessionLifetime));

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthSchemes.AdminPolicy, p => p
                .AddAuthenticationSchemes(AuthSchemes.Admin, AuthSchemes.Candidate)
                .RequireAuthenticatedUser()
                .RequireClaim(AuthSchemes.KindClaim, "admin"))
            .AddPolicy(AuthSchemes.CandidatePolicy, p => p
                .AddAuthenticationSchemes(AuthSchemes.Candidate, AuthSchemes.Admin)
                .RequireAuthenticatedUser()
                .RequireClaim(AuthSchemes.KindClaim, "candidate"));

        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionHandler>();
        services.AddHttpContextAccessor();
        services.AddScoped<CongDoan.Application.Shared.ICurrentUser, CongDoan.Api.Shared.HttpCurrentUser>();

        services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-XSRF-TOKEN";
            o.Cookie.Name = "cd_xsrf";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        var loginPermit = config.GetValue("RateLimit:LoginPermitPerMinute", 10);
        services.AddRateLimiter(o =>
        {
            o.AddPolicy(AuthSchemes.LoginRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPermit, Window = TimeSpan.FromMinutes(1) }));
            o.OnRejected = async (ctx, _) => await ErrorWriter.WriteAsync(ctx.HttpContext, StatusCodes.Status429TooManyRequests,
                "rate_limited", "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút.");
        });
        return services;
    }

    private static void Configure(CookieAuthenticationOptions o, AccountKind kind, string cookieName, TimeSpan lifetime)
    {
        o.Cookie.Name = cookieName;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = lifetime;
        o.SlidingExpiration = false;
        o.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx => ErrorWriter.WriteAsync(ctx.HttpContext, 401, "unauthenticated", "Vui lòng đăng nhập."),
            OnRedirectToAccessDenied = ctx => ErrorWriter.WriteAsync(ctx.HttpContext, 403, "forbidden", "Bạn không có quyền truy cập."),
            OnValidatePrincipal = async ctx =>
            {
                var sid = ctx.Principal?.FindFirstValue(AuthSchemes.SessionClaim);
                var auth = ctx.HttpContext.RequestServices.GetRequiredService<AuthService>();
                var info = Guid.TryParse(sid, out var sessionId) ? await auth.ValidateSessionAsync(kind, sessionId) : null;
                if (info is null)
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(ctx.Scheme.Name);
                    return;
                }
                // Always reflect current database state (e.g. password changed, flags cleared).
                ctx.ReplacePrincipal(AuthSchemes.Principal(kind, info, sessionId));
            },
        };
    }

    /// <summary>Rejects unsafe /api requests without a valid anti-forgery header.</summary>
    public static IApplicationBuilder UseAntiforgeryForApi(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var method = ctx.Request.Method;
        var unsafeMethod = !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));
        if (unsafeMethod && ctx.Request.Path.StartsWithSegments("/api"))
        {
            var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
            if (!await antiforgery.IsRequestValidAsync(ctx))
            {
                await ErrorWriter.WriteAsync(ctx, 400, "csrf_invalid", "Phiên làm việc không hợp lệ. Vui lòng tải lại trang.");
                return;
            }
        }
        await next();
    });

    /// <summary>Accounts flagged for a first-login password change may only use the auth endpoints.</summary>
    public static IApplicationBuilder UsePasswordChangeGate(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var path = ctx.Request.Path;
        string? scheme = path.StartsWithSegments("/api/admin") ? AuthSchemes.Admin
            : path.StartsWithSegments("/api/thi") ? AuthSchemes.Candidate : null;
        if (scheme is not null && !path.Value!.Contains("/auth/", StringComparison.OrdinalIgnoreCase))
        {
            var result = await ctx.AuthenticateAsync(scheme);
            if (result.Succeeded && result.Principal!.HasClaim(AuthSchemes.MustChangeClaim, "1"))
            {
                await ErrorWriter.WriteAsync(ctx, 403, "password_change_required", "Bạn cần đổi mật khẩu trước khi tiếp tục.");
                return;
            }
        }
        await next();
    });
}
