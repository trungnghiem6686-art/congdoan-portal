using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public partial interface ICmsDbContext
{
    DbSet<Menu> Menus { get; }
    DbSet<MenuItem> MenuItems { get; }
    DbSet<Banner> Banners { get; }
    DbSet<Document> Documents { get; }
    DbSet<Album> Albums { get; }
    DbSet<AlbumItem> AlbumItems { get; }
    DbSet<Page> Pages { get; }
    DbSet<SiteSetting> SiteSettings { get; }
    DbSet<Redirect> Redirects { get; }
    DbSet<ContactRequest> ContactRequests { get; }
}

/// <summary>Hide/show, trash/restore shared by every CMS item type.</summary>
public static class CmsItemOps
{
    public static async Task<T> LoadAsync<T>(this DbSet<T> set, Guid id, CancellationToken ct) where T : CmsItem =>
        await set.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();

    public static async Task SetVisibleAsync<T>(ICmsDbContext db, DbSet<T> set, IAuditWriter audit, Guid id, bool visible, CancellationToken ct) where T : CmsItem
    {
        var item = await set.LoadAsync(id, ct);
        item.IsVisible = visible;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync($"cms.{typeof(T).Name.ToLowerInvariant()}.{(visible ? "show" : "hide")}", typeof(T).Name, id.ToString(), null, ct);
    }

    public static async Task SetDeletedAsync<T>(ICmsDbContext db, DbSet<T> set, IAuditWriter audit, IClock clock, Guid id, bool deleted, CancellationToken ct) where T : CmsItem
    {
        var item = await set.LoadAsync(id, ct);
        item.IsDeleted = deleted;
        item.DeletedUtc = deleted ? clock.UtcNow : null;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync($"cms.{typeof(T).Name.ToLowerInvariant()}.{(deleted ? "delete" : "restore")}", typeof(T).Name, id.ToString(), null, ct);
    }

    public static void Touch(CmsItem item, IClock clock, bool created = false)
    {
        if (created) item.CreatedUtc = clock.UtcNow;
        item.UpdatedUtc = clock.UtcNow;
    }

    public static void EnsureSafeUrl(string? url, bool required = false)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            if (required) throw AppException.Validation("Đường dẫn liên kết là bắt buộc.");
            return;
        }
        if (!UrlValidator.IsSafe(url))
            throw new AppException("unsafe_url", $"Liên kết \"{url}\" không an toàn. Chỉ chấp nhận http(s), mailto, tel hoặc đường dẫn nội bộ bắt đầu bằng /.", 400);
    }
}
