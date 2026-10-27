using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public sealed record MenuDto(Guid Id, string Code, string Name);

public sealed record SaveMenuItem(string Label, string? Slug, string LinkType, Guid? TargetId, string? Url, Guid? ParentId,
    bool OpenInNewTab = false, bool IsVisible = true, int? SortOrder = null, string? RowVersion = null);

public sealed record MenuItemDto(Guid Id, Guid MenuId, Guid? ParentId, string Label, string Slug, bool SlugIsManual, string Path,
    string LinkType, Guid? TargetId, string? Url, bool OpenInNewTab, int SortOrder, bool IsVisible, bool IsDeleted, string RowVersion,
    IReadOnlyList<MenuItemDto> Children);

public sealed record ReorderItem(Guid Id, Guid? ParentId, int SortOrder);

public sealed record ReorderRequest(IReadOnlyList<ReorderItem> Items);

public sealed class MenuService
{
    private readonly ICmsDbContext _db;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly RedirectService _redirects;

    public MenuService(ICmsDbContext db, IAuditWriter audit, IClock clock, RedirectService redirects)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _redirects = redirects;
    }

    public async Task<IReadOnlyList<MenuDto>> MenusAsync(CancellationToken ct = default) =>
        await _db.Menus.AsNoTracking().OrderBy(m => m.Code).Select(m => new MenuDto(m.Id, m.Code, m.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<MenuItemDto>> TreeAsync(Guid menuId, bool deleted = false, CancellationToken ct = default)
    {
        var items = await _db.MenuItems.AsNoTracking().Where(i => i.MenuId == menuId && i.IsDeleted == deleted).ToListAsync(ct);
        return deleted ? items.Select(i => ToDto(i, Array.Empty<MenuItemDto>())).ToList() : Build(items, null);
    }

    public async Task<MenuItemDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await _db.MenuItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw AppException.NotFound(), Array.Empty<MenuItemDto>());

    public async Task<MenuItemDto> CreateAsync(Guid menuId, SaveMenuItem cmd, CancellationToken ct = default)
    {
        if (!await _db.Menus.AnyAsync(m => m.Id == menuId, ct)) throw AppException.NotFound();
        var item = new MenuItem { MenuId = menuId };
        var (slug, manual) = SlugRules.Resolve(cmd.Slug, cmd.Label);
        await EnsureSlugFreeAsync(slug, null, ct);
        await ApplyAsync(item, cmd, ct);
        item.Slug = slug;
        item.SlugIsManual = manual;
        item.SortOrder = cmd.SortOrder ?? await _db.MenuItems.Where(i => i.MenuId == menuId && i.ParentId == cmd.ParentId).CountAsync(ct);
        CmsItemOps.Touch(item, _clock, created: true);
        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("cms.menuitem.create", "MenuItem", item.Id.ToString(), new { item.Label, item.Slug }, ct);
        return ToDto(item, Array.Empty<MenuItemDto>());
    }

    public async Task<MenuItemDto> UpdateAsync(Guid id, SaveMenuItem cmd, CancellationToken ct = default)
    {
        var item = await _db.MenuItems.LoadAsync(id, ct);
        if (cmd.RowVersion is not null) _db.ExpectRowVersion(item, RowVersions.Decode(cmd.RowVersion));
        var oldSlug = item.Slug;
        var (slug, manual) = string.IsNullOrWhiteSpace(cmd.Slug) && item.SlugIsManual ? (item.Slug, true) : SlugRules.Resolve(cmd.Slug, cmd.Label);
        if (slug != item.Slug) await EnsureSlugFreeAsync(slug, id, ct);
        await EnsureNoCycleAsync(item, cmd.ParentId, ct);
        await ApplyAsync(item, cmd, ct);
        if (cmd.SortOrder is { } order) item.SortOrder = order;
        item.Slug = slug;
        item.SlugIsManual = manual;
        CmsItemOps.Touch(item, _clock);
        await _db.SaveChangesAsync(ct);
        if (oldSlug != slug) await _redirects.RecordAsync("menu", oldSlug, slug, ct);
        await _audit.WriteAsync("cms.menuitem.update", "MenuItem", id.ToString(), new { item.Label, item.Slug }, ct);
        return ToDto(item, Array.Empty<MenuItemDto>());
    }

    public async Task ReorderAsync(Guid menuId, ReorderRequest req, CancellationToken ct = default)
    {
        var items = await _db.MenuItems.Where(i => i.MenuId == menuId && !i.IsDeleted).ToListAsync(ct);
        var byId = items.ToDictionary(i => i.Id);
        foreach (var r in req.Items ?? Array.Empty<ReorderItem>())
        {
            if (!byId.TryGetValue(r.Id, out var item)) throw AppException.Validation("Mục menu không thuộc menu này.");
            if (r.ParentId is { } p && !byId.ContainsKey(p)) throw AppException.Validation("Mục cha không thuộc menu này.");
            item.ParentId = r.ParentId;
            item.SortOrder = r.SortOrder;
        }
        foreach (var item in items) EnsureNoCycle(item.Id, item.ParentId, items.ToDictionary(i => i.Id, i => i.ParentId));
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("cms.menu.reorder", "Menu", menuId.ToString(), new { count = req.Items?.Count ?? 0 }, ct);
    }

    public Task SetVisibleAsync(Guid id, bool visible, CancellationToken ct = default) =>
        CmsItemOps.SetVisibleAsync(_db, _db.MenuItems, _audit, id, visible, ct);

    public Task SetDeletedAsync(Guid id, bool deleted, CancellationToken ct = default) =>
        CmsItemOps.SetDeletedAsync(_db, _db.MenuItems, _audit, _clock, id, deleted, ct);

    private async Task ApplyAsync(MenuItem item, SaveMenuItem cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Label)) throw AppException.Validation("Tên mục menu là bắt buộc.");
        if (!Enum.TryParse<MenuLinkType>(cmd.LinkType, true, out var type)) throw AppException.Validation("Loại liên kết không hợp lệ.");
        item.Label = cmd.Label.Trim();
        item.LinkType = type;
        item.OpenInNewTab = cmd.OpenInNewTab;
        item.IsVisible = cmd.IsVisible;
        item.Url = null;
        item.TargetId = null;
        switch (type)
        {
            case MenuLinkType.Url:
                CmsItemOps.EnsureSafeUrl(cmd.Url, required: true);
                item.Url = cmd.Url!.Trim();
                break;
            case MenuLinkType.Category:
                if (cmd.TargetId is not { } cat || !await _db.Categories.AnyAsync(c => c.Id == cat && !c.IsDeleted, ct))
                    throw AppException.Validation("Chọn chuyên mục cho mục menu.");
                item.TargetId = cat;
                break;
            case MenuLinkType.Page:
                if (cmd.TargetId is not { } page || !await _db.Pages.AnyAsync(p => p.Id == page && !p.IsDeleted, ct))
                    throw AppException.Validation("Chọn trang cho mục menu.");
                item.TargetId = page;
                break;
            case MenuLinkType.Post:
                if (cmd.TargetId is not { } post || !await _db.Posts.AnyAsync(p => p.Id == post && !p.IsDeleted, ct))
                    throw AppException.Validation("Chọn bài viết cho mục menu.");
                item.TargetId = post;
                break;
        }
        if (cmd.ParentId is { } parent && !await _db.MenuItems.AnyAsync(i => i.Id == parent && i.MenuId == item.MenuId && !i.IsDeleted, ct))
            throw AppException.Validation("Mục cha không tồn tại trong menu này.");
        item.ParentId = cmd.ParentId;
    }

    private async Task EnsureNoCycleAsync(MenuItem item, Guid? newParent, CancellationToken ct)
    {
        var parents = await _db.MenuItems.AsNoTracking().Where(i => i.MenuId == item.MenuId).ToDictionaryAsync(i => i.Id, i => i.ParentId, ct);
        EnsureNoCycle(item.Id, newParent, parents);
    }

    private static void EnsureNoCycle(Guid id, Guid? parent, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        var seen = new HashSet<Guid> { id };
        for (var p = parent; p is not null; p = parents.GetValueOrDefault(p.Value))
            if (!seen.Add(p.Value)) throw new AppException("menu_cycle", "Không thể đặt mục menu làm con của chính nó hoặc của mục con của nó.", 400);
    }

    private async Task EnsureSlugFreeAsync(string slug, Guid? self, CancellationToken ct)
    {
        async Task<bool> Taken(string s) => PublicPaths.Reserved.Contains(s) || await _db.MenuItems.AnyAsync(i => i.Slug == s && i.Id != self, ct);
        if (await Taken(slug)) throw await SlugRules.DuplicateAsync(slug, Taken);
    }

    private static List<MenuItemDto> Build(IReadOnlyCollection<MenuItem> items, Guid? parent) =>
        items.Where(i => i.ParentId == parent).OrderBy(i => i.SortOrder).ThenBy(i => i.Label)
            .Select(i => ToDto(i, Build(items, i.Id))).ToList();

    private static MenuItemDto ToDto(MenuItem i, IReadOnlyList<MenuItemDto> children) =>
        new(i.Id, i.MenuId, i.ParentId, i.Label, i.Slug, i.SlugIsManual, PublicPaths.For("menu", i.Slug), i.LinkType.ToString(), i.TargetId,
            i.Url, i.OpenInNewTab, i.SortOrder, i.IsVisible, i.IsDeleted, RowVersions.Encode(i.RowVersion), children);
}
