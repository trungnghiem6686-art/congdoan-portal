using CongDoan.Application.Shared;
using CongDoan.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Identity;

public sealed record RoleGrant(string RoleCode, Guid? OrgUnitId = null);

public sealed record AdminUserDto(Guid Id, string UserName, string DisplayName, string? Email, bool IsLocked, bool IsDeleted,
    bool MustChangePassword, DateTime? LastLoginUtc, IReadOnlyList<RoleGrant> Roles);

public sealed record CreateAdminUser(string UserName, string DisplayName, string? Email, string? Password, IReadOnlyList<RoleGrant>? Roles);

public sealed record UpdateAdminUser(string DisplayName, string? Email);

public sealed record CreatedAccount<T>(T Account, string? InitialPassword);

public sealed class AdminUserService
{
    private readonly IIdentityDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IPermissionChecker _permissions;
    private readonly AuthService _auth;
    private readonly IClock _clock;

    public AdminUserService(IIdentityDbContext db, IPasswordService passwords, IPermissionChecker permissions, AuthService auth, IClock clock)
    {
        _db = db;
        _passwords = passwords;
        _permissions = permissions;
        _auth = auth;
        _clock = clock;
    }

    public async Task<PagedResult<AdminUserDto>> ListAsync(PageQuery q, bool deleted = false, CancellationToken ct = default)
    {
        var query = _db.AdminUsers.AsNoTracking().Where(u => u.IsDeleted == deleted);
        if (!string.IsNullOrWhiteSpace(q.Q))
            query = query.Where(u => u.UserName.Contains(q.Q) || u.DisplayName.Contains(q.Q));
        var total = await query.CountAsync(ct);
        var users = await query.OrderBy(u => u.UserName).Skip(q.Skip).Take(q.SafeSize).ToListAsync(ct);
        var roles = await RolesOfAsync(users.Select(u => u.Id).ToList(), ct);
        return new PagedResult<AdminUserDto>(users.Select(u => ToDto(u, roles)).ToList(), total, q.SafePage, q.SafeSize);
    }

    public async Task<AdminUserDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var u = await _db.AdminUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        return ToDto(u, await RolesOfAsync(new List<Guid> { id }, ct));
    }

    public async Task<CreatedAccount<AdminUserDto>> CreateAsync(CreateAdminUser cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.UserName) || string.IsNullOrWhiteSpace(cmd.DisplayName))
            throw AppException.Validation("Tên đăng nhập và họ tên là bắt buộc.");
        var userName = Account.Normalize(cmd.UserName);
        if (await _db.AdminUsers.AnyAsync(u => u.UserName == userName, ct))
            throw AppException.Conflict("duplicate_username", "Tên đăng nhập đã tồn tại.");
        var password = string.IsNullOrEmpty(cmd.Password) ? Passwords.Generate() : cmd.Password;
        PasswordPolicy.Ensure(password);

        var user = new AdminUser(userName, cmd.DisplayName.Trim(), _passwords.Hash(password), _clock.UtcNow) { Email = cmd.Email };
        _db.AdminUsers.Add(user);
        await ReplaceRolesAsync(user.Id, cmd.Roles ?? Array.Empty<RoleGrant>(), ct);
        await _db.SaveChangesAsync(ct);
        _permissions.Invalidate();
        return new CreatedAccount<AdminUserDto>(await GetAsync(user.Id, ct), string.IsNullOrEmpty(cmd.Password) ? password : null);
    }

    public async Task<AdminUserDto> UpdateAsync(Guid id, UpdateAdminUser cmd, CancellationToken ct = default)
    {
        var u = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        if (string.IsNullOrWhiteSpace(cmd.DisplayName)) throw AppException.Validation("Họ tên là bắt buộc.");
        u.DisplayName = cmd.DisplayName.Trim();
        u.Email = cmd.Email;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminUserDto> SetRolesAsync(Guid id, IReadOnlyList<RoleGrant> roles, CancellationToken ct = default)
    {
        var u = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        var keepsSystemAdmin = await IsGlobalSystemAdminGrant(roles, ct);
        if (!keepsSystemAdmin) await EnsureNotLastSystemAdminAsync(u.Id, ct);
        await ReplaceRolesAsync(u.Id, roles, ct);
        await _db.SaveChangesAsync(ct);
        _permissions.Invalidate();
        return await GetAsync(id, ct);
    }

    public async Task LockAsync(Guid id, CancellationToken ct = default)
    {
        var u = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        await EnsureNotLastSystemAdminAsync(u.Id, ct);
        u.Lock();
        await _auth.RevokeSessionsAsync(AccountKind.Admin, u.Id, ct: ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UnlockAsync(Guid id, CancellationToken ct = default)
    {
        var u = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        u.Unlock();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string> ResetPasswordAsync(Guid id, CancellationToken ct = default)
    {
        var u = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        var password = Passwords.Generate();
        u.SetPassword(_passwords.Hash(password), mustChange: true);
        u.Unlock();
        await _auth.RevokeSessionsAsync(AccountKind.Admin, u.Id, ct: ct);
        await _db.SaveChangesAsync(ct);
        return password;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var u = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        await EnsureNotLastSystemAdminAsync(u.Id, ct);
        u.SoftDelete();
        await _auth.RevokeSessionsAsync(AccountKind.Admin, u.Id, ct: ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var u = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        u.Restore();
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Blocks any change that would leave no active, unscoped system administrator.</summary>
    private async Task EnsureNotLastSystemAdminAsync(Guid userId, CancellationToken ct)
    {
        var sysRoleId = await _db.Roles.Where(r => r.Code == Roles.SystemAdmin).Select(r => r.Id).SingleAsync(ct);
        var activeAdmins = from ur in _db.AdminUserRoles
                           join u in _db.AdminUsers on ur.AdminUserId equals u.Id
                           where ur.RoleId == sysRoleId && ur.OrgUnitId == null && !u.IsLocked && !u.IsDeleted
                           select u.Id;
        var ids = await activeAdmins.Distinct().ToListAsync(ct);
        if (ids.Contains(userId) && ids.Count == 1)
            throw AppException.Conflict("last_system_admin", "Không thể khóa, xóa hoặc hạ quyền quản trị hệ thống cuối cùng.");
    }

    private async Task<bool> IsGlobalSystemAdminGrant(IReadOnlyList<RoleGrant> roles, CancellationToken ct) =>
        roles.Any(r => r.RoleCode == Roles.SystemAdmin && r.OrgUnitId == null) && await _db.Roles.AnyAsync(r => r.Code == Roles.SystemAdmin, ct);

    private async Task ReplaceRolesAsync(Guid userId, IReadOnlyList<RoleGrant> grants, CancellationToken ct)
    {
        var codes = grants.Select(g => g.RoleCode).Distinct().ToList();
        var roles = await _db.Roles.Where(r => codes.Contains(r.Code)).ToDictionaryAsync(r => r.Code, ct);
        var missing = codes.Where(c => !roles.ContainsKey(c)).ToList();
        if (missing.Count > 0) throw AppException.Validation("Vai trò không tồn tại: " + string.Join(", ", missing));
        var unitIds = grants.Where(g => g.OrgUnitId != null).Select(g => g.OrgUnitId!.Value).Distinct().ToList();
        if (unitIds.Count > 0 && await _db.OrgUnits.CountAsync(o => unitIds.Contains(o.Id) && !o.IsDeleted, ct) != unitIds.Count)
            throw AppException.Validation("Đơn vị phạm vi không tồn tại.");

        var existing = await _db.AdminUserRoles.Where(x => x.AdminUserId == userId).ToListAsync(ct);
        _db.AdminUserRoles.RemoveRange(existing);
        foreach (var g in grants.DistinctBy(g => (g.RoleCode, g.OrgUnitId)))
            _db.AdminUserRoles.Add(new AdminUserRole(userId, roles[g.RoleCode].Id, g.OrgUnitId));
    }

    private async Task<ILookup<Guid, RoleGrant>> RolesOfAsync(List<Guid> userIds, CancellationToken ct)
    {
        var rows = await (from ur in _db.AdminUserRoles
                          join r in _db.Roles on ur.RoleId equals r.Id
                          where userIds.Contains(ur.AdminUserId)
                          select new { ur.AdminUserId, r.Code, ur.OrgUnitId }).ToListAsync(ct);
        return rows.ToLookup(r => r.AdminUserId, r => new RoleGrant(r.Code, r.OrgUnitId));
    }

    private static AdminUserDto ToDto(AdminUser u, ILookup<Guid, RoleGrant> roles) =>
        new(u.Id, u.UserName, u.DisplayName, u.Email, u.IsLocked, u.IsDeleted, u.MustChangePassword, u.LastLoginUtc, roles[u.Id].ToList());
}
