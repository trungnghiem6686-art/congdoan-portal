using System.Text.Json;
using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public sealed record LinkSetting(string Label, string Url);
public sealed record SiteInfo(string Name, string? ShortName, string? Tagline, Guid? LogoMediaId, string? LogoUrl);
public sealed record ContactInfo(string? Organization, string? Address, string? Phone, string? Email, string? WorkingHours, string? MapUrl);
public sealed record FooterColumn(string Title, IReadOnlyList<LinkSetting> Links);
public sealed record FooterInfo(string? Copyright, IReadOnlyList<FooterColumn> Columns, string? Note);
public sealed record SocialLink(string Platform, string Url);
public sealed record SocialInfo(IReadOnlyList<SocialLink> Links);
public sealed record UtilityInfo(IReadOnlyList<LinkSetting> Links);
public sealed record SeoInfo(string? Description, string? Keywords);

public sealed record PublicSite(SiteInfo Site, ContactInfo Contact, FooterInfo Footer, SocialInfo Social, UtilityInfo Utility, SeoInfo Seo);

/// <summary>Typed site settings stored as JSON per key. Unknown keys are rejected; every URL is validated.</summary>
public sealed class SettingsService
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, Type> Keys = new()
    {
        ["site"] = typeof(SiteInfo),
        ["contact"] = typeof(ContactInfo),
        ["footer"] = typeof(FooterInfo),
        ["social"] = typeof(SocialInfo),
        ["utility"] = typeof(UtilityInfo),
        ["seo"] = typeof(SeoInfo),
    };

    /// <summary>Placeholder values clearly marked "MẪU" until the organisation supplies real ones.</summary>
    public static readonly PublicSite Defaults = new(
        new SiteInfo("Công đoàn Đường sắt Việt Nam (MẪU)", "CĐĐSVN", "Cổng thông tin điện tử (MẪU)", null, null),
        new ContactInfo("MẪU – Công đoàn Đường sắt Việt Nam", "MẪU – Địa chỉ chưa cung cấp", "MẪU – 000 000 0000", "mau@example.com", "MẪU – Thứ Hai đến Thứ Sáu", null),
        new FooterInfo("© MẪU – Công đoàn Đường sắt Việt Nam", new[]
        {
            new FooterColumn("Thông tin", new[] { new LinkSetting("Tin tức", "/tin-tuc"), new LinkSetting("Văn bản", "/van-ban") }),
            new FooterColumn("Tiện ích", new[] { new LinkSetting("Thư viện", "/thu-vien"), new LinkSetting("Liên hệ", "/lien-he"), new LinkSetting("Thi trực tuyến", "/thi") }),
        }, "Nội dung mẫu, sẽ thay bằng thông tin chính thức."),
        new SocialInfo(Array.Empty<SocialLink>()),
        new UtilityInfo(new[] { new LinkSetting("Thi trực tuyến", "/thi"), new LinkSetting("Văn bản", "/van-ban"), new LinkSetting("Liên hệ", "/lien-he") }),
        new SeoInfo("Cổng thông tin Công đoàn Đường sắt Việt Nam (MẪU)", null));

    private readonly ICmsDbContext _db;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly IMediaUsageTracker _usages;

    public SettingsService(ICmsDbContext db, IAuditWriter audit, IClock clock, IMediaUsageTracker usages)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _usages = usages;
    }

    public static bool IsKnown(string key) => Keys.ContainsKey(key);

    public async Task<object> GetAsync(string key, CancellationToken ct = default)
    {
        if (!Keys.TryGetValue(key, out var type)) throw AppException.NotFound("Cấu hình không tồn tại.");
        var row = await _db.SiteSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Key == key, ct);
        return row is null ? DefaultFor(key) : JsonSerializer.Deserialize(row.Json, type, Json)!;
    }

    public async Task<object> SaveAsync(string key, JsonElement value, CancellationToken ct = default)
    {
        if (!Keys.TryGetValue(key, out var type)) throw AppException.NotFound("Cấu hình không tồn tại.");
        object parsed;
        try { parsed = value.Deserialize(type, Json) ?? throw AppException.Validation("Dữ liệu cấu hình trống."); }
        catch (JsonException) { throw AppException.Validation("Dữ liệu cấu hình không đúng định dạng."); }
        Validate(parsed);

        var json = JsonSerializer.Serialize(parsed, type, Json);
        var row = await _db.SiteSettings.SingleOrDefaultAsync(s => s.Key == key, ct);
        if (row is null) _db.SiteSettings.Add(row = new SiteSetting(key, json));
        else row.Json = json;
        row.UpdatedUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (parsed is SiteInfo site)
            await _usages.SetUsagesAsync("Setting", Guid.Empty, "Cấu hình website", site.LogoMediaId is { } m ? new[] { m } : Array.Empty<Guid>(), ct);
        await _audit.WriteAsync("cms.settings.update", "SiteSetting", key, parsed, ct);
        return parsed;
    }

    public async Task<PublicSite> PublicAsync(CancellationToken ct = default)
    {
        var rows = await _db.SiteSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Json, ct);
        T Read<T>(string key, T fallback) => rows.TryGetValue(key, out var j) ? JsonSerializer.Deserialize<T>(j, Json) ?? fallback : fallback;
        var site = Read("site", Defaults.Site);
        site = site with { LogoUrl = site.LogoMediaId is null ? null : $"/media/{site.LogoMediaId}" };
        return new PublicSite(site, Read("contact", Defaults.Contact), Read("footer", Defaults.Footer), Read("social", Defaults.Social),
            Read("utility", Defaults.Utility), Read("seo", Defaults.Seo));
    }

    private static object DefaultFor(string key) => key switch
    {
        "site" => Defaults.Site,
        "contact" => Defaults.Contact,
        "footer" => Defaults.Footer,
        "social" => Defaults.Social,
        "utility" => Defaults.Utility,
        _ => Defaults.Seo,
    };

    private static void Validate(object value)
    {
        switch (value)
        {
            case SiteInfo s when string.IsNullOrWhiteSpace(s.Name):
                throw AppException.Validation("Tên website là bắt buộc.");
            case ContactInfo c:
                CmsItemOps.EnsureSafeUrl(c.MapUrl);
                break;
            case FooterInfo f:
                foreach (var col in f.Columns ?? Array.Empty<FooterColumn>())
                foreach (var l in col.Links ?? Array.Empty<LinkSetting>())
                    CmsItemOps.EnsureSafeUrl(l.Url, required: true);
                break;
            case SocialInfo so:
                foreach (var l in so.Links ?? Array.Empty<SocialLink>()) CmsItemOps.EnsureSafeUrl(l.Url, required: true);
                break;
            case UtilityInfo u:
                foreach (var l in u.Links ?? Array.Empty<LinkSetting>()) CmsItemOps.EnsureSafeUrl(l.Url, required: true);
                break;
        }
    }
}
