using CongDoan.Application.Shared;
using CongDoan.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Identity;

public sealed record CandidateDto(Guid Id, string Code, string DisplayName, string? Email, string? Phone, Guid? OrgUnitId,
    Guid? MemberId, bool IsLocked, bool IsDeleted, bool MustChangePassword, DateTime? LastLoginUtc);

public sealed record SaveCandidate(string Code, string DisplayName, string? Email, string? Phone, Guid? OrgUnitId, string? Password, Guid? MemberId = null);

public sealed record ImportedCredential(string Code, string Password);

public sealed record CandidateImportResult(int Created, IReadOnlyList<ImportedCredential> Credentials);

/// <summary>Candidate accounts. Every read and write is limited to the caller's org-unit scope (anti-IDOR).</summary>
public sealed class CandidateService
{
    private readonly IIdentityDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IPermissionChecker _permissions;
    private readonly ICurrentUser _user;
    private readonly AuthService _auth;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public CandidateService(IIdentityDbContext db, IPasswordService passwords, IPermissionChecker permissions, ICurrentUser user, AuthService auth, IClock clock, IAuditWriter audit)
    {
        _audit = audit;
        _db = db;
        _passwords = passwords;
        _permissions = permissions;
        _user = user;
        _auth = auth;
        _clock = clock;
    }

    public async Task<PagedResult<CandidateDto>> ListAsync(PageQuery q, Guid? orgUnitId, Guid? groupId, bool deleted = false, CancellationToken ct = default)
    {
        var scope = await _permissions.RequireScopeAsync(_user, Permissions.CandidateView, ct);
        var query = _db.Candidates.AsNoTracking().Where(c => c.IsDeleted == deleted);
        if (!scope.Global)
        {
            var units = scope.OrgUnits.ToList();
            query = query.Where(c => c.OrgUnitId != null && units.Contains(c.OrgUnitId.Value));
        }
        if (orgUnitId is not null) query = query.Where(c => c.OrgUnitId == orgUnitId);
        if (groupId is not null)
            query = query.Where(c => _db.CandidateGroupMembers.Any(m => m.GroupId == groupId && m.CandidateId == c.Id));
        if (!string.IsNullOrWhiteSpace(q.Q))
            query = query.Where(c => c.UserName.Contains(q.Q) || c.DisplayName.Contains(q.Q) || (c.Email != null && c.Email.Contains(q.Q)));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.UserName).Skip(q.Skip).Take(q.SafeSize).ToListAsync(ct);
        return new PagedResult<CandidateDto>(items.Select(ToDto).ToList(), total, q.SafePage, q.SafeSize);
    }

    public async Task<CandidateDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await LoadAsync(id, Permissions.CandidateView, track: false, ct));

    public async Task<CreatedAccount<CandidateDto>> CreateAsync(SaveCandidate cmd, CancellationToken ct = default)
    {
        await _permissions.EnsureAsync(_user, Permissions.CandidateManage, cmd.OrgUnitId, ct);
        Validate(cmd);
        var code = Account.Normalize(cmd.Code);
        if (await _db.Candidates.AnyAsync(c => c.UserName == code, ct))
            throw AppException.Conflict("duplicate_code", "Mã thí sinh đã tồn tại.");
        await EnsureOrgUnitAsync(cmd.OrgUnitId, ct);
        var password = string.IsNullOrEmpty(cmd.Password) ? Passwords.Generate() : cmd.Password;
        PasswordPolicy.Ensure(password);
        var c = new Candidate(code, cmd.DisplayName.Trim(), _passwords.Hash(password), _clock.UtcNow)
        {
            Email = cmd.Email, Phone = cmd.Phone, OrgUnitId = cmd.OrgUnitId, MemberId = cmd.MemberId,
        };
        _db.Candidates.Add(c);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("identity.candidate.create", "Candidate", c.Id.ToString(), new { c.UserName, c.OrgUnitId }, ct);
        return new CreatedAccount<CandidateDto>(ToDto(c), string.IsNullOrEmpty(cmd.Password) ? password : null);
    }

    public async Task<CandidateDto> UpdateAsync(Guid id, SaveCandidate cmd, CancellationToken ct = default)
    {
        var c = await LoadAsync(id, Permissions.CandidateManage, track: true, ct);
        if (cmd.OrgUnitId != c.OrgUnitId) await _permissions.EnsureAsync(_user, Permissions.CandidateManage, cmd.OrgUnitId, ct);
        if (string.IsNullOrWhiteSpace(cmd.DisplayName)) throw AppException.Validation("Họ tên là bắt buộc.");
        await EnsureOrgUnitAsync(cmd.OrgUnitId, ct);
        c.DisplayName = cmd.DisplayName.Trim();
        c.Email = cmd.Email;
        c.Phone = cmd.Phone;
        c.OrgUnitId = cmd.OrgUnitId;
        c.MemberId = cmd.MemberId;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("identity.candidate.update", "Candidate", id.ToString(), cmd, ct);
        return ToDto(c);
    }

    public async Task LockAsync(Guid id, bool locked, CancellationToken ct = default)
    {
        var c = await LoadAsync(id, Permissions.CandidateManage, track: true, ct);
        if (locked)
        {
            c.Lock();
            await _auth.RevokeSessionsAsync(AccountKind.Candidate, c.Id, ct: ct);
        }
        else c.Unlock();
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(locked ? "identity.candidate.lock" : "identity.candidate.unlock", "Candidate", id.ToString(), null, ct);
    }

    public async Task<string> ResetPasswordAsync(Guid id, CancellationToken ct = default)
    {
        var c = await LoadAsync(id, Permissions.CandidateManage, track: true, ct);
        var password = Passwords.Generate();
        c.SetPassword(_passwords.Hash(password), mustChange: true);
        c.Unlock();
        await _auth.RevokeSessionsAsync(AccountKind.Candidate, c.Id, ct: ct);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("identity.candidate.reset_password", "Candidate", id.ToString(), null, ct);
        return password;
    }

    public async Task DeleteAsync(Guid id, bool restore, CancellationToken ct = default)
    {
        var c = await LoadAsync(id, Permissions.CandidateManage, track: true, ct);
        if (restore) c.Restore();
        else
        {
            c.SoftDelete();
            await _auth.RevokeSessionsAsync(AccountKind.Candidate, c.Id, ct: ct);
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(restore ? "identity.candidate.restore" : "identity.candidate.delete", "Candidate", id.ToString(), null, ct);
    }

    /// <summary>CSV columns: ma, ho_ten, email, so_dien_thoai, ma_don_vi, mat_khau. All rows or none.</summary>
    public async Task<CandidateImportResult> ImportAsync(string csv, CancellationToken ct = default)
    {
        var scope = await _permissions.RequireScopeAsync(_user, Permissions.CandidateManage, ct);
        var units = await _db.OrgUnits.AsNoTracking().Where(o => !o.IsDeleted).ToDictionaryAsync(o => o.Code.ToLower(), o => o.Id, ct);
        var existing = (await _db.Candidates.AsNoTracking().Select(c => c.UserName).ToListAsync(ct)).ToHashSet();
        var errors = new List<RowError>();
        var seen = new HashSet<string>();
        var toCreate = new List<(Candidate C, string Password)>();

        foreach (var (row, get) in Csv.Records(csv ?? ""))
        {
            var code = Account.Normalize(get("ma"));
            var name = get("ho_ten");
            var unitCode = get("ma_don_vi").ToLowerInvariant();
            var password = get("mat_khau");
            var rowErrors = new List<string>();
            if (code.Length == 0) rowErrors.Add("thiếu mã thí sinh");
            else if (!seen.Add(code)) rowErrors.Add("mã trùng với dòng khác trong tệp");
            else if (existing.Contains(code)) rowErrors.Add("mã đã tồn tại");
            if (name.Length == 0) rowErrors.Add("thiếu họ tên");
            Guid? unitId = null;
            if (unitCode.Length > 0)
            {
                if (units.TryGetValue(unitCode, out var u)) unitId = u;
                else rowErrors.Add("mã đơn vị không tồn tại");
            }
            if (!scope.Global && !scope.Covers(unitId)) rowErrors.Add("đơn vị ngoài phạm vi quản lý");
            if (password.Length > 0 && (password.Length < 8 || !password.Any(char.IsDigit) || !password.Any(char.IsLetter)))
                rowErrors.Add("mật khẩu chưa đạt yêu cầu");
            if (rowErrors.Count > 0)
            {
                errors.Add(new RowError(row, string.Join("; ", rowErrors)));
                continue;
            }
            var pw = password.Length > 0 ? password : Passwords.Generate();
            var email = get("email");
            var phone = get("so_dien_thoai");
            toCreate.Add((new Candidate(code, name, _passwords.Hash(pw), _clock.UtcNow)
            {
                Email = email.Length > 0 ? email : null,
                Phone = phone.Length > 0 ? phone : null,
                OrgUnitId = unitId,
            }, pw));
        }
        if (errors.Count > 0) throw new RowErrorsException(errors);
        if (toCreate.Count == 0) throw AppException.Validation("Tệp không có dòng dữ liệu.");

        _db.Candidates.AddRange(toCreate.Select(x => x.C));
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("identity.candidate.import", "Candidate", "-", new { count = toCreate.Count }, ct);
        return new CandidateImportResult(toCreate.Count, toCreate.Select(x => new ImportedCredential(x.C.UserName, x.Password)).ToList());
    }

    private async Task<Candidate> LoadAsync(Guid id, string permission, bool track, CancellationToken ct)
    {
        var q = track ? _db.Candidates : _db.Candidates.AsNoTracking();
        var c = await q.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        await _permissions.EnsureAsync(_user, permission, c.OrgUnitId, ct);
        return c;
    }

    private async Task EnsureOrgUnitAsync(Guid? id, CancellationToken ct)
    {
        if (id is { } u && !await _db.OrgUnits.AnyAsync(o => o.Id == u && !o.IsDeleted, ct))
            throw AppException.Validation("Đơn vị không tồn tại.");
    }

    private static void Validate(SaveCandidate cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Code) || string.IsNullOrWhiteSpace(cmd.DisplayName))
            throw AppException.Validation("Mã thí sinh và họ tên là bắt buộc.");
    }

    private static CandidateDto ToDto(Candidate c) =>
        new(c.Id, c.UserName, c.DisplayName, c.Email, c.Phone, c.OrgUnitId, c.MemberId, c.IsLocked, c.IsDeleted, c.MustChangePassword, c.LastLoginUtc);
}
