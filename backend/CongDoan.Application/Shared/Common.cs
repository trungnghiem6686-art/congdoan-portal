using System.Security.Cryptography;

namespace CongDoan.Application.Shared;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record PageQuery(int Page = 1, int PageSize = 20, string? Q = null)
{
    public int SafePage => Math.Max(1, Page);
    public int SafeSize => Math.Clamp(PageSize, 1, 200);
    public int Skip => (SafePage - 1) * SafeSize;
}

/// <summary>Who is acting in the current request (resolved from the auth cookie).</summary>
public interface ICurrentUser
{
    Guid? AdminId { get; }
    Guid? CandidateId { get; }
    string? UserName { get; }
    string? RequestId { get; }
    string? IpAddress { get; }
}

public sealed record RowError(int Row, string Message);

public sealed class RowErrorsException : AppException
{
    public RowErrorsException(IReadOnlyList<RowError> errors)
        : base("import_invalid", $"Tệp có {errors.Count} lỗi. Không dòng nào được ghi.", 400) => Errors = errors;

    public IReadOnlyList<RowError> Errors { get; }
}

public static class Passwords
{
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Random initial password (letters + digits, no ambiguous characters).</summary>
    public static string Generate(int length = 12)
    {
        while (true)
        {
            var chars = new char[length];
            for (var i = 0; i < length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            var s = new string(chars);
            if (s.Any(char.IsDigit) && s.Any(char.IsLetter)) return s;
        }
    }
}
