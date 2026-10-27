using CongDoan.Domain.Cms;
using CongDoan.Application.Shared;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public sealed record PublicMenuItem(Guid Id, string Label, string Href, bool OpenInNewTab, IReadOnlyList<PublicMenuItem> Children);

public sealed record PublicMenu(string Code, string Name, IReadOnlyList<PublicMenuItem> Items);

public sealed record ResolvedTarget(string Type, string? Slug, string? Url);

public sealed record Resolved(string Type, int Status, string? Location, string? Slug, string? Title, ResolvedTarget? Target,
    IReadOnlyList<PublicMenuItem>? Children);

public sealed record SitemapEntry(string Path, DateTime? LastModifiedUtc);

/// <summary>Public navigation and URL resolution (menus, redirects, page lookup).</summary>
public sealed class PublicSiteService
{
    private readonly ICmsDbContext _db;
    private readonly RedirectService _redirects;
    private readonly PublicPostService _posts;

    public PublicSiteService(ICmsDbContext db, RedirectService redirects, PublicPostService posts)
    {
        _db = db;
        _redirects = redirects;
        _posts = posts;
    }

    public async Task<IReadOnlyList<PublicMenu>> MenusAsync(CancellationToken ct = default)
    {
        var menus = await _db.Menus.AsNoTracking().ToListAsync(ct);
        var items = await _db.MenuItems.AsNoTracking().Where(i => i.IsVisible && !i.IsDeleted).ToListAsync(ct);
        var targets = await TargetSlugsAsync(items, ct);
        return menus.Select(m => new PublicMenu(m.Code, m.Name, Build(items.Where(i => i.MenuId == m.Id).ToList(), null, targets))).ToList();
    }

    /// <summary>Every publicly visible content URL (canonical paths only; menu aliases are left out).</summary>
    public async Task<IReadOnlyList<SitemapEntry>> SitemapAsync(CancellationToken ct = default)
    {
        var entries = new List<SitemapEntry>();
        entries.AddRange((await _posts.Visible().Select(p => new { p.Slug, p.UpdatedUtc }).ToListAsync(ct))
            .Select(p => new SitemapEntry(PublicPaths.For("post", p.Slug), p.UpdatedUtc)));
        entries.AddRange((await _db.Categories.AsNoTracking().Where(c => c.IsVisible && !c.IsDeleted).Select(c => c.Slug).ToListAsync(ct))
            .Select(slug => new SitemapEntry(PublicPaths.For("category", slug), null)));
        entries.AddRange((await _db.Pages.AsNoTracking().Where(x => x.IsVisible && !x.IsDeleted).Select(x => new { x.Slug, x.UpdatedUtc }).ToListAsync(ct))
            .Select(x => new SitemapEntry(PublicPaths.For("page", x.Slug), x.UpdatedUtc)));
        entries.AddRange((await _db.Documents.AsNoTracking().Where(x => x.IsVisible && !x.IsDeleted).Select(x => new { x.Slug, x.UpdatedUtc }).ToListAsync(ct))
            .Select(x => new SitemapEntry(PublicPaths.For("document", x.Slug), x.UpdatedUtc)));
        entries.AddRange((await _db.Albums.AsNoTracking().Where(x => x.IsVisible && !x.IsDeleted).Select(x => new { x.Slug, x.UpdatedUtc }).ToListAsync(ct))
            .Select(x => new SitemapEntry(PublicPaths.For("album", x.Slug), x.UpdatedUtc)));
        return entries;
    }

    public async Task<PageDto?> PageAsync(string slug, CancellationToken ct = default)
    {
        var p = await _db.Pages.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug && x.IsVisible && !x.IsDeleted, ct);
        return p is null ? null : PageService.ToDto(p);
    }

    /// <summary>What a public path shows: content type + slug, a 301 redirect, or not found.</summary>
    public async Task<Resolved> ResolveAsync(string path, CancellationToken ct = default)
    {
        var p = PublicPaths.Normalize(path);
        var segments = p.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        Resolved? found = segments.Length switch
        {
            1 => await MenuAsync(segments[0], ct),
            2 => segments[0] switch
            {
                "tin-tuc" => await _posts.Visible().AnyAsync(x => x.Slug == segments[1], ct) ? Hit("post", segments[1]) : null,
                "chuyen-muc" => await _db.Categories.AnyAsync(c => c.Slug == segments[1] && c.IsVisible && !c.IsDeleted, ct) ? Hit("category", segments[1]) : null,
                "trang" => await _db.Pages.AnyAsync(x => x.Slug == segments[1] && x.IsVisible && !x.IsDeleted, ct) ? Hit("page", segments[1]) : null,
                "van-ban" => await _db.Documents.AnyAsync(x => x.Slug == segments[1] && x.IsVisible && !x.IsDeleted, ct) ? Hit("document", segments[1]) : null,
                "thu-vien" => await _db.Albums.AnyAsync(x => x.Slug == segments[1] && x.IsVisible && !x.IsDeleted, ct) ? Hit("album", segments[1]) : null,
                _ => null,
            },
            _ => null,
        };
        if (found is not null) return found;
        var location = await _redirects.FindAsync(p, ct);
        return location is not null
            ? new Resolved("redirect", 301, location, null, null, null, null)
            : new Resolved("not_found", 404, null, null, null, null, null);
    }

    private async Task<Resolved?> MenuAsync(string slug, CancellationToken ct)
    {
        var item = await _db.MenuItems.AsNoTracking().SingleOrDefaultAsync(i => i.Slug == slug && i.IsVisible && !i.IsDeleted, ct);
        if (item is null) return null;
        var siblings = await _db.MenuItems.AsNoTracking().Where(i => i.MenuId == item.MenuId && i.IsVisible && !i.IsDeleted).ToListAsync(ct);
        var targets = await TargetSlugsAsync(siblings, ct);
        var target = item.LinkType switch
        {
            MenuLinkType.Category => new ResolvedTarget("category", targets.GetValueOrDefault(item.Id), null),
            MenuLinkType.Page => new ResolvedTarget("page", targets.GetValueOrDefault(item.Id), null),
            MenuLinkType.Post => new ResolvedTarget("post", targets.GetValueOrDefault(item.Id), null),
            MenuLinkType.Url => new ResolvedTarget("url", null, item.Url),
            _ => new ResolvedTarget("section", null, null),
        };
        return new Resolved("menu", 200, null, item.Slug, item.Label, target, Build(siblings, item.Id, targets));
    }

    private static Resolved Hit(string type, string slug) => new(type, 200, null, slug, null, null, null);

    private async Task<Dictionary<Guid, string>> TargetSlugsAsync(IReadOnlyCollection<MenuItem> items, CancellationToken ct)
    {
        var ids = items.Where(i => i.TargetId != null).Select(i => i.TargetId!.Value).Distinct().ToList();
        var cats = await _db.Categories.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Slug, ct);
        var pages = await _db.Pages.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Slug, ct);
        var posts = await _db.Posts.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Slug, ct);
        var result = new Dictionary<Guid, string>();
        foreach (var i in items.Where(i => i.TargetId != null))
        {
            var t = i.TargetId!.Value;
            var slug = i.LinkType switch
            {
                MenuLinkType.Category => cats.GetValueOrDefault(t),
                MenuLinkType.Page => pages.GetValueOrDefault(t),
                MenuLinkType.Post => posts.GetValueOrDefault(t),
                _ => null,
            };
            if (slug is not null) result[i.Id] = slug;
        }
        return result;
    }

    private static List<PublicMenuItem> Build(IReadOnlyCollection<MenuItem> items, Guid? parent, IReadOnlyDictionary<Guid, string> targets) =>
        items.Where(i => i.ParentId == parent).OrderBy(i => i.SortOrder).ThenBy(i => i.Label)
            .Select(i => new PublicMenuItem(i.Id, i.Label, Href(i, targets), i.OpenInNewTab, Build(items, i.Id, targets)))
            .ToList();

    /// <summary>Menu items always live at "/{slug}" (so renames redirect); URL items point straight to their URL.</summary>
    private static string Href(MenuItem i, IReadOnlyDictionary<Guid, string> targets) =>
        i.LinkType == MenuLinkType.Url ? i.Url ?? "/" : PublicPaths.For("menu", i.Slug);
}

public sealed class CmsSeeder
{
    private readonly ICmsDbContext _db;

    public CmsSeeder(ICmsDbContext db) => _db = db;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existing = await _db.Menus.Select(m => m.Code).ToListAsync(ct);
        foreach (var (code, name) in new[] { ("main", "Menu chính"), ("utility", "Thanh tiện ích"), ("footer", "Menu chân trang") })
            if (!existing.Contains(code)) _db.Menus.Add(new Menu(code, name));
        await _db.SaveChangesAsync(ct);
    }
}
