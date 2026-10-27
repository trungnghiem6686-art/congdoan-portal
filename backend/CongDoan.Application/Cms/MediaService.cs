using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CongDoan.Application.Cms;

public sealed record MediaDto(Guid Id, string Kind, string OriginalName, string ContentType, long Size, int? Width, int? Height,
    string Visibility, string? Alt, string? Caption, string Url, string? ThumbnailUrl, DateTime CreatedUtc, bool IsDeleted);

public sealed record MediaUsageDto(string OwnerType, Guid OwnerId, string OwnerTitle);

public sealed record StartUpload(string FileName, long Size, string? Visibility);

public sealed record UploadStarted(Guid UploadId, int ChunkSize, int ChunkCount);

public sealed record UpdateMedia(string? Alt, string? Caption);

public sealed record MediaFile(string FullPath, string ContentType, string DownloadName, bool Inline);

public interface IMediaUsageTracker
{
    /// <summary>Replaces the set of media an owner (post, banner...) references.</summary>
    Task SetUsagesAsync(string ownerType, Guid ownerId, string ownerTitle, IEnumerable<Guid> mediaIds, CancellationToken ct = default);
}

public sealed class MediaService : IMediaUsageTracker
{
    private const string TempRoot = "_uploads";
    private readonly ICmsDbContext _db;
    private readonly IFileStore _files;
    private readonly IImageProcessor _images;
    private readonly MediaOptions _options;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public MediaService(ICmsDbContext db, IFileStore files, IImageProcessor images, IOptions<MediaOptions> options,
        ICurrentUser user, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _files = files;
        _images = images;
        _options = options.Value;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public static string PublicUrl(Guid id, string name) => $"/media/{id}/{SafeUrlName(name)}";

    public async Task<PagedResult<MediaDto>> ListAsync(PageQuery q, string? kind, bool deleted, CancellationToken ct = default)
    {
        var query = _db.MediaAssets.AsNoTracking().Where(m => m.IsDeleted == deleted);
        if (Enum.TryParse<MediaKind>(kind, true, out var k)) query = query.Where(m => m.Kind == k);
        if (!string.IsNullOrWhiteSpace(q.Q)) query = query.Where(m => m.OriginalName.Contains(q.Q) || (m.Caption != null && m.Caption.Contains(q.Q)));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(m => m.CreatedUtc).Skip(q.Skip).Take(q.SafeSize).ToListAsync(ct);
        return new PagedResult<MediaDto>(items.Select(ToDto).ToList(), total, q.SafePage, q.SafeSize);
    }

    public async Task<MediaDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await _db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct) ?? throw AppException.NotFound());

    public async Task<string> GetStoredPathAsync(Guid id, CancellationToken ct = default) =>
        (await _db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct) ?? throw AppException.NotFound()).StoredPath;

    /// <summary>Single-request upload for images and documents.</summary>
    public async Task<MediaDto> UploadAsync(Stream content, string fileName, long length, string? visibility, CancellationToken ct = default)
    {
        var name = CleanName(fileName);
        var kind = FileSniffer.KindOf(name) ?? throw Unsupported();
        EnsureWithinLimit(kind, length);
        var tempPath = $"{TempRoot}/{Guid.NewGuid():N}.bin";
        try
        {
            await _files.SaveAsync(tempPath, content, ct);
            EnsureWithinLimit(kind, _files.Length(tempPath));
            return await PromoteAsync(tempPath, name, ParseVisibility(visibility), ct);
        }
        finally
        {
            _files.Delete(tempPath);
        }
    }

    public async Task<UploadStarted> StartUploadAsync(StartUpload cmd, CancellationToken ct = default)
    {
        var name = CleanName(cmd.FileName);
        var kind = FileSniffer.KindOf(name) ?? throw Unsupported();
        if (cmd.Size <= 0) throw AppException.Validation("Kích thước tệp không hợp lệ.");
        EnsureWithinLimit(kind, cmd.Size);
        var upload = new MediaUpload(name, cmd.Size, _options.ChunkSize, ParseVisibility(cmd.Visibility), _user.AdminId, _clock.UtcNow);
        _db.MediaUploads.Add(upload);
        await _db.SaveChangesAsync(ct);
        return new UploadStarted(upload.Id, upload.ChunkSize, upload.ChunkCount);
    }

    public async Task PutChunkAsync(Guid uploadId, int index, Stream content, CancellationToken ct = default)
    {
        var upload = await LoadUploadAsync(uploadId, ct);
        if (index < 0 || index >= upload.ChunkCount) throw AppException.Validation("Số thứ tự phần tệp không hợp lệ.");
        var part = ChunkPath(upload.Id, index);
        await _files.SaveAsync(part, content, ct);
        var length = _files.Length(part);
        var expected = index == upload.ChunkCount - 1 ? upload.DeclaredSize - (long)index * upload.ChunkSize : upload.ChunkSize;
        if (length > expected)
        {
            _files.Delete(part);
            throw TooLarge(FileSniffer.KindOf(upload.FileName) ?? MediaKind.Document, "Phần tệp lớn hơn kích thước đã khai báo.");
        }
    }

    public async Task<MediaDto> CompleteUploadAsync(Guid uploadId, CancellationToken ct = default)
    {
        var upload = await LoadUploadAsync(uploadId, ct);
        var assembled = $"{TempRoot}/{upload.Id:N}/assembled.bin";
        try
        {
            await using (var output = _files.Create(assembled))
            {
                for (var i = 0; i < upload.ChunkCount; i++)
                {
                    var part = ChunkPath(upload.Id, i);
                    if (!_files.Exists(part)) throw AppException.Validation($"Thiếu phần tệp số {i + 1}. Vui lòng tải lại.");
                    await using var input = _files.OpenRead(part);
                    await input.CopyToAsync(output, ct);
                }
            }
            if (_files.Length(assembled) != upload.DeclaredSize)
                throw AppException.Validation("Tệp nhận được không đủ dung lượng đã khai báo. Vui lòng tải lại.");
            var dto = await PromoteAsync(assembled, upload.FileName, upload.Visibility, ct);
            upload.CompletedAssetId = dto.Id;
            await _db.SaveChangesAsync(ct);
            return dto;
        }
        finally
        {
            _files.DeleteDirectory($"{TempRoot}/{upload.Id:N}");
        }
    }

    public async Task<MediaDto> UpdateAsync(Guid id, UpdateMedia cmd, CancellationToken ct = default)
    {
        var m = await _db.MediaAssets.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        m.Alt = cmd.Alt?.Trim();
        m.Caption = cmd.Caption?.Trim();
        await _db.SaveChangesAsync(ct);
        return ToDto(m);
    }

    public async Task<IReadOnlyList<MediaUsageDto>> UsagesAsync(Guid id, CancellationToken ct = default) =>
        await _db.MediaUsages.AsNoTracking().Where(u => u.MediaId == id)
            .OrderBy(u => u.OwnerType).Select(u => new MediaUsageDto(u.OwnerType, u.OwnerId, u.OwnerTitle)).ToListAsync(ct);

    /// <summary>Soft delete; refused while any content still references the file.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var m = await _db.MediaAssets.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        var uses = await _db.MediaUsages.CountAsync(u => u.MediaId == id, ct);
        if (uses > 0) throw AppException.Conflict("media_in_use", $"Tệp đang được dùng ở {uses} nội dung. Gỡ khỏi nội dung trước khi xóa.");
        m.IsDeleted = true;
        m.DeletedUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("cms.media.delete", "MediaAsset", id.ToString(), new { m.OriginalName }, ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var m = await _db.MediaAssets.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        m.IsDeleted = false;
        m.DeletedUtc = null;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("cms.media.restore", "MediaAsset", id.ToString(), null, ct);
    }

    public async Task SetUsagesAsync(string ownerType, Guid ownerId, string ownerTitle, IEnumerable<Guid> mediaIds, CancellationToken ct = default)
    {
        var wanted = mediaIds.Distinct().ToList();
        var known = await _db.MediaAssets.Where(m => wanted.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct);
        var current = await _db.MediaUsages.Where(u => u.OwnerType == ownerType && u.OwnerId == ownerId).ToListAsync(ct);
        _db.MediaUsages.RemoveRange(current.Where(u => !known.Contains(u.MediaId)));
        foreach (var u in current.Where(u => known.Contains(u.MediaId))) u.OwnerTitle = ownerTitle;
        foreach (var id in known.Where(id => current.All(u => u.MediaId != id)))
            _db.MediaUsages.Add(new MediaUsage(id, ownerType, ownerId, ownerTitle));
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Public file (null when missing, deleted or restricted). thumbnail=true serves the small rendition.</summary>
    public async Task<MediaFile?> PublicFileAsync(Guid id, bool thumbnail, CancellationToken ct = default)
    {
        var m = await _db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.Visibility == MediaVisibility.Public, ct);
        return m is null ? null : ToFile(m, thumbnail);
    }

    public async Task<MediaFile?> RestrictedFileAsync(Guid id, CancellationToken ct = default)
    {
        var m = await _db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        return m is null ? null : ToFile(m, thumbnail: false);
    }

    private MediaFile? ToFile(MediaAsset m, bool thumbnail)
    {
        if (thumbnail)
            return m.ThumbnailPath is null ? null : new MediaFile(_files.FullPath(m.ThumbnailPath), "image/jpeg", "thumb.jpg", true);
        var inline = m.Kind is MediaKind.Image or MediaKind.Video || m.ContentType == "application/pdf";
        return new MediaFile(_files.FullPath(m.StoredPath), m.ContentType, m.OriginalName, inline);
    }

    private async Task<MediaDto> PromoteAsync(string tempPath, string originalName, MediaVisibility visibility, CancellationToken ct)
    {
        var head = new byte[64];
        int read;
        await using (var s = _files.OpenRead(tempPath)) read = await s.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        var sniff = FileSniffer.Sniff(head.AsSpan(0, read), originalName) ?? throw Unsupported();
        var size = _files.Length(tempPath);
        EnsureWithinLimit(sniff.Kind, size);

        var id = Guid.NewGuid();
        var folder = $"{_clock.UtcNow:yyyy}/{_clock.UtcNow:MM}";
        var stored = $"{folder}/{id:N}{sniff.Extension}";
        var asset = new MediaAsset(id, sniff.Kind, originalName, stored, sniff.ContentType, size, visibility, _user.AdminId, _clock.UtcNow);

        if (sniff.Kind == MediaKind.Image)
        {
            ImageInfo? info;
            await using (var s = _files.OpenRead(tempPath)) info = _images.Inspect(s);
            if (info is null) throw Unsupported();
            asset.Width = info.Width;
            asset.Height = info.Height;
            var thumb = $"{folder}/{id:N}_thumb.jpg";
            await using (var s = _files.OpenRead(tempPath))
            await using (var output = _files.Create(thumb))
                _images.WriteThumbnail(s, output, _options.ThumbnailWidth);
            asset.ThumbnailPath = thumb;
        }

        await using (var s = _files.OpenRead(tempPath)) await _files.SaveAsync(stored, s, ct);
        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("cms.media.upload", "MediaAsset", id.ToString(), new { originalName, asset.ContentType, size }, ct);
        return ToDto(asset);
    }

    private async Task<MediaUpload> LoadUploadAsync(Guid id, CancellationToken ct)
    {
        var upload = await _db.MediaUploads.SingleOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound();
        if (upload.CreatedBy != _user.AdminId) throw AppException.NotFound();
        if (upload.CompletedAssetId is not null) throw AppException.Conflict("upload_completed", "Phiên tải lên đã hoàn tất.");
        return upload;
    }

    private void EnsureWithinLimit(MediaKind kind, long size)
    {
        if (size > _options.LimitFor(kind)) throw TooLarge(kind, null);
    }

    private AppException TooLarge(MediaKind kind, string? detail)
    {
        var mb = _options.LimitFor(kind) / (1024.0 * 1024.0);
        var label = kind switch { MediaKind.Image => "ảnh", MediaKind.Video => "video", _ => "tài liệu" };
        return new AppException("file_too_large", $"{detail} Tệp {label} tối đa {mb:0.#} MB.".Trim(), 400);
    }

    private static AppException Unsupported() =>
        new("unsupported_file_type", "Định dạng tệp không được hỗ trợ hoặc nội dung không khớp phần mở rộng. Chấp nhận: JPG, PNG, GIF, WEBP, MP4, WEBM, PDF, DOC(X), XLS(X), PPT(X), ZIP.", 400);

    private static string ChunkPath(Guid uploadId, int index) => $"{TempRoot}/{uploadId:N}/{index:D6}.part";

    private static MediaVisibility ParseVisibility(string? v) =>
        string.Equals(v, "restricted", StringComparison.OrdinalIgnoreCase) ? MediaVisibility.Restricted : MediaVisibility.Public;

    /// <summary>Display name only (path parts and control characters removed); never used on disk.</summary>
    private static string CleanName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim().TrimStart('.');
        if (name.Length == 0) throw AppException.Validation("Tên tệp không hợp lệ.");
        return name.Length > 200 ? name[^200..] : name;
    }

    private static string SafeUrlName(string name) => Uri.EscapeDataString(Slug.From(Path.GetFileNameWithoutExtension(name)) + Path.GetExtension(name).ToLowerInvariant());

    private static MediaDto ToDto(MediaAsset m) => new(m.Id, m.Kind.ToString().ToLowerInvariant(), m.OriginalName, m.ContentType, m.Size,
        m.Width, m.Height, m.Visibility.ToString().ToLowerInvariant(), m.Alt, m.Caption,
        m.Visibility == MediaVisibility.Public ? PublicUrl(m.Id, m.OriginalName) : $"/api/files/{m.Id}",
        m.ThumbnailPath is null ? null : m.Visibility == MediaVisibility.Public ? $"/media/{m.Id}/thumb" : null,
        m.CreatedUtc, m.IsDeleted);
}
