using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public sealed record SaveBanner(string Title, string? Position, Guid? ImageMediaId, string? LinkUrl, string? Description,
    DateTime? StartsUtc, DateTime? EndsUtc, int SortOrder = 0, bool IsVisible = true, string? RowVersion = null);

public sealed record BannerDto(Guid Id, string Title, string Position, Guid? ImageMediaId, string? ImageUrl, string? LinkUrl, string? Description,
    DateTime? StartsUtc, DateTime? EndsUtc, int SortOrder, bool IsVisible, bool IsDeleted, string RowVersion);

public sealed record SaveDocument(string Number, string Title, string? Slug, string DocType, string? Issuer, DateOnly IssuedDate,
    DateOnly? EffectiveDate, string? Summary, Guid? FileMediaId, int SortOrder = 0, bool IsVisible = true, string? RowVersion = null);

public sealed record DocumentDto(Guid Id, string Number, string Title, string Slug, string DocType, string? Issuer, DateOnly IssuedDate,
    DateOnly? EffectiveDate, string? Summary, Guid? FileMediaId, string? FileUrl, int SortOrder, bool IsVisible, bool IsDeleted, string RowVersion);

public sealed record SaveAlbum(string Title, string? Slug, string Kind, string? Description, Guid? CoverMediaId, DateTime? EventDate,
    int SortOrder = 0, bool IsVisible = true, string? RowVersion = null);

public sealed record AlbumItemDto(Guid Id, Guid? MediaId, string? Url, string? ThumbnailUrl, string? EmbedUrl, string? Caption, int SortOrder);

public sealed record AlbumDto(Guid Id, string Title, string Slug, string Kind, string? Description, Guid? CoverMediaId, string? CoverUrl,
    DateTime? EventDate, int SortOrder, bool IsVisible, bool IsDeleted, string RowVersion, IReadOnlyList<AlbumItemDto> Items);

public sealed record SaveAlbumItem(Guid? MediaId, string? EmbedUrl, string? Caption, int? SortOrder);

public sealed record SavePage(string Title, string? Slug, string? Body, string? SeoDescription, bool IsVisible = true, string? RowVersion = null);

public sealed record PageDto(Guid Id, string Title, string Slug, bool SlugIsManual, string Body, string? SeoDescription, string Path,
    bool IsVisible, bool IsDeleted, DateTime UpdatedUtc, string RowVersion);

public sealed record ItemQuery(int Page = 1, int PageSize = 20, string? Q = null, bool Deleted = false);

public sealed class BannerService
{
    private readonly ICmsDbContext _db;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly IMediaUsageTracker _usages;

    public BannerService(ICmsDbContext db, IAuditWriter audit, IClock clock, IMediaUsageTracker usages)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _usages = usages;
    }

    public async Task<PagedResult<BannerDto>> ListAsync(ItemQuery q, string? position, CancellationToken ct = default)
    {
        var page = new PageQuery(q.Page, q.PageSize);
        var query = _db.Banners.AsNoTracking().Where(b => b.IsDeleted == q.Deleted);
        if (!string.IsNullOrWhiteSpace(position)) query = query.Where(b => b.Position == position);
        if (!string.IsNullOrWhiteSpace(q.Q)) query = query.Where(b => b.Title.Contains(q.Q));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(b => b.Position).ThenBy(b => b.SortOrder).Skip(page.Skip).Take(page.SafeSize).ToListAsync(ct);
        return new PagedResult<BannerDto>(items.Select(ToDto).ToList(), total, page.SafePage, page.SafeSize);
    }

    public async Task<BannerDto> GetAsync(Guid id, CancellationToken ct = default) => ToDto(await _db.Banners.LoadAsync(id, ct));

    public async Task<IReadOnlyList<BannerDto>> LiveAsync(string? position, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var query = _db.Banners.AsNoTracking().Where(b => b.IsVisible && !b.IsDeleted
            && (b.StartsUtc == null || b.StartsUtc <= now) && (b.EndsUtc == null || b.EndsUtc > now));
        if (!string.IsNullOrWhiteSpace(position)) query = query.Where(b => b.Position == position);
        return (await query.OrderBy(b => b.SortOrder).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<BannerDto> CreateAsync(SaveBanner cmd, CancellationToken ct = default)
    {
        var b = new Banner();
        Apply(b, cmd);
        CmsItemOps.Touch(b, _clock, created: true);
        _db.Banners.Add(b);
        await _db.SaveChangesAsync(ct);
        await AfterAsync(b, "create", ct);
        return ToDto(b);
    }

    public async Task<BannerDto> UpdateAsync(Guid id, SaveBanner cmd, CancellationToken ct = default)
    {
        var b = await _db.Banners.LoadAsync(id, ct);
        if (cmd.RowVersion is not null) _db.ExpectRowVersion(b, RowVersions.Decode(cmd.RowVersion));
        Apply(b, cmd);
        CmsItemOps.Touch(b, _clock);
        await _db.SaveChangesAsync(ct);
        await AfterAsync(b, "update", ct);
        return ToDto(b);
    }

    public Task SetVisibleAsync(Guid id, bool v, CancellationToken ct = default) => CmsItemOps.SetVisibleAsync(_db, _db.Banners, _audit, id, v, ct);
    public Task SetDeletedAsync(Guid id, bool d, CancellationToken ct = default) => CmsItemOps.SetDeletedAsync(_db, _db.Banners, _audit, _clock, id, d, ct);

    private static void Apply(Banner b, SaveBanner cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Title)) throw AppException.Validation("Tiêu đề banner là bắt buộc.");
        CmsItemOps.EnsureSafeUrl(cmd.LinkUrl);
        if (cmd.StartsUtc is { } s && cmd.EndsUtc is { } e && e <= s) throw AppException.Validation("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
        b.Title = cmd.Title.Trim();
        b.Position = string.IsNullOrWhiteSpace(cmd.Position) ? "home_top" : cmd.Position.Trim();
        b.ImageMediaId = cmd.ImageMediaId;
        b.LinkUrl = cmd.LinkUrl?.Trim();
        b.Description = cmd.Description?.Trim();
        b.StartsUtc = cmd.StartsUtc;
        b.EndsUtc = cmd.EndsUtc;
        b.SortOrder = cmd.SortOrder;
        b.IsVisible = cmd.IsVisible;
    }

    private async Task AfterAsync(Banner b, string verb, CancellationToken ct)
    {
        await _usages.SetUsagesAsync("Banner", b.Id, b.Title, b.ImageMediaId is { } m ? new[] { m } : Array.Empty<Guid>(), ct);
        await _audit.WriteAsync($"cms.banner.{verb}", "Banner", b.Id.ToString(), new { b.Title, b.Position }, ct);
    }

    private static BannerDto ToDto(Banner b) => new(b.Id, b.Title, b.Position, b.ImageMediaId, b.ImageMediaId is null ? null : $"/media/{b.ImageMediaId}",
        b.LinkUrl, b.Description, b.StartsUtc, b.EndsUtc, b.SortOrder, b.IsVisible, b.IsDeleted, RowVersions.Encode(b.RowVersion));
}

public sealed class DocumentService
{
    private readonly ICmsDbContext _db;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly IMediaUsageTracker _usages;
    private readonly ISlugHistory _history;

    public DocumentService(ICmsDbContext db, IAuditWriter audit, IClock clock, IMediaUsageTracker usages, ISlugHistory history)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _usages = usages;
        _history = history;
    }

    public async Task<PagedResult<DocumentDto>> ListAsync(ItemQuery q, CancellationToken ct = default)
    {
        var page = new PageQuery(q.Page, q.PageSize);
        var query = _db.Documents.AsNoTracking().Where(d => d.IsDeleted == q.Deleted);
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = Slug.SearchText(q.Q);
            query = query.Where(d => d.Number.Contains(q.Q) || d.SearchText.Contains(term));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(d => d.IssuedDate).Skip(page.Skip).Take(page.SafeSize).ToListAsync(ct);
        return new PagedResult<DocumentDto>(items.Select(ToDto).ToList(), total, page.SafePage, page.SafeSize);
    }

    public async Task<DocumentDto> GetAsync(Guid id, CancellationToken ct = default) => ToDto(await _db.Documents.LoadAsync(id, ct));

    public async Task<DocumentDto> CreateAsync(SaveDocument cmd, CancellationToken ct = default)
    {
        var d = new Document();
        var (slug, _) = SlugRules.Resolve(cmd.Slug, $"{cmd.Number} {cmd.Title}");
        d.Slug = await FreeSlugAsync(slug, null, ct);
        Apply(d, cmd);
        CmsItemOps.Touch(d, _clock, created: true);
        _db.Documents.Add(d);
        await _db.SaveChangesAsync(ct);
        await AfterAsync(d, "create", ct);
        return ToDto(d);
    }

    public async Task<DocumentDto> UpdateAsync(Guid id, SaveDocument cmd, CancellationToken ct = default)
    {
        var d = await _db.Documents.LoadAsync(id, ct);
        if (cmd.RowVersion is not null) _db.ExpectRowVersion(d, RowVersions.Decode(cmd.RowVersion));
        var old = d.Slug;
        if (!string.IsNullOrWhiteSpace(cmd.Slug) && Slug.From(cmd.Slug) != d.Slug)
            d.Slug = await FreeSlugAsync(Slug.From(cmd.Slug), id, ct, strict: true);
        Apply(d, cmd);
        CmsItemOps.Touch(d, _clock);
        await _db.SaveChangesAsync(ct);
        if (old != d.Slug) await _history.RecordAsync("document", old, d.Slug, ct);
        await AfterAsync(d, "update", ct);
        return ToDto(d);
    }

    public Task SetVisibleAsync(Guid id, bool v, CancellationToken ct = default) => CmsItemOps.SetVisibleAsync(_db, _db.Documents, _audit, id, v, ct);
    public Task SetDeletedAsync(Guid id, bool d, CancellationToken ct = default) => CmsItemOps.SetDeletedAsync(_db, _db.Documents, _audit, _clock, id, d, ct);

    private static void Apply(Document d, SaveDocument cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Number) || string.IsNullOrWhiteSpace(cmd.Title) || string.IsNullOrWhiteSpace(cmd.DocType))
            throw AppException.Validation("Số hiệu, trích yếu và loại văn bản là bắt buộc.");
        if (cmd.IssuedDate == default) throw AppException.Validation("Ngày ban hành là bắt buộc.");
        d.Number = cmd.Number.Trim();
        d.Title = cmd.Title.Trim();
        d.DocType = cmd.DocType.Trim();
        d.Issuer = cmd.Issuer?.Trim();
        d.IssuedDate = cmd.IssuedDate;
        d.EffectiveDate = cmd.EffectiveDate;
        d.Summary = cmd.Summary?.Trim();
        d.FileMediaId = cmd.FileMediaId;
        d.SortOrder = cmd.SortOrder;
        d.IsVisible = cmd.IsVisible;
        d.SearchText = Slug.SearchText($"{d.Number} {d.Title} {d.DocType} {d.Issuer} {d.Summary}");
    }

    private async Task<string> FreeSlugAsync(string slug, Guid? self, CancellationToken ct, bool strict = false)
    {
        Task<bool> Taken(string s) => _db.Documents.AnyAsync(x => x.Slug == s && x.Id != self, ct);
        if (!await Taken(slug)) return slug;
        var conflict = (SlugConflictException)await SlugRules.DuplicateAsync(slug, Taken);
        if (strict) throw conflict;
        return conflict.Suggestion;
    }

    private async Task AfterAsync(Document d, string verb, CancellationToken ct)
    {
        await _usages.SetUsagesAsync("Document", d.Id, d.Title, d.FileMediaId is { } m ? new[] { m } : Array.Empty<Guid>(), ct);
        await _audit.WriteAsync($"cms.document.{verb}", "Document", d.Id.ToString(), new { d.Number, d.Title }, ct);
    }

    public static DocumentDto ToDto(Document d) => new(d.Id, d.Number, d.Title, d.Slug, d.DocType, d.Issuer, d.IssuedDate, d.EffectiveDate,
        d.Summary, d.FileMediaId, d.FileMediaId is null ? null : $"/media/{d.FileMediaId}", d.SortOrder, d.IsVisible, d.IsDeleted, RowVersions.Encode(d.RowVersion));
}

public sealed class AlbumService
{
    private readonly ICmsDbContext _db;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly IMediaUsageTracker _usages;
    private readonly CmsOptions _cms;
    private readonly ISlugHistory _history;

    public AlbumService(ICmsDbContext db, IAuditWriter audit, IClock clock, IMediaUsageTracker usages,
        Microsoft.Extensions.Options.IOptions<CmsOptions> cms, ISlugHistory history)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _usages = usages;
        _cms = cms.Value;
        _history = history;
    }

    public async Task<PagedResult<AlbumDto>> ListAsync(ItemQuery q, CancellationToken ct = default)
    {
        var page = new PageQuery(q.Page, q.PageSize);
        var query = _db.Albums.AsNoTracking().Where(a => a.IsDeleted == q.Deleted);
        if (!string.IsNullOrWhiteSpace(q.Q)) query = query.Where(a => a.Title.Contains(q.Q));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(a => a.SortOrder).ThenByDescending(a => a.CreatedUtc).Skip(page.Skip).Take(page.SafeSize).ToListAsync(ct);
        return new PagedResult<AlbumDto>(items.Select(a => ToDto(a, Array.Empty<AlbumItemDto>())).ToList(), total, page.SafePage, page.SafeSize);
    }

    public async Task<AlbumDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var a = await _db.Albums.LoadAsync(id, ct);
        return ToDto(a, await ItemsAsync(id, ct));
    }

    public async Task<AlbumDto> CreateAsync(SaveAlbum cmd, CancellationToken ct = default)
    {
        var a = new Album();
        var (slug, _) = SlugRules.Resolve(cmd.Slug, cmd.Title);
        await EnsureSlugFreeAsync(slug, null, ct);
        a.Slug = slug;
        Apply(a, cmd);
        CmsItemOps.Touch(a, _clock, created: true);
        _db.Albums.Add(a);
        await _db.SaveChangesAsync(ct);
        await TrackAsync(a, ct);
        await _audit.WriteAsync("cms.album.create", "Album", a.Id.ToString(), new { a.Title }, ct);
        return ToDto(a, Array.Empty<AlbumItemDto>());
    }

    public async Task<AlbumDto> UpdateAsync(Guid id, SaveAlbum cmd, CancellationToken ct = default)
    {
        var a = await _db.Albums.LoadAsync(id, ct);
        if (cmd.RowVersion is not null) _db.ExpectRowVersion(a, RowVersions.Decode(cmd.RowVersion));
        var old = a.Slug;
        var (slug, _) = SlugRules.Resolve(cmd.Slug, cmd.Title);
        if (slug != a.Slug) await EnsureSlugFreeAsync(slug, id, ct);
        a.Slug = slug;
        Apply(a, cmd);
        CmsItemOps.Touch(a, _clock);
        await _db.SaveChangesAsync(ct);
        if (old != slug) await _history.RecordAsync("album", old, slug, ct);
        await TrackAsync(a, ct);
        await _audit.WriteAsync("cms.album.update", "Album", id.ToString(), new { a.Title }, ct);
        return ToDto(a, await ItemsAsync(id, ct));
    }

    public async Task<AlbumItemDto> AddItemAsync(Guid albumId, SaveAlbumItem cmd, CancellationToken ct = default)
    {
        var a = await _db.Albums.LoadAsync(albumId, ct);
        if (cmd.MediaId is null && string.IsNullOrWhiteSpace(cmd.EmbedUrl)) throw AppException.Validation("Chọn tệp hoặc nhập liên kết nhúng.");
        if (!string.IsNullOrWhiteSpace(cmd.EmbedUrl) && !_cms.IsAllowedEmbed(cmd.EmbedUrl))
            throw new AppException("embed_not_allowed", "Nguồn nhúng không nằm trong danh sách cho phép (ví dụ YouTube).", 400);
        if (cmd.MediaId is { } m && !await _db.MediaAssets.AnyAsync(x => x.Id == m && !x.IsDeleted, ct))
            throw AppException.Validation("Tệp không tồn tại.");
        var order = cmd.SortOrder ?? await _db.AlbumItems.CountAsync(i => i.AlbumId == albumId, ct);
        var item = new AlbumItem(albumId, cmd.MediaId, cmd.EmbedUrl?.Trim(), cmd.Caption?.Trim(), order);
        _db.AlbumItems.Add(item);
        await _db.SaveChangesAsync(ct);
        await TrackAsync(a, ct);
        return ToItem(item);
    }

    public async Task RemoveItemAsync(Guid albumId, Guid itemId, CancellationToken ct = default)
    {
        var a = await _db.Albums.LoadAsync(albumId, ct);
        var item = await _db.AlbumItems.SingleOrDefaultAsync(i => i.Id == itemId && i.AlbumId == albumId, ct) ?? throw AppException.NotFound();
        _db.AlbumItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        await TrackAsync(a, ct);
    }

    public async Task ReorderItemsAsync(Guid albumId, IReadOnlyList<Guid> order, CancellationToken ct = default)
    {
        var items = await _db.AlbumItems.Where(i => i.AlbumId == albumId).ToListAsync(ct);
        for (var i = 0; i < order.Count; i++)
            if (items.FirstOrDefault(x => x.Id == order[i]) is { } item) item.SortOrder = i;
        await _db.SaveChangesAsync(ct);
    }

    public Task SetVisibleAsync(Guid id, bool v, CancellationToken ct = default) => CmsItemOps.SetVisibleAsync(_db, _db.Albums, _audit, id, v, ct);
    public Task SetDeletedAsync(Guid id, bool d, CancellationToken ct = default) => CmsItemOps.SetDeletedAsync(_db, _db.Albums, _audit, _clock, id, d, ct);

    public async Task<IReadOnlyList<AlbumItemDto>> ItemsAsync(Guid albumId, CancellationToken ct = default) =>
        (await _db.AlbumItems.AsNoTracking().Where(i => i.AlbumId == albumId).OrderBy(i => i.SortOrder).ToListAsync(ct)).Select(ToItem).ToList();

    private static void Apply(Album a, SaveAlbum cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Title)) throw AppException.Validation("Tên album là bắt buộc.");
        if (!Enum.TryParse<AlbumKind>(cmd.Kind, true, out var kind)) throw AppException.Validation("Loại album không hợp lệ.");
        a.Title = cmd.Title.Trim();
        a.Kind = kind;
        a.Description = cmd.Description?.Trim();
        a.CoverMediaId = cmd.CoverMediaId;
        a.EventDate = cmd.EventDate;
        a.SearchText = Slug.SearchText($"{a.Title} {a.Description}");
        a.SortOrder = cmd.SortOrder;
        a.IsVisible = cmd.IsVisible;
    }

    private async Task EnsureSlugFreeAsync(string slug, Guid? self, CancellationToken ct)
    {
        Task<bool> Taken(string s) => _db.Albums.AnyAsync(x => x.Slug == s && x.Id != self, ct);
        if (await Taken(slug)) throw await SlugRules.DuplicateAsync(slug, Taken);
    }

    private async Task TrackAsync(Album a, CancellationToken ct)
    {
        var ids = await _db.AlbumItems.Where(i => i.AlbumId == a.Id && i.MediaId != null).Select(i => i.MediaId!.Value).ToListAsync(ct);
        if (a.CoverMediaId is { } c) ids.Add(c);
        await _usages.SetUsagesAsync("Album", a.Id, a.Title, ids, ct);
    }

    private static AlbumItemDto ToItem(AlbumItem i) => new(i.Id, i.MediaId, i.MediaId is null ? null : $"/media/{i.MediaId}",
        i.MediaId is null ? null : $"/media/{i.MediaId}/thumb", i.EmbedUrl, i.Caption, i.SortOrder);

    public static AlbumDto ToDto(Album a, IReadOnlyList<AlbumItemDto> items) => new(a.Id, a.Title, a.Slug, a.Kind.ToString(), a.Description,
        a.CoverMediaId, a.CoverMediaId is null ? null : $"/media/{a.CoverMediaId}/thumb", a.EventDate, a.SortOrder, a.IsVisible, a.IsDeleted,
        RowVersions.Encode(a.RowVersion), items);
}

public sealed class PageService
{
    private readonly ICmsDbContext _db;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly IContentSanitizer _sanitizer;
    private readonly IMediaUsageTracker _usages;
    private readonly ISlugHistory _history;

    public PageService(ICmsDbContext db, IAuditWriter audit, IClock clock, IContentSanitizer sanitizer, IMediaUsageTracker usages, ISlugHistory history)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _sanitizer = sanitizer;
        _usages = usages;
        _history = history;
    }

    public async Task<PagedResult<PageDto>> ListAsync(ItemQuery q, CancellationToken ct = default)
    {
        var page = new PageQuery(q.Page, q.PageSize);
        var query = _db.Pages.AsNoTracking().Where(p => p.IsDeleted == q.Deleted);
        if (!string.IsNullOrWhiteSpace(q.Q)) query = query.Where(p => p.Title.Contains(q.Q));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Title).Skip(page.Skip).Take(page.SafeSize).ToListAsync(ct);
        return new PagedResult<PageDto>(items.Select(ToDto).ToList(), total, page.SafePage, page.SafeSize);
    }

    public async Task<PageDto> GetAsync(Guid id, CancellationToken ct = default) => ToDto(await _db.Pages.LoadAsync(id, ct));

    public async Task<PageDto> CreateAsync(SavePage cmd, CancellationToken ct = default)
    {
        var p = new Page();
        var (slug, manual) = SlugRules.Resolve(cmd.Slug, cmd.Title);
        await EnsureSlugFreeAsync(slug, null, ct);
        p.Slug = slug;
        p.SlugIsManual = manual;
        Apply(p, cmd);
        CmsItemOps.Touch(p, _clock, created: true);
        _db.Pages.Add(p);
        await _db.SaveChangesAsync(ct);
        await AfterAsync(p, "create", ct);
        return ToDto(p);
    }

    public async Task<PageDto> UpdateAsync(Guid id, SavePage cmd, CancellationToken ct = default)
    {
        var p = await _db.Pages.LoadAsync(id, ct);
        if (cmd.RowVersion is not null) _db.ExpectRowVersion(p, RowVersions.Decode(cmd.RowVersion));
        var old = p.Slug;
        var (slug, manual) = string.IsNullOrWhiteSpace(cmd.Slug) && p.SlugIsManual ? (p.Slug, true) : SlugRules.Resolve(cmd.Slug, cmd.Title);
        if (slug != p.Slug) await EnsureSlugFreeAsync(slug, id, ct);
        p.Slug = slug;
        p.SlugIsManual = manual;
        Apply(p, cmd);
        CmsItemOps.Touch(p, _clock);
        await _db.SaveChangesAsync(ct);
        if (old != slug) await _history.RecordAsync("page", old, slug, ct);
        await AfterAsync(p, "update", ct);
        return ToDto(p);
    }

    public Task SetVisibleAsync(Guid id, bool v, CancellationToken ct = default) => CmsItemOps.SetVisibleAsync(_db, _db.Pages, _audit, id, v, ct);
    public Task SetDeletedAsync(Guid id, bool d, CancellationToken ct = default) => CmsItemOps.SetDeletedAsync(_db, _db.Pages, _audit, _clock, id, d, ct);

    private void Apply(Page p, SavePage cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Title)) throw AppException.Validation("Tiêu đề trang là bắt buộc.");
        p.Title = cmd.Title.Trim();
        p.Body = _sanitizer.Sanitize(cmd.Body);
        p.SeoDescription = cmd.SeoDescription?.Trim();
        p.IsVisible = cmd.IsVisible;
        p.SearchText = Slug.SearchText($"{p.Title} {PostService.StripTags(p.Body)}");
    }

    private async Task EnsureSlugFreeAsync(string slug, Guid? self, CancellationToken ct)
    {
        Task<bool> Taken(string s) => _db.Pages.AnyAsync(x => x.Slug == s && x.Id != self, ct);
        if (await Taken(slug)) throw await SlugRules.DuplicateAsync(slug, Taken);
    }

    private async Task AfterAsync(Page p, string verb, CancellationToken ct)
    {
        await _usages.SetUsagesAsync("Page", p.Id, p.Title, PostService.MediaRefs(p.Body), ct);
        await _audit.WriteAsync($"cms.page.{verb}", "Page", p.Id.ToString(), new { p.Title, p.Slug }, ct);
    }

    public static PageDto ToDto(Page p) => new(p.Id, p.Title, p.Slug, p.SlugIsManual, p.Body, p.SeoDescription, PublicPaths.For("page", p.Slug),
        p.IsVisible, p.IsDeleted, p.UpdatedUtc, RowVersions.Encode(p.RowVersion));
}
