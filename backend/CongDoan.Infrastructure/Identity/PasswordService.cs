using CongDoan.Application.Identity;
using Microsoft.AspNetCore.Identity;

namespace CongDoan.Infrastructure.Identity;

/// <summary>ASP.NET Core Identity's PBKDF2 hasher (versioned format, automatic re-hash support).</summary>
public sealed class PasswordService : IPasswordService
{
    private static readonly object User = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(User, password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(User, hash, password) != PasswordVerificationResult.Failed;
}
