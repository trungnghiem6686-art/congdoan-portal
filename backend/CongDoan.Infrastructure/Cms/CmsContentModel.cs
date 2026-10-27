using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Infrastructure.Cms;

public sealed partial class CmsDbContext
{
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();

    public void Detach(object entity) => Entry(entity).State = EntityState.Detached;

    public void ExpectRowVersion(object entity, byte[] rowVersion) =>
        Entry(entity).Property("RowVersion").OriginalValue = rowVersion;

    partial void ConfigureContent(ModelBuilder b)
    {
        b.Entity<Post>(e =>
        {
            e.ToTable("Posts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Summary).HasMaxLength(2000);
            e.Property(x => x.SourceKey).HasMaxLength(300);
            e.HasIndex(x => x.SourceKey).IsUnique().HasFilter("[SourceKey] IS NOT NULL");
            e.Property(x => x.ReviewNote).HasMaxLength(1000);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.Status, x.IsDeleted, x.PublishedUtc });
            e.HasIndex(x => x.CategoryId);
            e.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.CoverMediaId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Tags).WithOne().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<PostTag>(e =>
        {
            e.ToTable("PostTags");
            e.HasKey(x => new { x.PostId, x.TagId });
            e.HasOne<Tag>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PostAttachment>(e =>
        {
            e.ToTable("PostAttachments");
            e.HasKey(x => new { x.PostId, x.MediaId });
            e.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Tag>(e =>
        {
            e.ToTable("Tags");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
        });
        ConfigureSite(b);
    }

    partial void ConfigureSite(ModelBuilder b);
}
