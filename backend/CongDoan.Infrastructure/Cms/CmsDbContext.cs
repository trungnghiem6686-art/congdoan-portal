using CongDoan.Application.Cms;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CongDoan.Infrastructure.Cms;

public sealed partial class CmsDbContext : DbContext, ICmsDbContext
{
    public const string Schema = "cms";

    public CmsDbContext(DbContextOptions<CmsDbContext> options) : base(options) { }

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<MediaUpload> MediaUploads => Set<MediaUpload>();
    public DbSet<MediaUsage> MediaUsages => Set<MediaUsage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);
        b.Entity<MediaAsset>(e =>
        {
            e.ToTable("MediaAssets");
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalName).HasMaxLength(255).IsRequired();
            e.Property(x => x.StoredPath).HasMaxLength(300).IsRequired();
            e.HasIndex(x => x.StoredPath).IsUnique();
            e.Property(x => x.ThumbnailPath).HasMaxLength(300);
            e.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
            e.Property(x => x.Alt).HasMaxLength(500);
            e.Property(x => x.Caption).HasMaxLength(1000);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.IsDeleted, x.CreatedUtc });
        });
        b.Entity<MediaUpload>(e =>
        {
            e.ToTable("MediaUploads");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        });
        b.Entity<MediaUsage>(e =>
        {
            e.ToTable("MediaUsages");
            e.HasKey(x => new { x.MediaId, x.OwnerType, x.OwnerId });
            e.Property(x => x.OwnerType).HasMaxLength(50);
            e.Property(x => x.OwnerTitle).HasMaxLength(500);
            e.HasIndex(x => new { x.OwnerType, x.OwnerId });
            e.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.Restrict);
        });
        ConfigureContent(b);
    }

    partial void ConfigureContent(ModelBuilder b);
}

internal sealed class CmsDesignFactory : IDesignTimeDbContextFactory<CmsDbContext>
{
    public CmsDbContext CreateDbContext(string[] args) => new(DesignTime.Options<CmsDbContext>(CmsDbContext.Schema));
}
