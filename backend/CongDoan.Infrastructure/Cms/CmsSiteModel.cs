using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongDoan.Infrastructure.Cms;

public sealed partial class CmsDbContext
{
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<AlbumItem> AlbumItems => Set<AlbumItem>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<Redirect> Redirects => Set<Redirect>();
    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();

    private static void Item<T>(EntityTypeBuilder<T> e, string table) where T : CmsItem
    {
        e.ToTable(table);
        e.HasKey(x => x.Id);
        e.Property(x => x.RowVersion).IsRowVersion();
        e.HasIndex(x => new { x.IsDeleted, x.IsVisible });
    }

    partial void ConfigureSite(ModelBuilder b)
    {
        b.Entity<Menu>(e =>
        {
            e.ToTable("Menus");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
        b.Entity<MenuItem>(e =>
        {
            Item(e, "MenuItems");
            e.Property(x => x.Label).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Url).HasMaxLength(1000);
            e.HasIndex(x => new { x.MenuId, x.ParentId, x.SortOrder });
            e.HasOne<Menu>().WithMany().HasForeignKey(x => x.MenuId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<MenuItem>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Banner>(e =>
        {
            Item(e, "Banners");
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Position).HasMaxLength(50).IsRequired();
            e.Property(x => x.LinkUrl).HasMaxLength(1000);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasIndex(x => new { x.Position, x.SortOrder });
            e.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.ImageMediaId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Document>(e =>
        {
            Item(e, "Documents");
            e.Property(x => x.Number).HasMaxLength(100).IsRequired();
            e.Property(x => x.Title).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.DocType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Issuer).HasMaxLength(300);
            e.Property(x => x.Summary).HasMaxLength(4000);
            e.Property(x => x.SourceKey).HasMaxLength(300);
            e.HasIndex(x => x.SourceKey).IsUnique().HasFilter("[SourceKey] IS NOT NULL");
            e.HasIndex(x => x.Number);
            e.HasIndex(x => x.IssuedDate);
            e.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.FileMediaId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Album>(e =>
        {
            Item(e, "Albums");
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.CoverMediaId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AlbumItem>(e =>
        {
            e.ToTable("AlbumItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.EmbedUrl).HasMaxLength(1000);
            e.Property(x => x.Caption).HasMaxLength(1000);
            e.HasIndex(x => new { x.AlbumId, x.SortOrder });
            e.HasOne<Album>().WithMany().HasForeignKey(x => x.AlbumId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Page>(e =>
        {
            Item(e, "Pages");
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.SeoDescription).HasMaxLength(500);
        });
        b.Entity<SiteSetting>(e =>
        {
            e.ToTable("SiteSettings");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(50);
        });
        b.Entity<Redirect>(e =>
        {
            e.ToTable("Redirects");
            e.HasKey(x => x.Id);
            e.Property(x => x.OldPath).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.OldPath).IsUnique();
            e.Property(x => x.NewPath).HasMaxLength(500).IsRequired();
        });
        b.Entity<ContactRequest>(e =>
        {
            e.ToTable("ContactRequests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Subject).HasMaxLength(300);
            e.Property(x => x.Message).HasMaxLength(5000).IsRequired();
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.HandlerNote).HasMaxLength(2000);
            e.HasIndex(x => new { x.Status, x.CreatedUtc });
        });
        ConfigureHomepage(b);
    }

    partial void ConfigureHomepage(ModelBuilder b);
}
