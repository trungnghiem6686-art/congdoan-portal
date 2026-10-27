using System.Text.Json;
using System.Text.Json.Nodes;
using CongDoan.Application.Shared;

namespace CongDoan.Application.Cms;

public sealed record SectionInput(string? Id, string? Type, string? Title, bool Visible = true, DateTime? StartsUtc = null, DateTime? EndsUtc = null,
    JsonElement Config = default);

public sealed record Section(string Id, string Type, string? Title, bool Visible, DateTime? StartsUtc, DateTime? EndsUtc, JsonObject Config)
{
    public bool IsLive(DateTime nowUtc) => Visible && (StartsUtc is null || StartsUtc <= nowUtc) && (EndsUtc is null || EndsUtc > nowUtc);
}

/// <summary>
/// The ten standard block types and their allowed configuration. Each config is parsed into a typed record,
/// range-checked and re-serialised, so unknown fields (and any script) never reach storage.
/// </summary>
public sealed class SectionCatalog
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record NewsConfig(string? CategorySlug, string? TagSlug, int Limit = 6, string? Layout = null, int Columns = 3);
    private sealed record BannerConfig(string? Position, string? Layout);
    private sealed record LinkItem(string? Label, string? Url, string? Icon);
    private sealed record LinksConfig(List<LinkItem>? Items, string? Layout);
    private sealed record DocumentsConfig(string? DocType, int Limit = 5, string? Layout = null);
    private sealed record GalleryConfig(string? AlbumKind, int Limit = 6, string? Layout = null);
    private sealed record ExamConfig(int Limit = 5, string? Layout = null);
    private sealed record IntroConfig(string? Html, string? Layout);

    private static readonly Dictionary<string, (string Label, string[] Layouts)> Types = new()
    {
        ["featured_news"] = ("Tin nổi bật", new[] { "hero", "hero-list" }),
        ["news_list"] = ("Danh sách tin", new[] { "list", "compact" }),
        ["news_grid"] = ("Lưới tin", new[] { "grid", "cards" }),
        ["banner"] = ("Banner", new[] { "wide", "strip" }),
        ["quick_links"] = ("Tiện ích nhanh", new[] { "icons", "buttons" }),
        ["documents"] = ("Văn bản", new[] { "table", "list" }),
        ["gallery"] = ("Thư viện ảnh/video", new[] { "grid", "strip" }),
        ["exam_notice"] = ("Thông báo kỳ thi", new[] { "list", "cards" }),
        ["intro_text"] = ("Văn bản giới thiệu", new[] { "plain", "boxed" }),
        ["links"] = ("Liên kết", new[] { "list", "logos" }),
    };

    private readonly IContentSanitizer _sanitizer;

    public SectionCatalog(IContentSanitizer sanitizer) => _sanitizer = sanitizer;

    public static IReadOnlyDictionary<string, (string Label, string[] Layouts)> All => Types;

    public IReadOnlyList<Section> Validate(IReadOnlyList<SectionInput>? input)
    {
        var sections = new List<Section>();
        var ids = new HashSet<string>();
        var index = 0;
        foreach (var s in input ?? Array.Empty<SectionInput>())
        {
            index++;
            var where = $"Khối thứ {index}";
            if (string.IsNullOrWhiteSpace(s.Type) || !Types.TryGetValue(s.Type, out var meta))
                throw Invalid($"{where}: loại khối \"{s.Type}\" không được hỗ trợ.");
            var id = string.IsNullOrWhiteSpace(s.Id) ? Guid.NewGuid().ToString("N") : s.Id.Trim();
            if (id.Length > 64 || !id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_')) throw Invalid($"{where}: mã khối không hợp lệ.");
            if (!ids.Add(id)) throw Invalid($"{where}: mã khối bị trùng.");
            if (s.StartsUtc is { } a && s.EndsUtc is { } b && b <= a) throw Invalid($"{where}: thời điểm kết thúc phải sau bắt đầu.");
            var config = Normalize(s.Type, s.Config, meta.Layouts, where);
            sections.Add(new Section(id, s.Type, s.Title?.Trim(), s.Visible, s.StartsUtc, s.EndsUtc, config));
        }
        if (sections.Count > 40) throw Invalid("Tối đa 40 khối trên một trang.");
        return sections;
    }

    private JsonObject Normalize(string type, JsonElement raw, string[] layouts, string where)
    {
        if (raw.ValueKind != JsonValueKind.Object) throw Invalid($"{where}: thiếu cấu hình khối.");
        string Layout(string? l) => l is not null && layouts.Contains(l) ? l : layouts[0];
        object normalized;
        switch (type)
        {
            case "featured_news" or "news_list" or "news_grid":
            {
                var n = Parse<NewsConfig>(raw, where);
                normalized = new NewsConfig(Clean(n.CategorySlug), Clean(n.TagSlug),
                    Range(n.Limit, 1, type == "featured_news" ? 10 : 20, where, "số tin"), Layout(n.Layout), Range(n.Columns, 2, 4, where, "số cột"));
                break;
            }
            case "banner":
            {
                var b = Parse<BannerConfig>(raw, where);
                if (string.IsNullOrWhiteSpace(b.Position)) throw Invalid($"{where}: chọn vị trí banner.");
                normalized = new BannerConfig(b.Position.Trim(), Layout(b.Layout));
                break;
            }
            case "quick_links" or "links":
                normalized = Links(Parse<LinksConfig>(raw, where), type == "quick_links" ? 12 : 30, Layout, where);
                break;
            case "documents":
            {
                var d = Parse<DocumentsConfig>(raw, where);
                normalized = new DocumentsConfig(Clean(d.DocType), Range(d.Limit, 1, 20, where, "số văn bản"), Layout(d.Layout));
                break;
            }
            case "gallery":
            {
                var g = Parse<GalleryConfig>(raw, where);
                normalized = new GalleryConfig(g.AlbumKind is "Photo" or "Video" ? g.AlbumKind : null, Range(g.Limit, 1, 12, where, "số album"), Layout(g.Layout));
                break;
            }
            case "exam_notice":
            {
                var e = Parse<ExamConfig>(raw, where);
                normalized = new ExamConfig(Range(e.Limit, 1, 10, where, "số kỳ thi"), Layout(e.Layout));
                break;
            }
            case "intro_text":
            {
                var intro = Parse<IntroConfig>(raw, where);
                var html = _sanitizer.Sanitize(intro.Html);
                if (PostService.StripTags(html).Trim().Length == 0) throw Invalid($"{where}: nội dung giới thiệu đang trống.");
                normalized = new IntroConfig(html, Layout(intro.Layout));
                break;
            }
            default:
                throw Invalid($"{where}: loại khối không được hỗ trợ.");
        }
        return (JsonObject)JsonSerializer.SerializeToNode(normalized, normalized.GetType(), Json)!;
    }

    private static LinksConfig Links(LinksConfig c, int max, Func<string?, string> layout, string where)
    {
        var items = c.Items ?? new List<LinkItem>();
        if (items.Count == 0) throw Invalid($"{where}: cần ít nhất một liên kết.");
        if (items.Count > max) throw Invalid($"{where}: tối đa {max} liên kết.");
        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.Label)) throw Invalid($"{where}: liên kết thiếu tên.");
            if (!UrlValidator.IsSafe(it.Url)) throw Invalid($"{where}: liên kết \"{it.Url}\" không an toàn.");
        }
        return new LinksConfig(items.Select(i => new LinkItem(i.Label!.Trim(), i.Url!.Trim(), Clean(i.Icon))).ToList(), layout(c.Layout));
    }

    private static T Parse<T>(JsonElement raw, string where)
    {
        try { return raw.Deserialize<T>(Json) ?? throw Invalid($"{where}: thiếu cấu hình khối."); }
        catch (JsonException) { throw Invalid($"{where}: cấu hình khối sai định dạng."); }
    }

    private static int Range(int value, int min, int max, string where, string what) =>
        value >= min && value <= max ? value : throw Invalid($"{where}: {what} phải từ {min} đến {max}.");

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static AppException Invalid(string message) => new("invalid_section", message, 400);
}
