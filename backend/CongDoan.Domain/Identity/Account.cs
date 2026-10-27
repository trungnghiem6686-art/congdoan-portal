namespace CongDoan.Domain.Identity;

/// <summary>Common login state for admin users and candidates (stored in separate tables).</summary>
public abstract class Account
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    protected Account() { }

    protected Account(string userName, string displayName, string passwordHash, DateTime createdUtc)
    {
        Id = Guid.NewGuid();
        UserName = Normalize(userName);
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedUtc = createdUtc;
        MustChangePassword = true;
    }

    public Guid Id { get; private set; }
    public string UserName { get; private set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public Guid? OrgUnitId { get; set; }
    public string PasswordHash { get; private set; } = "";
    public bool MustChangePassword { get; set; }
    public bool IsLocked { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEndUtc { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime? LastLoginUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static string Normalize(string userName) => userName.Trim().ToLowerInvariant();

    public bool IsLockedOut(DateTime nowUtc) => LockoutEndUtc is { } end && end > nowUtc;

    public bool CanSignIn => !IsLocked && !IsDeleted;

    public void RegisterFailedLogin(DateTime nowUtc)
    {
        if (IsLockedOut(nowUtc)) return;
        if (LockoutEndUtc is not null)
        {
            // Previous lockout expired: start a fresh window.
            LockoutEndUtc = null;
            FailedLoginCount = 0;
        }
        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedLogins) LockoutEndUtc = nowUtc.Add(LockoutDuration);
    }

    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        FailedLoginCount = 0;
        LockoutEndUtc = null;
        LastLoginUtc = nowUtc;
    }

    public void SetPassword(string hash, bool mustChange)
    {
        PasswordHash = hash;
        MustChangePassword = mustChange;
    }

    public void Lock() => IsLocked = true;

    public void Unlock()
    {
        IsLocked = false;
        FailedLoginCount = 0;
        LockoutEndUtc = null;
    }

    public void SoftDelete() => IsDeleted = true;

    public void Restore() => IsDeleted = false;
}

public sealed class AdminUser : Account
{
    private AdminUser() { }

    public AdminUser(string userName, string displayName, string passwordHash, DateTime createdUtc)
        : base(userName, displayName, passwordHash, createdUtc) { }
}

public sealed class Candidate : Account
{
    private Candidate() { }

    public Candidate(string code, string displayName, string passwordHash, DateTime createdUtc)
        : base(code, displayName, passwordHash, createdUtc) { }

    /// <summary>Optional link to a union-member profile (future module); never required.</summary>
    public Guid? MemberId { get; set; }
    public string? Phone { get; set; }
}
