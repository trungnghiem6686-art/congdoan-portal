using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Cms;
using CongDoan.Application.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Cms;

[Route("api/admin/posts")]
public sealed class AdminPostsController : ApiControllerBase
{
    private readonly PostService _svc;

    public AdminPostsController(PostService svc) => _svc = svc;

    [HttpGet]
    [RequirePermission(Permissions.CmsPostEdit)]
    public async Task<IActionResult> List([FromQuery] PostFilter f, CancellationToken ct) => Envelope(await _svc.ListAsync(f, ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CmsPostEdit)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Envelope(await _svc.GetAsync(id, ct));

    [HttpPost]
    [RequirePermission(Permissions.CmsPostCreate)]
    public async Task<IActionResult> Create([FromBody] SavePost cmd, CancellationToken ct) => Created(await _svc.CreateAsync(cmd, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CmsPostEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SavePost cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));

    [HttpPost("{id:guid}/transition")]
    [RequirePermission(Permissions.CmsPostEdit)]
    public async Task<IActionResult> Transition(Guid id, [FromBody] TransitionPost cmd, CancellationToken ct) => Envelope(await _svc.TransitionAsync(id, cmd, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.CmsPostDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, false, ct); return Envelope(new { deleted = true }); }

    [HttpPost("{id:guid}/restore")]
    [RequirePermission(Permissions.CmsPostDelete)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, true, ct); return Envelope(new { restored = true }); }
}

[Route("api/admin/categories")]
public sealed class AdminCategoriesController : ApiControllerBase
{
    private readonly CategoryService _svc;

    public AdminCategoriesController(CategoryService svc) => _svc = svc;

    [HttpGet]
    [RequirePermission(Permissions.CmsPostEdit)]
    public async Task<IActionResult> List([FromQuery] bool deleted, CancellationToken ct) => Envelope(await _svc.ListAsync(deleted, ct));

    [HttpGet("/api/admin/tags")]
    [RequirePermission(Permissions.CmsPostEdit)]
    public async Task<IActionResult> Tags(CancellationToken ct) => Envelope(await _svc.TagsAsync(ct));

    [HttpPost]
    [RequirePermission(Permissions.CmsCategoryManage)]
    public async Task<IActionResult> Create([FromBody] SaveCategory cmd, CancellationToken ct) => Created(await _svc.CreateAsync(cmd, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CmsCategoryManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCategory cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.CmsCategoryManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, true, ct); return Envelope(new { deleted = true }); }

    [HttpPost("{id:guid}/restore")]
    [RequirePermission(Permissions.CmsCategoryManage)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, false, ct); return Envelope(new { restored = true }); }
}

[Route("api/public/posts")]
public sealed class PublicPostsController : ApiControllerBase
{
    private readonly PublicPostService _svc;

    public PublicPostsController(PublicPostService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PublicPostQuery q, CancellationToken ct) => Envelope(await _svc.ListAsync(q, ct));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
    {
        var post = await _svc.GetAsync(slug, ct);
        return post is null ? NotFoundEnvelope() : Envelope(post);
    }
}
