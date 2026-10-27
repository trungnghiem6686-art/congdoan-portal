using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms.Import;

/// <param name="SourceDir">Folder holding manifest.json and the files it references (see docs/migration.md).</param>
/// <param name="Batch">Prefix of every SourceKey; the same batch re-run never creates duplicates.</param>
public sealed record ImportOptions(string SourceDir, bool DryRun, string Batch = "legacy");

public sealed record ImportIssue(string SourceKey, string Kind, string Message);

public sealed class ImportReport
{
    public int SourcePosts { get; set; }
    public int SourceCategories { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int Images { get; set; }
    public int Redirects { get; set; }
    public bool DryRun { get; set; }
    public List<ImportIssue> Issues { get; } = new();

    /// <summary>Reconciliation report: totals first, then one line per issue.</summary>
    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.Append("metric,value\n");
        sb.Append($"dry_run,{(DryRun ? 1 : 0)}\n");
        sb.Append($"source_posts,{SourcePosts}\n");
        sb.Append($"source_categories,{SourceCategories}\n");
        sb.Append($"created,{Created}\n");
        sb.Append($"skipped,{Skipped}\n");
        sb.Append($"failed,{Failed}\n");
        sb.Append($"images,{Images}\n");
        sb.Append($"redirects,{Redirects}\n");
        sb.Append("\nsource_key,kind,message\n");
        foreach (var i in Issues) sb.Append($"{Csv(i.SourceKey)},{Csv(i.Kind)},{Csv(i.Message)}\n");
        return sb.ToString();
    }

    private static string Csv(string v)
    {
        // Leading = + - @ would be run as a formula by Excel.
        if (v.Length > 0 && "=+-@".Contains(v[0])) v = "'" + v;
        return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }
}

/// <summary>
/// Imports a legacy site export (categories, posts, images) and maps old URLs to new ones with 301 redirects.
/// Re-runnable: posts are keyed by SourceKey, categories by slug. Problems are reported, never fatal.
/// </summary>
public sealed partial class LegacyImporter
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly ICmsDbContext _db;
    private readonly MediaService _media;
    private readonly RedirectService _redirects;
    private readonly IContentSanitizer _sanitizer;
    private readonly IClock _clock;

    public LegacyImporter(ICmsDbContext db, MediaService media, RedirectService redirects, IContentSanitizer sanitizer, IClock clock)
    {
        _db = db;
        _media = media;
        _redirects = redirects;
        _sanitizer = sanitizer;
        _clock = clock;
    }

    private sealed record Manifest(List<SourceCategory>? Categories, List<SourcePost>? Posts);

    private sealed record SourceCategory(string Key, string Name, string? OldUrl);

    private sealed record SourcePost(string Key, string? Title, string? Summary, string? BodyHtml, string? CategoryKey,
        string? CoverImage, DateTime? PublishedUtc, string? OldUrl);

    public async Task<ImportReport> RunAsync(ImportOptions o, CancellationToken ct = default)
    {
        var root = Path.GetFullPath(o.SourceDir);
        var manifestPath = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifestPath)) throw AppException.Validation($"Không thấy manifest.json trong {root}.");
        Manifest manifest;
        await using (var stream = File.OpenRead(manifestPath))
            manifest = await JsonSerializer.DeserializeAsync<Manifest>(stream, Json, ct) ?? new Manifest(null, null);

        var report = new ImportReport { DryRun = o.DryRun };
        var categories = manifest.Categories ?? new();
        var posts = manifest.Posts ?? new();
        report.SourceCategories = categories.Count;
        report.SourcePosts = posts.Count;
        var run = new Run(o, root, report);

        var categoryIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var c in categories)
        {
            var id = await ImportCategoryAsync(run, c, ct);
            if (id is { } value) categoryIds[c.Key] = value;
        }

        foreach (var p in posts)
        {
            try
            {
                await ImportPostAsync(run, p, categoryIds, ct);
            }
            catch (AppException e)
            {
                report.Failed++;
                report.Issues.Add(new ImportIssue(p.Key, "post", e.Message));
            }
        }
        return report;
    }

    private sealed class Run
    {
        public Run(ImportOptions options, string root, ImportReport report)
        {
            Options = options;
            Root = root;
            Report = report;
        }

        public ImportOptions Options { get; }
        public string Root { get; }
        public ImportReport Report { get; }
        /// <summary>Source path → uploaded media URL and id (an image used twice is uploaded once).</summary>
        public Dictionary<string, MediaDto> Uploaded { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Guid?> ImportCategoryAsync(Run run, SourceCategory c, CancellationToken ct)
    {
        var slug = Slug.From(c.Name ?? "");
        if (slug.Length == 0)
        {
            run.Report.Issues.Add(new ImportIssue(c.Key, "category", "Chuyên mục không có tên."));
            return null;
        }
        var existing = await _db.Categories.SingleOrDefaultAsync(x => x.Slug == slug && !x.IsDeleted, ct);
        if (run.Options.DryRun) return existing?.Id ?? Guid.Empty;
        if (existing is null)
        {
            existing = new Category(c.Name!.Trim(), slug);
            _db.Categories.Add(existing);
            await _db.SaveChangesAsync(ct);
        }
        await MapAsync(run, c.Key, c.OldUrl, PublicPaths.For("category", existing.Slug), ct);
        return existing.Id;
    }

    private async Task ImportPostAsync(Run run, SourcePost p, Dictionary<string, Guid> categoryIds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.Key)) throw AppException.Validation("Bài viết thiếu khóa nguồn (key).");
        var title = p.Title?.Trim();
        if (string.IsNullOrEmpty(title)) throw AppException.Validation("Bài viết thiếu tiêu đề.");
        var sourceKey = $"{run.Options.Batch}:{p.Key}";

        var existing = await _db.Posts.AsNoTracking().Where(x => x.SourceKey == sourceKey).Select(x => new { x.Slug }).SingleOrDefaultAsync(ct);
        if (existing is not null)
        {
            run.Report.Skipped++;
            if (!run.Options.DryRun) await MapAsync(run, p.Key, p.OldUrl, PublicPaths.For("post", existing.Slug), ct);
            return;
        }

        Guid? categoryId = null;
        if (p.CategoryKey is { } ck)
        {
            if (categoryIds.TryGetValue(ck, out var id)) categoryId = id;
            else run.Report.Issues.Add(new ImportIssue(p.Key, "category", $"Không thấy chuyên mục '{ck}'; bài được nhập không có chuyên mục."));
        }

        if (run.Options.DryRun)
        {
            CheckImages(run, p);
            run.Report.Created++;
            return;
        }

        var body = await RewriteImagesAsync(run, p.Key, p.BodyHtml ?? "", ct);
        Guid? cover = null;
        if (!string.IsNullOrWhiteSpace(p.CoverImage)) cover = (await UploadAsync(run, p.Key, p.CoverImage, ct))?.Id;

        var slug = await FreeSlugAsync(Slug.From(title), ct);
        var now = _clock.UtcNow;
        var post = new Post(title, slug, false, null, now)
        {
            SourceKey = sourceKey,
            Summary = p.Summary?.Trim(),
            Body = _sanitizer.Sanitize(body),
            CategoryId = categoryId,
            CoverMediaId = cover,
        };
        post.SearchText = Slug.SearchText($"{post.Title} {post.Summary} {PostService.StripTags(post.Body)}");
        post.TryTransition(PostStatus.Published, now, null, null);
        if (p.PublishedUtc is { } published) post.PublishedUtc = published.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(published, DateTimeKind.Utc) : published.ToUniversalTime();
        _db.Posts.Add(post);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.Posts.Remove(post);
            throw AppException.Validation("Không lưu được bài viết (trùng khóa nguồn hoặc đường dẫn).");
        }

        var media = PostService.MediaRefs(post.Body).ToList();
        if (cover is { } c) media.Add(c);
        await _media.SetUsagesAsync("Post", post.Id, post.Title, media, ct);
        run.Report.Created++;
        await MapAsync(run, p.Key, p.OldUrl, PublicPaths.For("post", post.Slug), ct);
    }

    private async Task<string> FreeSlugAsync(string slug, CancellationToken ct)
    {
        if (slug.Length == 0) slug = "bai-viet";
        if (!await _db.Posts.AnyAsync(x => x.Slug == slug, ct)) return slug;
        var conflict = (SlugConflictException)await SlugRules.DuplicateAsync(slug, s => _db.Posts.AnyAsync(x => x.Slug == s, ct));
        return conflict.Suggestion;
    }

    private async Task<string> RewriteImagesAsync(Run run, string key, string html, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var last = 0;
        foreach (Match m in ImgTag().Matches(html))
        {
            sb.Append(html, last, m.Index - last);
            last = m.Index + m.Length;
            var src = m.Groups["src"].Value;
            var uploaded = await UploadAsync(run, key, src, ct);
            if (uploaded is not null)
                sb.Append(html, m.Index, m.Groups["src"].Index - m.Index).Append(uploaded.Url).Append(html, m.Groups["src"].Index + m.Groups["src"].Length, last - (m.Groups["src"].Index + m.Groups["src"].Length));
        }
        sb.Append(html, last, html.Length - last);
        return sb.ToString();
    }

    private void CheckImages(Run run, SourcePost p)
    {
        var sources = ImgTag().Matches(p.BodyHtml ?? "").Select(m => m.Groups["src"].Value).ToList();
        if (!string.IsNullOrWhiteSpace(p.CoverImage)) sources.Add(p.CoverImage);
        foreach (var src in sources)
            if (Resolve(run, p.Key, src) is { } path && !File.Exists(path))
                run.Report.Issues.Add(new ImportIssue(p.Key, "image", $"Không thấy tệp ảnh {src}."));
    }

    /// <summary>Uploads one image through the normal media pipeline; a missing or broken file is reported and skipped.</summary>
    private async Task<MediaDto?> UploadAsync(Run run, string key, string src, CancellationToken ct)
    {
        var path = Resolve(run, key, src);
        if (path is null) return null;
        if (run.Uploaded.TryGetValue(path, out var cached)) return cached;
        if (!File.Exists(path))
        {
            run.Report.Issues.Add(new ImportIssue(key, "image", $"Không thấy tệp ảnh {src}."));
            return null;
        }
        try
        {
            await using var stream = File.OpenRead(path);
            var dto = await _media.UploadAsync(stream, Path.GetFileName(path), stream.Length, "public", ct);
            run.Uploaded[path] = dto;
            run.Report.Images++;
            return dto;
        }
        catch (AppException e)
        {
            run.Report.Issues.Add(new ImportIssue(key, "image", $"Ảnh {src} bị lỗi, đã bỏ qua: {e.Message}"));
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            run.Report.Issues.Add(new ImportIssue(key, "image", $"Không đọc được tệp ảnh {src}, đã bỏ qua ({e.GetType().Name})."));
            return null;
        }
    }

    /// <summary>Local path inside the export folder, or null (reported) for remote or escaping references.</summary>
    private static string? Resolve(Run run, string key, string src)
    {
        if (Uri.TryCreate(src, UriKind.Absolute, out var abs) && abs.Scheme is "http" or "https" or "data")
        {
            run.Report.Issues.Add(new ImportIssue(key, "image", $"Ảnh ngoài thư mục xuất ({Truncate(src)}) chưa được tải; cần xuất kèm tệp."));
            return null;
        }
        var relative = Uri.UnescapeDataString(src.Split('?', '#')[0]).TrimStart('/', '\\');
        var full = Path.GetFullPath(Path.Combine(run.Root, relative));
        if (!full.StartsWith(run.Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            run.Report.Issues.Add(new ImportIssue(key, "image", $"Đường dẫn ảnh {Truncate(src)} nằm ngoài thư mục xuất, đã bỏ qua."));
            return null;
        }
        return full;
    }

    /// <summary>Old URL → new path as a 301, unless the old URL is unusable or already maps there.</summary>
    private async Task MapAsync(Run run, string key, string? oldUrl, string target, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(oldUrl)) return;
        var raw = oldUrl.Trim();
        if (Uri.TryCreate(raw, UriKind.Absolute, out var abs) && abs.Scheme is "http" or "https") raw = abs.PathAndQuery;
        if (raw.Contains('?'))
        {
            run.Report.Issues.Add(new ImportIssue(key, "redirect", $"URL cũ có tham số truy vấn ({raw}); cần quy tắc rewrite trên IIS."));
            return;
        }
        var old = PublicPaths.Normalize(raw);
        if (old == "/" || old == target)
        {
            run.Report.Issues.Add(new ImportIssue(key, "redirect", $"Bỏ qua URL cũ {raw}: trùng trang chủ hoặc trùng đường dẫn mới."));
            return;
        }
        if (await _db.Redirects.AnyAsync(r => r.OldPath == old && r.NewPath == target, ct)) return;
        await _redirects.AddAsync(old, target, ct);
        run.Report.Redirects++;
    }

    private static string Truncate(string s) => s.Length <= 120 ? s : s[..120] + "…";

    [GeneratedRegex("""<img\b[^>]*?\bsrc\s*=\s*["'](?<src>[^"']+)["'][^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex ImgTag();
}
