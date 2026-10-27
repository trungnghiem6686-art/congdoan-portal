using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public sealed record PublicAttachment(Guid Id, string Name, string ContentType, long Size, string Url);

public sealed record PublicPostSummary(Guid Id, string Title, string Slug, string? Summary, string? CoverUrl, string? CategoryName,
    string? CategorySlug, DateTime PublishedUtc, bool IsFeatured);

public sealed record PublicPost(Guid Id, string Title, string Slug, string? Summary, string Body, string? CoverUrl,
    string? CategoryName, string? CategorySlug, IReadOnlyList<string> Tags, IReadOnlyList<PublicAttachment> Attachments,
    DateTime PublishedUtc, DateTime UpdatedUtc, IReadOnlyList<PublicPostSummary> Related);

public sealed record PublicPostQuery(int Page = 1, int PageSize = 12, string? Category = null, string? Tag = null, bool? Featured = null);

/// <summary>Read side for visitors: only published, non-deleted posts whose publish time has passed.</summary>
public sealed class PublicPostService
{
    private readonly ICmsDbContext _db;
    private readonly IClock _clock;

    public PublicPostService(ICmsDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public IQueryable<Post> Visible()
    {
        var now = _clock.UtcNow;
        return _db.Posts.AsNoTracking().Where(p => !p.IsDeleted && p.Status == PostStatus.Published && p.PublishedUtc <= now);
    }

    public async Task<PagedResult<PublicPostSummary>> ListAsync(PublicPostQuery q, CancellationToken ct = default)
    {
        var page = new PageQuery(q.Page, Math.Min(q.PageSize, 50));
        var posts = Visible();
        if (!string.IsNullOrWhiteSpace(q.Category))
        {
            var catIds = await CategoryWithChildrenAsync(q.Category, ct);
            posts = posts.Where(p => p.CategoryId != null && catIds.Contains(p.CategoryId.Value));
        }
        if (!string.IsNullOrWhiteSpace(q.Tag))
            posts = posts.Where(p => p.Tags.Any(t => _db.Tags.Any(x => x.Id == t.TagId && x.Slug == q.Tag)));
        if (q.Featured is true) posts = posts.Where(p => p.IsFeatured);
        var total = await posts.CountAsync(ct);
        var items = await Summaries(posts.OrderByDescending(p => p.PublishedUtc).Skip(page.Skip).Take(page.SafeSize)).ToListAsync(ct);
        return new PagedResult<PublicPostSummary>(items, total, page.SafePage, page.SafeSize);
    }

    public async Task<PublicPost?> GetAsync(string slug, CancellationToken ct = default)
    {
        var p = await Visible().Include(x => x.Tags).Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Slug == slug, ct);
        if (p is null) return null;
        var cat = p.CategoryId is null ? null : await _db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == p.CategoryId, ct);
        var tagIds = p.Tags.Select(t => t.TagId).ToList();
        var tags = await _db.Tags.AsNoTracking().Where(t => tagIds.Contains(t.Id)).Select(t => t.Name).ToListAsync(ct);
        var attIds = p.Attachments.OrderBy(a => a.SortOrder).Select(a => a.MediaId).ToList();
        var media = await _db.MediaAssets.AsNoTracking()
            .Where(m => attIds.Contains(m.Id) && !m.IsDeleted && m.Visibility == MediaVisibility.Public).ToListAsync(ct);
        var attachments = attIds.Select(id => media.FirstOrDefault(m => m.Id == id)).Where(m => m is not null)
            .Select(m => new PublicAttachment(m!.Id, m.OriginalName, m.ContentType, m.Size, MediaService.PublicUrl(m.Id, m.OriginalName))).ToList();
        var related = p.CategoryId is null
            ? new List<PublicPostSummary>()
            : await Summaries(Visible().Where(x => x.CategoryId == p.CategoryId && x.Id != p.Id).OrderByDescending(x => x.PublishedUtc).Take(5)).ToListAsync(ct);
        return new PublicPost(p.Id, p.Title, p.Slug, p.Summary, p.Body, CoverUrl(p.CoverMediaId), cat?.Name, cat?.Slug, tags,
            attachments, p.PublishedUtc!.Value, p.UpdatedUtc, related);
    }

    public IQueryable<PublicPostSummary> Summaries(IQueryable<Post> posts) =>
        from p in posts
        join c in _db.Categories on p.CategoryId equals c.Id into cats
        from c in cats.DefaultIfEmpty()
        select new PublicPostSummary(p.Id, p.Title, p.Slug, p.Summary,
            p.CoverMediaId == null ? null : "/media/" + p.CoverMediaId.ToString(),
            c == null ? null : c.Name, c == null ? null : c.Slug, p.PublishedUtc!.Value, p.IsFeatured);

    private async Task<List<Guid>> CategoryWithChildrenAsync(string slug, CancellationToken ct)
    {
        var all = await _db.Categories.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new { c.Id, c.ParentId, c.Slug }).ToListAsync(ct);
        var root = all.FirstOrDefault(c => c.Slug == slug);
        if (root is null) return new List<Guid>();
        var result = new List<Guid> { root.Id };
        for (var i = 0; i < result.Count; i++) result.AddRange(all.Where(c => c.ParentId == result[i]).Select(c => c.Id));
        return result;
    }

    private static string? CoverUrl(Guid? id) => id is null ? null : $"/media/{id}";
}
