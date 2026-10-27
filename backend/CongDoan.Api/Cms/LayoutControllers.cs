using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Cms;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Cms;

[Route("api/admin/homepage")]
[RequirePermission(Permissions.CmsHomepageManage)]
public sealed class AdminHomepageController : ApiControllerBase
{
    private readonly LayoutService _layouts;
    private readonly SectionResolver _resolver;

    public AdminHomepageController(LayoutService layouts, SectionResolver resolver)
    {
        _layouts = layouts;
        _resolver = resolver;
    }

    [HttpGet] public async Task<IActionResult> State(CancellationToken ct) => Envelope(await _layouts.StateAsync(LayoutRevision.Homepage, ct));
    [HttpGet("block-types")] public IActionResult Types() => Envelope(SectionCatalog.All.Select(t => new { type = t.Key, label = t.Value.Label, layouts = t.Value.Layouts }));
    [HttpPut("draft")] public async Task<IActionResult> Save([FromBody] SaveLayout cmd, CancellationToken ct) => Envelope(await _layouts.SaveDraftAsync(LayoutRevision.Homepage, cmd, ct));
    [HttpPost("publish")] public async Task<IActionResult> Publish([FromBody] PublishLayout cmd, CancellationToken ct) => Envelope(await _layouts.PublishAsync(LayoutRevision.Homepage, cmd, ct));
    [HttpPost("rollback")] public async Task<IActionResult> Rollback([FromBody] RollbackLayout cmd, CancellationToken ct) => Envelope(await _layouts.RollbackAsync(LayoutRevision.Homepage, cmd, ct));

    /// <summary>Draft (or live if no draft) with real data; never cached, admin only.</summary>
    [HttpGet("preview")]
    public async Task<IActionResult> Preview(CancellationToken ct)
    {
        var (row, sections) = await _layouts.CurrentAsync(LayoutRevision.Homepage, draft: true, ct);
        Response.Headers.CacheControl = "no-store";
        return Envelope(new ResolvedLayout(row?.Version ?? 0, row?.PublishedUtc, await _resolver.ResolveAsync(sections, ct)));
    }
}

[Route("api/admin/pages/{pageId:guid}/layout")]
[RequirePermission(Permissions.CmsPageManage)]
public sealed class AdminPageLayoutController : ApiControllerBase
{
    private readonly LayoutService _layouts;
    private readonly SectionResolver _resolver;
    private readonly PageService _pages;

    public AdminPageLayoutController(LayoutService layouts, SectionResolver resolver, PageService pages)
    {
        _layouts = layouts;
        _resolver = resolver;
        _pages = pages;
    }

    private async Task<string> TargetAsync(Guid pageId, CancellationToken ct)
    {
        await _pages.GetAsync(pageId, ct);
        return LayoutRevision.ForPage(pageId);
    }

    [HttpGet] public async Task<IActionResult> State(Guid pageId, CancellationToken ct) => Envelope(await _layouts.StateAsync(await TargetAsync(pageId, ct), ct));
    [HttpPut("draft")] public async Task<IActionResult> Save(Guid pageId, [FromBody] SaveLayout cmd, CancellationToken ct) => Envelope(await _layouts.SaveDraftAsync(await TargetAsync(pageId, ct), cmd, ct));
    [HttpPost("publish")] public async Task<IActionResult> Publish(Guid pageId, [FromBody] PublishLayout cmd, CancellationToken ct) => Envelope(await _layouts.PublishAsync(await TargetAsync(pageId, ct), cmd, ct));
    [HttpPost("rollback")] public async Task<IActionResult> Rollback(Guid pageId, [FromBody] RollbackLayout cmd, CancellationToken ct) => Envelope(await _layouts.RollbackAsync(await TargetAsync(pageId, ct), cmd, ct));

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(Guid pageId, CancellationToken ct)
    {
        var (row, sections) = await _layouts.CurrentAsync(await TargetAsync(pageId, ct), draft: true, ct);
        Response.Headers.CacheControl = "no-store";
        return Envelope(new ResolvedLayout(row?.Version ?? 0, row?.PublishedUtc, await _resolver.ResolveAsync(sections, ct)));
    }
}

public sealed record PublicPageDto(Guid Id, string Title, string Slug, string Body, string? SeoDescription, DateTime UpdatedUtc,
    IReadOnlyList<ResolvedSection> Sections);

[Route("api/public")]
public sealed class PublicLayoutController : ApiControllerBase
{
    private readonly LayoutService _layouts;
    private readonly SectionResolver _resolver;
    private readonly PublicSiteService _site;

    public PublicLayoutController(LayoutService layouts, SectionResolver resolver, PublicSiteService site)
    {
        _layouts = layouts;
        _resolver = resolver;
        _site = site;
    }

    [HttpGet("homepage")]
    public async Task<IActionResult> Homepage(CancellationToken ct)
    {
        var (row, sections) = await _layouts.CurrentAsync(LayoutRevision.Homepage, draft: false, ct);
        return Envelope(new ResolvedLayout(row?.Version ?? 0, row?.PublishedUtc, await _resolver.ResolveAsync(sections, ct)));
    }

    [HttpGet("pages/{slug}")]
    public async Task<IActionResult> Page(string slug, CancellationToken ct)
    {
        var page = await _site.PageAsync(slug, ct);
        if (page is null) return NotFoundEnvelope();
        var sections = await _layouts.PublishedSectionsAsync(LayoutRevision.ForPage(page.Id), ct) ?? Array.Empty<Section>();
        return Envelope(new PublicPageDto(page.Id, page.Title, page.Slug, page.Body, page.SeoDescription, page.UpdatedUtc,
            await _resolver.ResolveAsync(sections, ct)));
    }
}
