using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public partial interface ICmsDbContext
{
    DbSet<Post> Posts { get; }
    DbSet<Category> Categories { get; }
    DbSet<Tag> Tags { get; }

    DbSet<LayoutRevision> LayoutRevisions { get; }

    /// <summary>Runs work in one database transaction (retry-safe).</summary>
    Task InTransactionAsync(Func<Task> work, CancellationToken ct = default);

    /// <summary>Stops tracking an entity (after a lost concurrency race).</summary>
    void Detach(object entity);

    /// <summary>Optimistic concurrency: compare against the version the client last saw.</summary>
    void ExpectRowVersion(object entity, byte[] rowVersion);
}

public static class RowVersions
{
    public static string Encode(byte[] v) => Convert.ToBase64String(v);

    public static byte[] Decode(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) throw Shared.AppException.Validation("Thiếu phiên bản dữ liệu (rowVersion).");
        try { return Convert.FromBase64String(v); }
        catch (FormatException) { throw Shared.AppException.Validation("Phiên bản dữ liệu không hợp lệ."); }
    }
}
