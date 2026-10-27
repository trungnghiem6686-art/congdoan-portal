using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Cms;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CongDoan.Api.Cms;

[Route("api/public")]
public sealed class PublicContentController : ApiControllerBase
{
    private readonly PublicContentService _content;
    private readonly SearchService _search;
    private readonly ContactService _contacts;

    public PublicContentController(PublicContentService content, SearchService search, ContactService contacts)
    {
        _content = content;
        _search = search;
        _contacts = contacts;
    }

    [HttpGet("categories")] public async Task<IActionResult> Categories(CancellationToken ct) => Envelope(await _content.CategoriesAsync(ct));
    [HttpGet("documents")] public async Task<IActionResult> Documents([FromQuery] DocumentQuery query, CancellationToken ct) => Envelope(await _content.DocumentsAsync(query, ct));
    [HttpGet("document-types")] public async Task<IActionResult> DocumentTypes(CancellationToken ct) => Envelope(await _content.DocumentTypesAsync(ct));

    [HttpGet("documents/{slug}")]
    public async Task<IActionResult> Document(string slug, CancellationToken ct) => await _content.DocumentAsync(slug, ct) is { } d ? Envelope(d) : NotFoundEnvelope();

    [HttpGet("albums")]
    public async Task<IActionResult> Albums([FromQuery] int page = 1, [FromQuery] int pageSize = 12, [FromQuery] string? kind = null, CancellationToken ct = default) =>
        Envelope(await _content.AlbumsAsync(page, pageSize, kind, ct));

    [HttpGet("albums/{slug}")]
    public async Task<IActionResult> Album(string slug, CancellationToken ct) => await _content.AlbumAsync(slug, ct) is { } a ? Envelope(a) : NotFoundEnvelope();

    [HttpGet("search")] public async Task<IActionResult> Search([FromQuery] SearchQuery query, CancellationToken ct) => Envelope(await _search.SearchAsync(query, ct));

    [HttpPost("contact")]
    [EnableRateLimiting(AuthSchemes.ContactRateLimit)]
    public async Task<IActionResult> Contact([FromBody] SubmitContact cmd, CancellationToken ct) => Envelope(new { id = await _contacts.SubmitAsync(cmd, ct) });
}

[Route("api/admin/contacts")]
[RequirePermission(Permissions.CmsContactView)]
public sealed class AdminContactsController : ApiControllerBase
{
    private readonly ContactService _svc;
    public AdminContactsController(ContactService svc) => _svc = svc;

    [HttpGet] public async Task<IActionResult> List([FromQuery] PageQuery query, [FromQuery] string? status, CancellationToken ct) => Envelope(await _svc.ListAsync(query, status, ct));
    [HttpPost("{id:guid}/handle")] public async Task<IActionResult> Handle(Guid id, [FromBody] HandleContact cmd, CancellationToken ct) { await _svc.HandleAsync(id, cmd, ct); return Envelope(new { handled = true }); }
}
