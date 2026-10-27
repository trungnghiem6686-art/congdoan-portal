using CongDoan.Domain.Cms;

namespace CongDoan.Application.Cms;

public sealed class MediaOptions
{
    /// <summary>Absolute folder outside the release directory, never executable.</summary>
    public string RootPath { get; set; } = "";
    public long MaxImageBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxVideoBytes { get; set; } = 1024L * 1024 * 1024;
    public long MaxDocumentBytes { get; set; } = 50 * 1024 * 1024;
    public int ChunkSize { get; set; } = 5 * 1024 * 1024;
    public int ThumbnailWidth { get; set; } = 400;

    public long LimitFor(MediaKind kind) => kind switch
    {
        MediaKind.Image => MaxImageBytes,
        MediaKind.Video => MaxVideoBytes,
        _ => MaxDocumentBytes,
    };
}

public interface IFileStore
{
    string FullPath(string relativePath);
    Task SaveAsync(string relativePath, Stream content, CancellationToken ct = default);
    Stream Create(string relativePath);
    Stream OpenRead(string relativePath);
    bool Exists(string relativePath);
    long Length(string relativePath);
    void Delete(string relativePath);
    void DeleteDirectory(string relativePath);
}

public sealed record ImageInfo(int Width, int Height);

public interface IImageProcessor
{
    /// <summary>Decodes the image fully (proves it is a real image); null if it cannot be decoded.</summary>
    ImageInfo? Inspect(Stream image);
    void WriteThumbnail(Stream image, Stream output, int width);
}

public sealed record SniffResult(MediaKind Kind, string ContentType, string Extension);

/// <summary>Decides the real file type from its first bytes; the declared name only disambiguates container formats.</summary>
public static class FileSniffer
{
    private static readonly Dictionary<string, (MediaKind Kind, string Type)> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = (MediaKind.Image, "image/jpeg"), [".jpeg"] = (MediaKind.Image, "image/jpeg"),
        [".png"] = (MediaKind.Image, "image/png"), [".gif"] = (MediaKind.Image, "image/gif"), [".webp"] = (MediaKind.Image, "image/webp"),
        [".mp4"] = (MediaKind.Video, "video/mp4"), [".webm"] = (MediaKind.Video, "video/webm"),
        [".pdf"] = (MediaKind.Document, "application/pdf"),
        [".doc"] = (MediaKind.Document, "application/msword"),
        [".xls"] = (MediaKind.Document, "application/vnd.ms-excel"),
        [".ppt"] = (MediaKind.Document, "application/vnd.ms-powerpoint"),
        [".docx"] = (MediaKind.Document, "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        [".xlsx"] = (MediaKind.Document, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        [".pptx"] = (MediaKind.Document, "application/vnd.openxmlformats-officedocument.presentationml.presentation"),
        [".zip"] = (MediaKind.Document, "application/zip"),
    };

    /// <summary>Kind implied by the extension (used to pick the size limit before bytes arrive).</summary>
    public static MediaKind? KindOf(string fileName) =>
        ByExtension.TryGetValue(Path.GetExtension(fileName ?? ""), out var x) ? x.Kind : null;

    public static SniffResult? Sniff(ReadOnlySpan<byte> head, string declaredName)
    {
        var ext = Path.GetExtension(declaredName ?? "").ToLowerInvariant();
        if (StartsWith(head, 0xFF, 0xD8, 0xFF)) return new(MediaKind.Image, "image/jpeg", ".jpg");
        if (StartsWith(head, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)) return new(MediaKind.Image, "image/png", ".png");
        if (StartsWith(head, (byte)'G', (byte)'I', (byte)'F', (byte)'8')) return new(MediaKind.Image, "image/gif", ".gif");
        if (head.Length >= 12 && StartsWith(head, (byte)'R', (byte)'I', (byte)'F', (byte)'F') && head.Slice(8, 4).SequenceEqual("WEBP"u8))
            return new(MediaKind.Image, "image/webp", ".webp");
        if (head.Length >= 12 && head.Slice(4, 4).SequenceEqual("ftyp"u8)) return new(MediaKind.Video, "video/mp4", ".mp4");
        if (StartsWith(head, 0x1A, 0x45, 0xDF, 0xA3)) return new(MediaKind.Video, "video/webm", ".webm");
        if (StartsWith(head, (byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-')) return new(MediaKind.Document, "application/pdf", ".pdf");
        if (StartsWith(head, 0x50, 0x4B, 0x03, 0x04) && ext is ".docx" or ".xlsx" or ".pptx" or ".zip")
            return new(MediaKind.Document, ByExtension[ext].Type, ext);
        if (StartsWith(head, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1) && ext is ".doc" or ".xls" or ".ppt")
            return new(MediaKind.Document, ByExtension[ext].Type, ext);
        return null;
    }

    private static bool StartsWith(ReadOnlySpan<byte> data, params byte[] prefix) => data.Length >= prefix.Length && data[..prefix.Length].SequenceEqual(prefix);
}
