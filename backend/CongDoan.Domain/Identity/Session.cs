namespace CongDoan.Domain.Identity;

public enum AccountKind
{
    Admin = 1,
    Candidate = 2,
}

/// <summary>Server-side login session; the auth cookie only carries its id so it can be revoked.</summary>
public sealed class Session
{
    private Session() { }

    public Session(AccountKind kind, Guid accountId, DateTime createdUtc, TimeSpan lifetime)
    {
        Id = Guid.NewGuid();
        Kind = kind;
        AccountId = accountId;
        CreatedUtc = createdUtc;
        ExpiresUtc = createdUtc.Add(lifetime);
    }

    public Guid Id { get; private set; }
    public AccountKind Kind { get; private set; }
    public Guid AccountId { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime ExpiresUtc { get; private set; }
    public DateTime? RevokedUtc { get; private set; }

    public bool IsActive(DateTime nowUtc) => RevokedUtc is null && ExpiresUtc > nowUtc;

    public void Revoke(DateTime nowUtc) => RevokedUtc ??= nowUtc;
}
