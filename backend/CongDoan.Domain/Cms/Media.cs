namespace CongDoan.Domain.Cms;

public enum MediaKind
{
    Image = 1,
    Video = 2,
    Document = 3,
}

public enum MediaVisibility
{
    Public = 1,
    /// <summary>Served only through the permission-checked endpoint.</summary>
    Restricted = 2,
}

public sealed class MediaAsset
{
    private MediaAsset() { }

    public MediaAsset(Guid id, MediaKind kind, string originalName, string storedPath, string contentType, long size,
        MediaVisibility visibility, Guid? uploadedBy, DateTime createdUtc)
    {
        Id = id;
        Kind = kind;
        OriginalName = originalName;
        StoredPath = storedPath;
        ContentType = contentType;
        Size = size;
        Visibility = visibility;
        UploadedBy = uploadedBy;
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }
    public MediaKind Kind { get; private set; }
    public string OriginalName { get; private set; } = "";
    /// <summary>Relative path under the media root; always system generated.</summary>
    public string StoredPath { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long Size { get; private set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? ThumbnailPath { get; set; }
    public MediaVisibility Visibility { get; set; }
    public string? Alt { get; set; }
    public string? Caption { get; set; }
    public Guid? UploadedBy { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedUtc { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
}

/// <summary>In-progress chunked upload (large video files).</summary>
public sealed class MediaUpload
{
    private MediaUpload() { }

    public MediaUpload(string fileName, long declaredSize, int chunkSize, MediaVisibility visibility, Guid? createdBy, DateTime createdUtc)
    {
        Id = Guid.NewGuid();
        FileName = fileName;
        DeclaredSize = declaredSize;
        ChunkSize = chunkSize;
        Visibility = visibility;
        CreatedBy = createdBy;
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }
    public string FileName { get; private set; } = "";
    public long DeclaredSize { get; private set; }
    public int ChunkSize { get; private set; }
    public MediaVisibility Visibility { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public Guid? CompletedAssetId { get; set; }

    public int ChunkCount => (int)((DeclaredSize + ChunkSize - 1) / ChunkSize);
}

/// <summary>Where a media asset is referenced (post body, banner, album...). Blocks deletion while present.</summary>
public sealed class MediaUsage
{
    private MediaUsage() { }

    public MediaUsage(Guid mediaId, string ownerType, Guid ownerId, string ownerTitle)
    {
        MediaId = mediaId;
        OwnerType = ownerType;
        OwnerId = ownerId;
        OwnerTitle = ownerTitle;
    }

    public Guid MediaId { get; private set; }
    public string OwnerType { get; private set; } = "";
    public Guid OwnerId { get; private set; }
    public string OwnerTitle { get; set; } = "";
}
