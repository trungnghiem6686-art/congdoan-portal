using System.Text.Json;
using System.Text.Json.Nodes;
using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public interface ICacheInvalidator
{
    /// <summary>Asks the frontend to drop cached pages tagged with <paramref name="tag"/>. Never throws.</summary>
    Task InvalidateAsync(string tag, CancellationToken ct = default);
}

/// <summary>Open exams shown by the "exam_notice" block; implemented by the exam module.</summary>
public interface IExamNoticeProvider
{
    Task<IReadOnlyList<object>> OpenExamsAsync(int limit, CancellationToken ct = default);
}

public sealed record SaveLayout(IReadOnlyList<SectionInput>? Sections, string? Note = null);
public sealed record PublishLayout(Guid RevisionId);
public sealed record RollbackLayout(int Version);

public sealed record LayoutRevisionDto(Guid Id, int Version, string Status, IReadOnlyList<Section> Sections, string? Note,
    DateTime CreatedUtc, DateTime? PublishedUtc, Guid? PublishedBy);

public sealed record LayoutHistoryItem(Guid Id, int Version, string Status, DateTime? PublishedUtc, Guid? PublishedBy, string? Note);

public sealed record LayoutState(LayoutRevisionDto? Draft, LayoutRevisionDto? Published, IReadOnlyList<LayoutHistoryItem> History);

public sealed record ResolvedSection(string Id, string Type, string? Title, JsonObject Config, object? Data);

public sealed record ResolvedLayout(int Version, DateTime? PublishedUtc, IReadOnlyList<ResolvedSection> Sections);

/// <summary>Draft / publish / rollback for block layouts (homepage and static pages share the same rules).</summary>
public sealed class LayoutService
{
    private readonly ICmsDbContext _db;
    private readonly SectionCatalog _catalog;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public LayoutService(ICmsDbContext db, SectionCatalog catalog, ICurrentUser user, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _catalog = catalog;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<LayoutState> StateAsync(string target, CancellationToken ct = default)
    {
        var rows = await _db.LayoutRevisions.AsNoTracking().Where(r => r.Target == target).ToListAsync(ct);
        var draft = rows.FirstOrDefault(r => r.Status == LayoutStatus.Draft);
        var published = rows.FirstOrDefault(r => r.Status == LayoutStatus.Published);
        var history = rows.Where(r => r.Status != LayoutStatus.Draft).OrderByDescending(r => r.Version)
            .Select(r => new LayoutHistoryItem(r.Id, r.Version, r.Status.ToString(), r.PublishedUtc, r.PublishedBy, r.Note)).ToList();
        return new LayoutState(draft is null ? null : ToDto(draft), published is null ? null : ToDto(published), history);
    }

    /// <summary>Creates or replaces the single draft for the target.</summary>
    public async Task<LayoutRevisionDto> SaveDraftAsync(string target, SaveLayout cmd, CancellationToken ct = default)
    {
        var sections = _catalog.Validate(cmd.Sections);
        var json = JsonSerializer.Serialize(sections, SectionCatalog.Json);
        var draft = await _db.LayoutRevisions.SingleOrDefaultAsync(r => r.Target == target && r.Status == LayoutStatus.Draft, ct);
        if (draft is null) _db.LayoutRevisions.Add(draft = new LayoutRevision(target, json, _user.AdminId, _clock.UtcNow));
        else draft.SectionsJson = json;
        draft.Note = cmd.Note?.Trim();
        draft.UpdatedUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(draft);
    }

    /// <summary>Atomically supersedes the live version and promotes the draft with the next version number.</summary>
    public async Task<LayoutRevisionDto> PublishAsync(string target, PublishLayout cmd, CancellationToken ct = default)
    {
        LayoutRevision? draft = null;
        await _db.InTransactionAsync(async () =>
        {
            draft = await _db.LayoutRevisions.SingleOrDefaultAsync(r => r.Id == cmd.RevisionId && r.Target == target, ct)
                    ?? throw AppException.NotFound("Không tìm thấy bản nháp.");
            if (draft.Status != LayoutStatus.Draft) throw AppException.Conflict("not_draft", "Phiên bản này đã được xuất bản.");
            await PromoteAsync(target, draft, ct);
        }, ct);
        await _audit.WriteAsync("cms.layout.publish", "Layout", target, new { draft!.Version }, ct);
        return ToDto(draft);
    }

    /// <summary>Publishes a copy of an older version as a new version (history is never rewritten).</summary>
    public async Task<LayoutRevisionDto> RollbackAsync(string target, RollbackLayout cmd, CancellationToken ct = default)
    {
        LayoutRevision? copy = null;
        await _db.InTransactionAsync(async () =>
        {
            var old = await _db.LayoutRevisions.AsNoTracking()
                          .SingleOrDefaultAsync(r => r.Target == target && r.Version == cmd.Version && r.Status != LayoutStatus.Draft, ct)
                      ?? throw AppException.NotFound("Không tìm thấy phiên bản.");
            copy = new LayoutRevision(target, old.SectionsJson, _user.AdminId, _clock.UtcNow) { Note = $"Khôi phục phiên bản {old.Version}" };
            var existingDraft = await _db.LayoutRevisions.SingleOrDefaultAsync(r => r.Target == target && r.Status == LayoutStatus.Draft, ct);
            if (existingDraft is not null) _db.LayoutRevisions.Remove(existingDraft);
            await _db.SaveChangesAsync(ct);
            _db.LayoutRevisions.Add(copy);
            await _db.SaveChangesAsync(ct);
            await PromoteAsync(target, copy, ct);
        }, ct);
        await _audit.WriteAsync("cms.layout.rollback", "Layout", target, new { cmd.Version, newVersion = copy!.Version }, ct);
        return ToDto(copy);
    }

    public async Task<IReadOnlyList<Section>?> PublishedSectionsAsync(string target, CancellationToken ct = default)
    {
        var row = await _db.LayoutRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Target == target && r.Status == LayoutStatus.Published, ct);
        return row is null ? null : Read(row.SectionsJson);
    }

    public async Task<(LayoutRevision? Row, IReadOnlyList<Section> Sections)> CurrentAsync(string target, bool draft, CancellationToken ct = default)
    {
        var rows = await _db.LayoutRevisions.AsNoTracking()
            .Where(r => r.Target == target && (r.Status == LayoutStatus.Published || (draft && r.Status == LayoutStatus.Draft))).ToListAsync(ct);
        var row = (draft ? rows.FirstOrDefault(r => r.Status == LayoutStatus.Draft) : null) ?? rows.FirstOrDefault(r => r.Status == LayoutStatus.Published);
        return (row, row is null ? Array.Empty<Section>() : Read(row.SectionsJson));
    }

    private async Task PromoteAsync(string target, LayoutRevision draft, CancellationToken ct)
    {
        var current = await _db.LayoutRevisions.SingleOrDefaultAsync(r => r.Target == target && r.Status == LayoutStatus.Published, ct);
        var next = (await _db.LayoutRevisions.Where(r => r.Target == target).MaxAsync(r => (int?)r.Version, ct) ?? 0) + 1;
        if (current is not null)
        {
            current.MarkSuperseded();
            await _db.SaveChangesAsync(ct);
        }
        draft.MarkPublished(next, _user.AdminId, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    public static IReadOnlyList<Section> Read(string json) =>
        JsonSerializer.Deserialize<List<Section>>(json, SectionCatalog.Json) ?? new List<Section>();

    private static LayoutRevisionDto ToDto(LayoutRevision r) =>
        new(r.Id, r.Version, r.Status.ToString(), Read(r.SectionsJson), r.Note, r.CreatedUtc, r.PublishedUtc, r.PublishedBy);
}

/// <summary>Fills each live block with the data it shows (published content only).</summary>
public sealed class SectionResolver
{
    private readonly ICmsDbContext _db;
    private readonly PublicPostService _posts;
    private readonly BannerService _banners;
    private readonly IExamNoticeProvider _exams;
    private readonly IClock _clock;

    public SectionResolver(ICmsDbContext db, PublicPostService posts, BannerService banners, IExamNoticeProvider exams, IClock clock)
    {
        _db = db;
        _posts = posts;
        _banners = banners;
        _exams = exams;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ResolvedSection>> ResolveAsync(IEnumerable<Section> sections, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var result = new List<ResolvedSection>();
        foreach (var s in sections.Where(s => s.IsLive(now)))
            result.Add(new ResolvedSection(s.Id, s.Type, s.Title, s.Config, await DataAsync(s, ct)));
        return result;
    }

    private async Task<object?> DataAsync(Section s, CancellationToken ct)
    {
        string? Str(string key) => s.Config[key]?.GetValue<string?>();
        int Int(string key, int fallback) => s.Config[key]?.GetValue<int>() ?? fallback;
        switch (s.Type)
        {
            case "featured_news" or "news_list" or "news_grid":
                var featured = s.Type == "featured_news";
                var page = await _posts.ListAsync(new PublicPostQuery(1, Int("limit", 6), Str("categorySlug"), Str("tagSlug"), featured ? true : null), ct);
                if (featured && page.Items.Count == 0)
                    page = await _posts.ListAsync(new PublicPostQuery(1, Int("limit", 6), Str("categorySlug"), Str("tagSlug")), ct);
                return new { posts = page.Items };
            case "banner":
                return new { banners = await _banners.LiveAsync(Str("position"), ct) };
            case "documents":
                var docType = Str("docType");
                var docs = _db.Documents.AsNoTracking().Where(d => d.IsVisible && !d.IsDeleted);
                if (docType is not null) docs = docs.Where(d => d.DocType == docType);
                var list = await docs.OrderByDescending(d => d.IssuedDate).Take(Int("limit", 5)).ToListAsync(ct);
                return new { documents = list.Select(DocumentService.ToDto).Select(d => new { d.Id, d.Number, d.Title, d.Slug, d.DocType, d.IssuedDate, d.FileUrl }) };
            case "gallery":
                var kind = Str("albumKind");
                var albums = _db.Albums.AsNoTracking().Where(a => a.IsVisible && !a.IsDeleted);
                if (Enum.TryParse<AlbumKind>(kind, out var k)) albums = albums.Where(a => a.Kind == k);
                var items = await albums.OrderBy(a => a.SortOrder).ThenByDescending(a => a.CreatedUtc).Take(Int("limit", 6)).ToListAsync(ct);
                return new { albums = items.Select(a => AlbumService.ToDto(a, Array.Empty<AlbumItemDto>())).Select(a => new { a.Id, a.Title, a.Slug, a.Kind, a.CoverUrl }) };
            case "exam_notice":
                return new { exams = await _exams.OpenExamsAsync(Int("limit", 5), ct) };
            default:
                return null;
        }
    }
}

public sealed class NoExamNotices : IExamNoticeProvider
{
    public Task<IReadOnlyList<object>> OpenExamsAsync(int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());
}
