using CongDoan.Application.Shared;
using CongDoan.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CongDoan.Application.Identity;

/// <summary>Where a permission applies for one admin: everywhere, or in a set of org units (descendants included).</summary>
public sealed record PermissionScope(bool Global, IReadOnlySet<Guid> OrgUnits)
{
    public static readonly PermissionScope None = new(false, new HashSet<Guid>());
    public bool Any => Global || OrgUnits.Count > 0;
    public bool Covers(Guid? orgUnitId) => Global || (orgUnitId is { } id && OrgUnits.Contains(id));
}

public interface IPermissionChecker
{
    Task<PermissionScope> ScopeAsync(Guid adminId, string permission, CancellationToken ct = default);
    Task<bool> HasAsync(Guid adminId, string permission, Guid? orgUnitId = null, CancellationToken ct = default);
    /// <summary>Every permission code the admin holds in any scope (for showing/hiding UI; the API still checks each call).</summary>
    Task<IReadOnlyList<string>> CodesAsync(Guid adminId, CancellationToken ct = default);
    void Invalidate();
}

public sealed class PermissionService : IPermissionChecker
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);
    private static int _generation;
    private readonly IIdentityDbContext _db;
    private readonly IMemoryCache _cache;

    public PermissionService(IIdentityDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>Called after any role/permission change so the next request re-reads the database.</summary>
    public void Invalidate() => Interlocked.Increment(ref _generation);

    public async Task<bool> HasAsync(Guid adminId, string permission, Guid? orgUnitId = null, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(adminId, permission, ct);
        return orgUnitId is null ? scope.Any : scope.Covers(orgUnitId);
    }

    public async Task<IReadOnlyList<string>> CodesAsync(Guid adminId, CancellationToken ct = default) =>
        (await GrantsAsync(adminId, ct)).Where(g => g.Value.Any).Select(g => g.Key).OrderBy(c => c).ToList();

    public async Task<PermissionScope> ScopeAsync(Guid adminId, string permission, CancellationToken ct = default)
    {
        var grants = await GrantsAsync(adminId, ct);
        return grants.TryGetValue(permission, out var scope) ? scope : PermissionScope.None;
    }

    private async Task<Dictionary<string, PermissionScope>> GrantsAsync(Guid adminId, CancellationToken ct)
    {
        var key = $"perm:{Volatile.Read(ref _generation)}:{adminId}";
        if (_cache.TryGetValue(key, out Dictionary<string, PermissionScope>? cached)) return cached!;

        var rows = await (from ur in _db.AdminUserRoles
                          join r in _db.Roles on ur.RoleId equals r.Id
                          from p in r.Permissions
                          where ur.AdminUserId == adminId
                          select new { p.Permission, ur.OrgUnitId }).ToListAsync(ct);
        var units = rows.Any(r => r.OrgUnitId != null)
            ? await _db.OrgUnits.AsNoTracking().Where(o => !o.IsDeleted).Select(o => new { o.Id, o.ParentId }).ToListAsync(ct)
            : new();
        var children = units.ToLookup(u => u.ParentId, u => u.Id);

        var result = new Dictionary<string, PermissionScope>();
        foreach (var g in rows.GroupBy(r => r.Permission))
        {
            var global = g.Any(x => x.OrgUnitId == null);
            var set = new HashSet<Guid>();
            if (!global)
                foreach (var root in g.Select(x => x.OrgUnitId!.Value)) Descend(root, children, set);
            result[g.Key] = new PermissionScope(global, set);
        }
        _cache.Set(key, result, CacheFor);
        return result;
    }

    private static void Descend(Guid id, ILookup<Guid?, Guid> children, HashSet<Guid> into)
    {
        if (!into.Add(id)) return;
        foreach (var c in children[id]) Descend(c, children, into);
    }
}

public static class PermissionCheckerExtensions
{
    public static async Task EnsureAsync(this IPermissionChecker checker, ICurrentUser user, string permission, Guid? orgUnitId, CancellationToken ct = default)
    {
        if (user.AdminId is not { } id || !await checker.HasAsync(id, permission, orgUnitId, ct))
            throw AppException.Forbidden();
    }

    public static async Task<PermissionScope> RequireScopeAsync(this IPermissionChecker checker, ICurrentUser user, string permission, CancellationToken ct = default)
    {
        if (user.AdminId is not { } id) throw AppException.Forbidden();
        var scope = await checker.ScopeAsync(id, permission, ct);
        if (!scope.Any) throw AppException.Forbidden();
        return scope;
    }
}
