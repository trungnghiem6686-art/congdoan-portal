using System.Security.Claims;
using CongDoan.Application.Identity;
using CongDoan.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CongDoan.Api.Identity;

/// <summary>Admin-only endpoint that also needs one permission (in any scope; resource scope is checked in services).</summary>
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string Prefix = "perm:";

    public RequirePermissionAttribute(string permission) : base(Prefix + permission) => Permission = permission;

    public string Permission { get; }
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;
    public string Permission { get; }
}

public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(RequirePermissionAttribute.Prefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);
        return new AuthorizationPolicyBuilder(AuthSchemes.Admin, AuthSchemes.Candidate)
            .RequireAuthenticatedUser()
            .RequireClaim(AuthSchemes.KindClaim, "admin")
            .AddRequirements(new PermissionRequirement(policyName[RequirePermissionAttribute.Prefix.Length..]))
            .Build();
    }
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionChecker _checker;

    public PermissionHandler(IPermissionChecker checker) => _checker = checker;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var id = context.User.IdentityOf(AccountKind.Admin)?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(id, out var adminId) && await _checker.HasAsync(adminId, requirement.Permission))
            context.Succeed(requirement);
    }
}
