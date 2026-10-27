using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Cms;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Cms;

[Route("api/admin/media")]
public sealed class AdminMediaController : ApiControllerBase
{
    private readonly MediaService _svc;

    public AdminMediaController(MediaService svc) => _svc = svc;

    [HttpGet]
    [RequirePermission(Permissions.CmsMediaUpload)]
    public async Task<IActionResult> List([FromQuery] PageQuery query, [FromQuery] string? kind, [FromQuery] bool deleted, CancellationToken ct) =>
        Envelope(await _svc.ListAsync(query, kind, deleted, ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CmsMediaUpload)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Envelope(await _svc.GetAsync(id, ct));

    /// <summary>Multipart upload (field "file", optional "visibility") for images and documents.</summary>
    [HttpPost]
    [RequirePermission(Permissions.CmsMediaUpload)]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string? visibility, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw AppException.Validation("Chưa chọn tệp.");
        await using var stream = file.OpenReadStream();
        return Created(await _svc.UploadAsync(stream, file.FileName, file.Length, visibility, ct));
    }

    [HttpPost("uploads")]
    [RequirePermission(Permissions.CmsMediaUpload)]
    public async Task<IActionResult> Start([FromBody] StartUpload cmd, CancellationToken ct) => Created(await _svc.StartUploadAsync(cmd, ct));

    [HttpPut("uploads/{id:guid}/chunks/{index:int}")]
    [RequirePermission(Permissions.CmsMediaUpload)]
    [RequestSizeLimit(64 * 1024 * 1024)]
    public async Task<IActionResult> Chunk(Guid id, int index, CancellationToken ct)
    {
        await _svc.PutChunkAsync(id, index, Request.Body, ct);
        return Envelope(new { received = index });
    }

    [HttpPost("uploads/{id:guid}/complete")]
    [RequirePermission(Permissions.CmsMediaUpload)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct) => Envelope(await _svc.CompleteUploadAsync(id, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CmsMediaUpload)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMedia cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));

    [HttpGet("{id:guid}/usages")]
    [RequirePermission(Permissions.CmsMediaUpload)]
    public async Task<IActionResult> Usages(Guid id, CancellationToken ct) => Envelope(await _svc.UsagesAsync(id, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.CmsMediaManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, ct); return Envelope(new { deleted = true }); }

    [HttpPost("{id:guid}/restore")]
    [RequirePermission(Permissions.CmsMediaManage)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.RestoreAsync(id, ct); return Envelope(new { restored = true }); }
}

/// <summary>Public files: only published (non-deleted, public) assets; range requests for video.</summary>
[ApiController]
[Route("media")]
public sealed class PublicMediaController : ControllerBase
{
    private readonly MediaService _svc;

    public PublicMediaController(MediaService svc) => _svc = svc;

    [HttpGet("{id:guid}/thumb")]
    public async Task<IActionResult> Thumb(Guid id, CancellationToken ct) => Serve(await _svc.PublicFileAsync(id, true, ct));

    [HttpGet("{id:guid}/{name?}")]
    public async Task<IActionResult> File(Guid id, CancellationToken ct) => Serve(await _svc.PublicFileAsync(id, false, ct));

    private IActionResult Serve(MediaFile? f)
    {
        if (f is null || !System.IO.File.Exists(f.FullPath)) return NotFound();
        Response.Headers.CacheControl = "public, max-age=604800";
        return FileResults.Physical(this, f);
    }
}

[ApiController]
[Route("api/files")]
public sealed class RestrictedFilesController : ControllerBase
{
    private readonly MediaService _svc;

    public RestrictedFilesController(MediaService svc) => _svc = svc;

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CmsMediaManage)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var f = await _svc.RestrictedFileAsync(id, ct);
        if (f is null || !System.IO.File.Exists(f.FullPath)) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        return FileResults.Physical(this, f);
    }
}

internal static class FileResults
{
    public static IActionResult Physical(ControllerBase c, MediaFile f)
    {
        var result = c.PhysicalFile(f.FullPath, f.ContentType, enableRangeProcessing: true);
        if (!f.Inline) result.FileDownloadName = f.DownloadName;
        return result;
    }
}
