using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public static class PublicPaths
{
    public const string Post = "/tin-tuc/";
    public const string Category = "/chuyen-muc/";
    public const string Page = "/trang/";
    public const string Document = "/van-ban/";
    public const string Album = "/thu-vien/";

    /// <summary>Root segments owned by the application; menu items cannot take them.</summary>
    public static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "tin-tuc", "chuyen-muc", "trang", "van-ban", "thu-vien", "tim-kiem", "lien-he", "admin", "thi", "api", "media",
        "dang-nhap", "_next", "favicon-ico", "robots-txt", "sitemap-xml", "bao-tri", "loi",
    };

    public static string For(string kind, string slug) => kind switch
    {
        "post" => Post + slug,
        "category" => Category + slug,
        "page" => Page + slug,
        "document" => Document + slug,
        "album" => Album + slug,
        "menu" => "/" + slug,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string Normalize(string? path)
    {
        var p = (path ?? "").Trim();
        var q = p.IndexOfAny(new[] { '?', '#' });
        if (q >= 0) p = p[..q];
        p = Uri.UnescapeDataString(p).ToLowerInvariant();
        if (!p.StartsWith('/')) p = "/" + p;
        return p.Length > 1 ? p.TrimEnd('/') : p;
    }
}

public sealed record RedirectDto(Guid Id, string OldPath, string NewPath, int StatusCode, int Hits, DateTime CreatedUtc);

public sealed class RedirectService : ISlugHistory
{
    private readonly ICmsDbContext _db;
    private readonly IClock _clock;

    public RedirectService(ICmsDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task RecordAsync(string kind, string oldSlug, string newSlug, CancellationToken ct = default) =>
        AddAsync(PublicPaths.For(kind, oldSlug), PublicPaths.For(kind, newSlug), ct);

    /// <summary>Adds old → new, re-points older redirects to the final target and drops a redirect from the new path.</summary>
    public async Task AddAsync(string oldPath, string newPath, CancellationToken ct = default)
    {
        oldPath = PublicPaths.Normalize(oldPath);
        newPath = PublicPaths.Normalize(newPath);
        if (oldPath == newPath) return;
        var related = await _db.Redirects.Where(r => r.OldPath == oldPath || r.NewPath == oldPath || r.OldPath == newPath).ToListAsync(ct);
        foreach (var r in related.Where(r => r.NewPath == oldPath)) r.NewPath = newPath;
        _db.Redirects.RemoveRange(related.Where(r => r.OldPath == newPath || r.OldPath == oldPath));
        _db.Redirects.Add(new Redirect(oldPath, newPath, _clock.UtcNow));
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> FindAsync(string path, CancellationToken ct = default)
    {
        var p = PublicPaths.Normalize(path);
        var r = await _db.Redirects.SingleOrDefaultAsync(x => x.OldPath == p, ct);
        if (r is null) return null;
        r.Hits++;
        await _db.SaveChangesAsync(ct);
        return r.NewPath;
    }

    public async Task<PagedResult<RedirectDto>> ListAsync(PageQuery q, CancellationToken ct = default)
    {
        var query = _db.Redirects.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Q)) query = query.Where(r => r.OldPath.Contains(q.Q) || r.NewPath.Contains(q.Q));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.CreatedUtc).Skip(q.Skip).Take(q.SafeSize)
            .Select(r => new RedirectDto(r.Id, r.OldPath, r.NewPath, r.StatusCode, r.Hits, r.CreatedUtc)).ToListAsync(ct);
        return new PagedResult<RedirectDto>(items, total, q.SafePage, q.SafeSize);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _db.Redirects.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        _db.Redirects.Remove(r);
        await _db.SaveChangesAsync(ct);
    }
}
