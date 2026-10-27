using CongDoan.Application.Shared;
using CongDoan.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Identity;

public sealed record AccountInfo(Guid Id, AccountKind Kind, string UserName, string DisplayName, bool MustChangePassword, Guid? OrgUnitId);

public sealed record LoginResult(AccountInfo Account, Guid SessionId, DateTime ExpiresUtc);

public sealed class AuthService
{
    public static readonly TimeSpan AdminSessionLifetime = TimeSpan.FromHours(8);
    public static readonly TimeSpan CandidateSessionLifetime = TimeSpan.FromHours(6);
    private const string InvalidMessage = "Tên đăng nhập hoặc mật khẩu không đúng.";

    private readonly IIdentityDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IClock _clock;
    private static string? _dummyHash;

    public AuthService(IIdentityDbContext db, IPasswordService passwords, IClock clock)
    {
        _db = db;
        _passwords = passwords;
        _clock = clock;
    }

    public async Task<LoginResult> LoginAsync(AccountKind kind, string userName, string password, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var account = await FindByUserNameAsync(kind, userName ?? "", ct);
        if (account is null)
        {
            // Spend the same hashing time as a real check so unknown users are not distinguishable.
            _passwords.Verify(_dummyHash ??= _passwords.Hash("not-a-real-password"), password ?? "");
            throw new AppException("invalid_credentials", InvalidMessage, 401);
        }
        if (account.IsLockedOut(now))
            throw new AppException("account_locked_out", "Tài khoản tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau 15 phút.", 401);

        if (!_passwords.Verify(account.PasswordHash, password ?? ""))
        {
            account.RegisterFailedLogin(now);
            await _db.SaveChangesAsync(ct);
            throw new AppException("invalid_credentials", InvalidMessage, 401);
        }
        if (!account.CanSignIn)
            throw new AppException("account_disabled", "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị.", 401);

        account.RegisterSuccessfulLogin(now);
        var session = new Session(kind, account.Id, now, kind == AccountKind.Admin ? AdminSessionLifetime : CandidateSessionLifetime);
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return new LoginResult(ToInfo(kind, account), session.Id, session.ExpiresUtc);
    }

    /// <summary>Returns the live account for a session, or null when the session or account is no longer valid.</summary>
    public async Task<AccountInfo?> ValidateSessionAsync(AccountKind kind, Guid sessionId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var session = await _db.Sessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId && s.Kind == kind, ct);
        if (session is null || !session.IsActive(now)) return null;
        var account = await FindByIdAsync(kind, session.AccountId, ct, track: false);
        if (account is null || !account.CanSignIn) return null;
        return ToInfo(kind, account);
    }

    public async Task LogoutAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null) return;
        session.Revoke(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ChangePasswordAsync(AccountKind kind, Guid accountId, Guid currentSessionId, string current, string newPassword, CancellationToken ct = default)
    {
        var account = await FindByIdAsync(kind, accountId, ct) ?? throw AppException.NotFound();
        if (!_passwords.Verify(account.PasswordHash, current ?? ""))
            throw new AppException("invalid_current_password", "Mật khẩu hiện tại không đúng.", 400);
        PasswordPolicy.Ensure(newPassword);
        if (_passwords.Verify(account.PasswordHash, newPassword))
            throw AppException.Validation("Mật khẩu mới phải khác mật khẩu hiện tại.");

        account.SetPassword(_passwords.Hash(newPassword), mustChange: false);
        await RevokeSessionsAsync(kind, accountId, except: currentSessionId, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeSessionsAsync(AccountKind kind, Guid accountId, Guid? except = null, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var sessions = await _db.Sessions
            .Where(s => s.Kind == kind && s.AccountId == accountId && s.RevokedUtc == null && s.Id != except)
            .ToListAsync(ct);
        foreach (var s in sessions) s.Revoke(now);
    }

    private async Task<Account?> FindByUserNameAsync(AccountKind kind, string userName, CancellationToken ct)
    {
        var normalized = Account.Normalize(userName);
        return kind == AccountKind.Admin
            ? await _db.AdminUsers.SingleOrDefaultAsync(a => a.UserName == normalized && !a.IsDeleted, ct)
            : await _db.Candidates.SingleOrDefaultAsync(a => a.UserName == normalized && !a.IsDeleted, ct);
    }

    private async Task<Account?> FindByIdAsync(AccountKind kind, Guid id, CancellationToken ct, bool track = true)
    {
        if (kind == AccountKind.Admin)
        {
            var q = track ? _db.AdminUsers : _db.AdminUsers.AsNoTracking();
            return await q.SingleOrDefaultAsync(a => a.Id == id, ct);
        }
        var c = track ? _db.Candidates : _db.Candidates.AsNoTracking();
        return await c.SingleOrDefaultAsync(a => a.Id == id, ct);
    }

    private static AccountInfo ToInfo(AccountKind kind, Account a) =>
        new(a.Id, kind, a.UserName, a.DisplayName, a.MustChangePassword, a.OrgUnitId);
}

public static class PasswordPolicy
{
    public static void Ensure(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw AppException.Validation("Mật khẩu phải có ít nhất 8 ký tự, gồm cả chữ và số.");
    }
}
