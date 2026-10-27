using CongDoan.Application.Shared;
using CongDoan.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Identity;

public sealed record OrgUnitDto(Guid Id, string Code, string Name, Guid? ParentId, int SortOrder, bool IsDeleted);
public sealed record SaveOrgUnit(string Code, string Name, Guid? ParentId, int SortOrder = 0);
public sealed record GroupDto(Guid Id, string Name, string? Description, Guid? OrgUnitId, int MemberCount, bool IsDeleted);
public sealed record SaveGroup(string Name, string? Description, Guid? OrgUnitId);
public sealed record RoleDto(Guid Id, string Code, string Name, string? Description, bool IsSystem, IReadOnlyList<string> Permissions);
public sealed record SaveRole(string Code, string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed class OrgUnitService
{
    private readonly IIdentityDbContext _db;
    private readonly IPermissionChecker _permissions;

    public OrgUnitService(IIdentityDbContext db, IPermissionChecker permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<OrgUnitDto>> ListAsync(bool deleted = false, CancellationToken ct = default) =>
        (await _db.OrgUnits.AsNoTracking().Where(o => o.IsDeleted == deleted).OrderBy(o => o.SortOrder).ThenBy(o => o.Name).ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<OrgUnitDto> CreateAsync(SaveOrgUnit cmd, CancellationToken ct = default)
    {
        await ValidateAsync(null, cmd, ct);
        var o = new OrgUnit(cmd.Code, cmd.Name, cmd.ParentId) { SortOrder = cmd.SortOrder };
        _db.OrgUnits.Add(o);
        await _db.SaveChangesAsync(ct);
        _permissions.Invalidate();
        return ToDto(o);
    }

    public async Task<OrgUnitDto> UpdateAsync(Guid id, SaveOrgUnit cmd, CancellationToken ct = default)
    {
        var o = await _db.OrgUnits.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        await ValidateAsync(id, cmd, ct);
        o.Code = cmd.Code.Trim();
        o.Name = cmd.Name.Trim();
        o.ParentId = cmd.ParentId;
        o.SortOrder = cmd.SortOrder;
        await _db.SaveChangesAsync(ct);
        _permissions.Invalidate();
        return ToDto(o);
    }

    public async Task SetDeletedAsync(Guid id, bool deleted, CancellationToken ct = default)
    {
        var o = await _db.OrgUnits.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        if (deleted && await _db.Candidates.AnyAsync(c => c.OrgUnitId == id && !c.IsDeleted, ct))
            throw AppException.Conflict("in_use", "Đơn vị còn thí sinh, không thể xóa.");
        o.IsDeleted = deleted;
        await _db.SaveChangesAsync(ct);
        _permissions.Invalidate();
    }

    private async Task ValidateAsync(Guid? id, SaveOrgUnit cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Code) || string.IsNullOrWhiteSpace(cmd.Name))
            throw AppException.Validation("Mã và tên đơn vị là bắt buộc.");
        var code = cmd.Code.Trim();
        if (await _db.OrgUnits.AnyAsync(o => o.Code == code && o.Id != id, ct))
            throw AppException.Conflict("duplicate_code", "Mã đơn vị đã tồn tại.");
        if (cmd.ParentId is null) return;
        var parents = await _db.OrgUnits.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.ParentId, ct);
        if (!parents.ContainsKey(cmd.ParentId.Value)) throw AppException.Validation("Đơn vị cha không tồn tại.");
        for (Guid? p = cmd.ParentId; p is not null; p = parents.GetValueOrDefault(p.Value))
            if (p == id) throw AppException.Validation("Không thể chọn đơn vị con làm đơn vị cha.");
    }

    private static OrgUnitDto ToDto(OrgUnit o) => new(o.Id, o.Code, o.Name, o.ParentId, o.SortOrder, o.IsDeleted);
}

public sealed class CandidateGroupService
{
    private readonly IIdentityDbContext _db;

    public CandidateGroupService(IIdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<GroupDto>> ListAsync(bool deleted = false, CancellationToken ct = default) =>
        await _db.CandidateGroups.AsNoTracking().Where(g => g.IsDeleted == deleted).OrderBy(g => g.Name)
            .Select(g => new GroupDto(g.Id, g.Name, g.Description, g.OrgUnitId,
                _db.CandidateGroupMembers.Count(m => m.GroupId == g.Id), g.IsDeleted))
            .ToListAsync(ct);

    public async Task<GroupDto> CreateAsync(SaveGroup cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw AppException.Validation("Tên nhóm là bắt buộc.");
        var g = new CandidateGroup(cmd.Name) { Description = cmd.Description, OrgUnitId = cmd.OrgUnitId };
        _db.CandidateGroups.Add(g);
        await _db.SaveChangesAsync(ct);
        return new GroupDto(g.Id, g.Name, g.Description, g.OrgUnitId, 0, false);
    }

    public async Task UpdateAsync(Guid id, SaveGroup cmd, CancellationToken ct = default)
    {
        var g = await _db.CandidateGroups.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw AppException.Validation("Tên nhóm là bắt buộc.");
        g.Name = cmd.Name.Trim();
        g.Description = cmd.Description;
        g.OrgUnitId = cmd.OrgUnitId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetDeletedAsync(Guid id, bool deleted, CancellationToken ct = default)
    {
        var g = await _db.CandidateGroups.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        g.IsDeleted = deleted;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetMembersAsync(Guid id, IReadOnlyList<Guid> add, IReadOnlyList<Guid> remove, CancellationToken ct = default)
    {
        if (!await _db.CandidateGroups.AnyAsync(g => g.Id == id && !g.IsDeleted, ct)) throw AppException.NotFound();
        var current = await _db.CandidateGroupMembers.Where(m => m.GroupId == id).ToListAsync(ct);
        _db.CandidateGroupMembers.RemoveRange(current.Where(m => remove.Contains(m.CandidateId)));
        var known = await _db.Candidates.Where(c => add.Contains(c.Id)).Select(c => c.Id).ToListAsync(ct);
        foreach (var cid in known.Where(c => current.All(m => m.CandidateId != c)))
            _db.CandidateGroupMembers.Add(new CandidateGroupMember(id, cid));
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class RoleService
{
    private readonly IIdentityDbContext _db;
    private readonly IPermissionChecker _permissions;
    private readonly PermissionCatalog _catalog;

    private readonly IAuditWriter _audit;

    public RoleService(IIdentityDbContext db, IPermissionChecker permissions, PermissionCatalog catalog, IAuditWriter audit)
    {
        _audit = audit;
        _db = db;
        _permissions = permissions;
        _catalog = catalog;
    }

    public IReadOnlyList<PermissionDefinition> Catalog => _catalog.All;

    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct = default) =>
        (await _db.Roles.AsNoTracking().Include(r => r.Permissions).OrderBy(r => r.Name).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<RoleDto> CreateAsync(SaveRole cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Code) || string.IsNullOrWhiteSpace(cmd.Name))
            throw AppException.Validation("Mã và tên vai trò là bắt buộc.");
        var code = cmd.Code.Trim().ToLowerInvariant();
        if (await _db.Roles.AnyAsync(r => r.Code == code, ct)) throw AppException.Conflict("duplicate_code", "Mã vai trò đã tồn tại.");
        var role = new Role(code, cmd.Name.Trim(), isSystem: false) { Description = cmd.Description };
        role.SetPermissions(_catalog.Validate(cmd.Permissions));
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("identity.role.create", "Role", role.Id.ToString(), cmd, ct);
        return ToDto(role);
    }

    public async Task<RoleDto> UpdateAsync(Guid id, SaveRole cmd, CancellationToken ct = default)
    {
        var role = await _db.Roles.Include(r => r.Permissions).SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw AppException.NotFound();
        if (role.Code == Roles.SystemAdmin) throw AppException.Conflict("system_role", "Không sửa quyền của vai trò Quản trị hệ thống.");
        role.Name = cmd.Name.Trim();
        role.Description = cmd.Description;
        role.SetPermissions(_catalog.Validate(cmd.Permissions));
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("identity.role.update", "Role", id.ToString(), cmd, ct);
        _permissions.Invalidate();
        return ToDto(role);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw AppException.NotFound();
        if (role.IsSystem) throw AppException.Conflict("system_role", "Không xóa vai trò mặc định.");
        if (await _db.AdminUserRoles.AnyAsync(x => x.RoleId == id, ct))
            throw AppException.Conflict("in_use", "Vai trò đang được gán cho tài khoản.");
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("identity.role.delete", "Role", id.ToString(), null, ct);
        _permissions.Invalidate();
    }

    private static RoleDto ToDto(Role r) =>
        new(r.Id, r.Code, r.Name, r.Description, r.IsSystem, r.Permissions.Select(p => p.Permission).OrderBy(p => p).ToList());
}

/// <summary>All permissions: core plus those contributed by modules.</summary>
public sealed class PermissionCatalog
{
    public PermissionCatalog(IEnumerable<IPermissionProvider> providers) =>
        All = Permissions.Core.Concat(providers.SelectMany(p => p.GetPermissions())).DistinctBy(p => p.Code).ToList();

    public IReadOnlyList<PermissionDefinition> All { get; }

    public IEnumerable<string> Validate(IEnumerable<string> codes)
    {
        var known = All.Select(p => p.Code).ToHashSet();
        var list = codes.ToList();
        var unknown = list.Where(c => !known.Contains(c)).ToList();
        if (unknown.Count > 0) throw AppException.Validation("Quyền không tồn tại: " + string.Join(", ", unknown));
        return list;
    }
}

public sealed class IdentitySeeder
{
    private readonly IIdentityDbContext _db;
    private readonly PermissionCatalog _catalog;
    private readonly IPasswordService _passwords;
    private readonly IClock _clock;

    public IdentitySeeder(IIdentityDbContext db, PermissionCatalog catalog, IPasswordService passwords, IClock clock)
    {
        _db = db;
        _catalog = catalog;
        _passwords = passwords;
        _clock = clock;
    }

    /// <summary>Creates missing default roles; system_admin always gets every permission. Idempotent.</summary>
    public async Task SeedAsync(string? bootstrapUser, string? bootstrapPassword, CancellationToken ct = default)
    {
        var roles = await _db.Roles.Include(r => r.Permissions).ToListAsync(ct);
        foreach (var seed in Roles.Defaults(_catalog.All.Select(p => p.Code)))
        {
            var role = roles.FirstOrDefault(r => r.Code == seed.Code);
            if (role is null)
            {
                role = new Role(seed.Code, seed.Name, isSystem: true);
                role.SetPermissions(seed.Permissions);
                _db.Roles.Add(role);
                roles.Add(role);
            }
            else if (seed.Code == Roles.SystemAdmin) role.SetPermissions(seed.Permissions);
        }
        await _db.SaveChangesAsync(ct);

        if (string.IsNullOrWhiteSpace(bootstrapUser) || string.IsNullOrWhiteSpace(bootstrapPassword)) return;
        if (await _db.AdminUsers.AnyAsync(ct)) return;
        var admin = new AdminUser(bootstrapUser, "Quản trị hệ thống", _passwords.Hash(bootstrapPassword), _clock.UtcNow);
        _db.AdminUsers.Add(admin);
        _db.AdminUserRoles.Add(new AdminUserRole(admin.Id, roles.Single(r => r.Code == Roles.SystemAdmin).Id, null));
        await _db.SaveChangesAsync(ct);
    }
}
