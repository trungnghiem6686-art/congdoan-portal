using CongDoan.Application.Cms;
using Microsoft.Extensions.Options;

namespace CongDoan.Infrastructure.Cms;

/// <summary>Files under Media:RootPath. Every relative path is resolved and checked to stay inside the root.</summary>
public sealed class LocalFileStore : IFileStore
{
    private readonly string _root;

    public LocalFileStore(IOptions<MediaOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.RootPath))
            throw new InvalidOperationException("Media:RootPath is not configured.");
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public string FullPath(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Path escapes the media root.");
        return full;
    }

    public async Task SaveAsync(string relativePath, Stream content, CancellationToken ct = default)
    {
        await using var output = Create(relativePath);
        await content.CopyToAsync(output, ct);
    }

    public Stream Create(string relativePath)
    {
        var full = FullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
    }

    public Stream OpenRead(string relativePath) =>
        new FileStream(FullPath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

    public bool Exists(string relativePath) => File.Exists(FullPath(relativePath));

    public long Length(string relativePath) => new FileInfo(FullPath(relativePath)).Length;

    public void Delete(string relativePath)
    {
        var full = FullPath(relativePath);
        if (File.Exists(full)) File.Delete(full);
    }

    public void DeleteDirectory(string relativePath)
    {
        var full = FullPath(relativePath);
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
