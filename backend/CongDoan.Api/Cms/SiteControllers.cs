using System.Text.Json;
using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Cms;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Cms;

[Route("api/admin/menus")]
[RequirePermission(Permissions.CmsMenuManage)]
public sealed class AdminMenusController : ApiControllerBase
{
    private readonly MenuService _svc;

    public AdminMenusController(MenuService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Menus(CancellationToken ct) => Envelope(await _svc.MenusAsync(ct));

    [HttpGet("{menuId:guid}/items")]
    public async Task<IActionResult> Items(Guid menuId, [FromQuery] bool deleted, CancellationToken ct) => Envelope(await _svc.TreeAsync(menuId, deleted, ct));

    [HttpPost("{menuId:guid}/items")]
    public async Task<IActionResult> Create(Guid menuId, [FromBody] SaveMenuItem cmd, CancellationToken ct) => Created(await _svc.CreateAsync(menuId, cmd, ct));

    [HttpPost("{menuId:guid}/reorder")]
    public async Task<IActionResult> Reorder(Guid menuId, [FromBody] ReorderRequest req, CancellationToken ct) { await _svc.ReorderAsync(menuId, req, ct); return Envelope(new { reordered = true }); }

    [HttpGet("/api/admin/menu-items/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Envelope(await _svc.GetAsync(id, ct));

    [HttpPut("/api/admin/menu-items/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveMenuItem cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));

    [HttpPost("/api/admin/menu-items/{id:guid}/hide")]
    public async Task<IActionResult> Hide(Guid id, CancellationToken ct) { await _svc.SetVisibleAsync(id, false, ct); return Envelope(new { visible = false }); }

    [HttpPost("/api/admin/menu-items/{id:guid}/show")]
    public async Task<IActionResult> Show(Guid id, CancellationToken ct) { await _svc.SetVisibleAsync(id, true, ct); return Envelope(new { visible = true }); }

    [HttpDelete("/api/admin/menu-items/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, true, ct); return Envelope(new { deleted = true }); }

    [HttpPost("/api/admin/menu-items/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, false, ct); return Envelope(new { restored = true }); }
}

[Route("api/admin/banners")]
[RequirePermission(Permissions.CmsBannerManage)]
public sealed class AdminBannersController : ApiControllerBase
{
    private readonly BannerService _svc;
    public AdminBannersController(BannerService svc) => _svc = svc;

    [HttpGet] public async Task<IActionResult> List([FromQuery] ItemQuery q, [FromQuery] string? position, CancellationToken ct) => Envelope(await _svc.ListAsync(q, position, ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Envelope(await _svc.GetAsync(id, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] SaveBanner cmd, CancellationToken ct) => Created(await _svc.CreateAsync(cmd, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] SaveBanner cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));
    [HttpPost("{id:guid}/hide")] public async Task<IActionResult> Hide(Guid id, CancellationToken ct) { await _svc.SetVisibleAsync(id, false, ct); return Envelope(new { visible = false }); }
    [HttpPost("{id:guid}/show")] public async Task<IActionResult> Show(Guid id, CancellationToken ct) { await _svc.SetVisibleAsync(id, true, ct); return Envelope(new { visible = true }); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, true, ct); return Envelope(new { deleted = true }); }
    [HttpPost("{id:guid}/restore")] public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, false, ct); return Envelope(new { restored = true }); }
}

[Route("api/admin/documents")]
[RequirePermission(Permissions.CmsDocumentManage)]
public sealed class AdminDocumentsController : ApiControllerBase
{
    private readonly DocumentService _svc;
    public AdminDocumentsController(DocumentService svc) => _svc = svc;

    [HttpGet] public async Task<IActionResult> List([FromQuery] ItemQuery q, CancellationToken ct) => Envelope(await _svc.ListAsync(q, ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Envelope(await _svc.GetAsync(id, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] SaveDocument cmd, CancellationToken ct) => Created(await _svc.CreateAsync(cmd, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] SaveDocument cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));
    [HttpPost("{id:guid}/hide")] public async Task<IActionResult> Hide(Guid id, CancellationToken ct) { await _svc.SetVisibleAsync(id, false, ct); return Envelope(new { visible = false }); }
    [HttpPost("{id:guid}/show")] public async Task<IActionResult> Show(Guid id, CancellationToken ct) { await _svc.SetVisibleAsync(id, true, ct); return Envelope(new { visible = true }); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, true, ct); return Envelope(new { deleted = true }); }
    [HttpPost("{id:guid}/restore")] public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, false, ct); return Envelope(new { restored = true }); }
}

public sealed record ReorderIdsRequest(IReadOnlyList<Guid> Ids);

[Route("api/admin/albums")]
[RequirePermission(Permissions.CmsAlbumManage)]
public sealed class AdminAlbumsController : ApiControllerBase
{
    private readonly AlbumService _svc;
    public AdminAlbumsController(AlbumService svc) => _svc = svc;

    [HttpGet] public async Task<IActionResult> List([FromQuery] ItemQuery q, CancellationToken ct) => Envelope(await _svc.ListAsync(q, ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Envelope(await _svc.GetAsync(id, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] SaveAlbum cmd, CancellationToken ct) => Created(await _svc.CreateAsync(cmd, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] SaveAlbum cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));
    [HttpPost("{id:guid}/items")] public async Task<IActionResult> AddItem(Guid id, [FromBody] SaveAlbumItem cmd, CancellationToken ct) => Created(await _svc.AddItemAsync(id, cmd, ct));
    [HttpDelete("{id:guid}/items/{itemId:guid}")] public async Task<IActionResult> RemoveItem(Guid id, Guid itemId, CancellationToken ct) { await _svc.RemoveItemAsync(id, itemId, ct); return Envelope(new { removed = true }); }
    [HttpPost("{id:guid}/items/reorder")] public async Task<IActionResult> ReorderItems(Guid id, [FromBody] ReorderIdsRequest req, CancellationToken ct) { await _svc.ReorderItemsAsync(id, req.Ids ?? Array.Empty<Guid>(), ct); return Envelope(new { reordered = true }); }
    [HttpPost("{id:guid}/hide")] public async Task<IActionResult> Hide(Guid id, CancellationToken ct) { await _svc.SetVisibleAsync(id, false, ct); return Envelope(new { visible = false }); }
    [HttpPost("{id:guid}/show")] public async Task<IActionResult> Show(Guid id, CancellationToken ct) { await _svc.SetVisibleAsync(id, true, ct); return Envelope(new { visible = true }); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, true, ct); return Envelope(new { deleted = true }); }
    [HttpPost("{id:guid}/restore")] public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, false, ct); return Envelope(new { restored = true }); }
}

[Route("api/admin/pages")]
[RequirePermission(Permissions.CmsPageManage)]
public sealed class AdminPagesController : ApiControllerBase
{
    private readonly PageService _svc;
    public AdminPagesController(PageService svc) => _svc = svc;

    [HttpGet] public async Task<IActionResult> List([FromQuery] ItemQuery q, CancellationToken ct) => Envelope(await _svc.ListAsync(q, ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Envelope(await _svc.GetAsync(id, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] SavePage cmd, CancellationToken ct) => Created(await _svc.CreateAsync(cmd, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] SavePage cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));
    [HttpPost("{id:guid}/hide")] public async Task<IActionResult> Hide(Guid id, CancellationToken ct) { await _svc.SetVisibleAsync(id, false, ct); return Envelope(new { visible = false }); }
    [HttpPost("{id:guid}/show")] public async Task<IActionResult> Show(Guid id, CancellationToken ct) { await _svc.SetVisibleAsync(id, true, ct); return Envelope(new { visible = true }); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, true, ct); return Envelope(new { deleted = true }); }
    [HttpPost("{id:guid}/restore")] public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, false, ct); return Envelope(new { restored = true }); }
}

[Route("api/admin/settings")]
[RequirePermission(Permissions.CmsSettingsManage)]
public sealed class AdminSettingsController : ApiControllerBase
{
    private readonly SettingsService _svc;
    public AdminSettingsController(SettingsService svc) => _svc = svc;

    [HttpGet("{key}")] public async Task<IActionResult> Get(string key, CancellationToken ct) => Envelope(await _svc.GetAsync(key, ct));
    [HttpPut("{key}")] public async Task<IActionResult> Save(string key, [FromBody] JsonElement value, CancellationToken ct) => Envelope(await _svc.SaveAsync(key, value, ct));
}

[Route("api/admin/redirects")]
[RequirePermission(Permissions.CmsMenuManage)]
public sealed class AdminRedirectsController : ApiControllerBase
{
    private readonly RedirectService _svc;
    public AdminRedirectsController(RedirectService svc) => _svc = svc;

    public sealed record AddRedirect(string OldPath, string NewPath);

    [HttpGet] public async Task<IActionResult> List([FromQuery] PageQuery q, CancellationToken ct) => Envelope(await _svc.ListAsync(q, ct));
    [HttpPost] public async Task<IActionResult> Add([FromBody] AddRedirect req, CancellationToken ct)
    {
        CmsItemOps.EnsureSafeUrl(req.NewPath, required: true);
        if (!req.OldPath.StartsWith('/')) throw AppException.Validation("Đường dẫn cũ phải bắt đầu bằng /.");
        await _svc.AddAsync(req.OldPath, req.NewPath, ct);
        return Envelope(new { added = true });
    }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, ct); return Envelope(new { deleted = true }); }
}

[Route("api/public")]
public sealed class PublicSiteController : ApiControllerBase
{
    private readonly PublicSiteService _site;
    private readonly SettingsService _settings;
    private readonly BannerService _banners;

    public PublicSiteController(PublicSiteService site, SettingsService settings, BannerService banners)
    {
        _site = site;
        _settings = settings;
        _banners = banners;
    }

    [HttpGet("menus")] public async Task<IActionResult> Menus(CancellationToken ct) => Envelope(await _site.MenusAsync(ct));
    [HttpGet("site")] public async Task<IActionResult> Site(CancellationToken ct) => Envelope(await _settings.PublicAsync(ct));
    [HttpGet("banners")] public async Task<IActionResult> Banners([FromQuery] string? position, CancellationToken ct) => Envelope(await _banners.LiveAsync(position, ct));
    [HttpGet("resolve")] public async Task<IActionResult> Resolve([FromQuery] string path, CancellationToken ct) => Envelope(await _site.ResolveAsync(path, ct));

    [HttpGet("pages/{slug}")]
    public async Task<IActionResult> Page(string slug, CancellationToken ct) =>
        await _site.PageAsync(slug, ct) is { } p ? Envelope(p) : NotFoundEnvelope();
}
