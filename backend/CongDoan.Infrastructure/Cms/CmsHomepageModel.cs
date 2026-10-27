using CongDoan.Application.Cms;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Infrastructure.Cms;

public sealed partial class CmsDbContext
{
    private readonly ICacheInvalidator? _invalidator;

    public CmsDbContext(DbContextOptions<CmsDbContext> options, ICacheInvalidator invalidator) : base(options) => _invalidator = invalidator;

    public DbSet<LayoutRevision> LayoutRevisions => Set<LayoutRevision>();

    public async Task InTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await Database.BeginTransactionAsync(ct);
            await work();
            await tx.CommitAsync(ct);
        });
        await FlushInvalidationsAsync(ct);
    }

    /// <summary>After any CMS write, tell the frontend which cache tags are stale (publish without redeploy).</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var tags = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .SelectMany(e => TagsFor(e.Entity)).ToHashSet();
        var result = await base.SaveChangesAsync(ct);
        if (_invalidator is not null && Database.CurrentTransaction is null)
            foreach (var tag in tags) await _invalidator.InvalidateAsync(tag, ct);
        else if (_invalidator is not null)
            _pendingTags.UnionWith(tags);
        return result;
    }

    private readonly HashSet<string> _pendingTags = new();

    /// <summary>Flushes tags collected inside a transaction once it has committed.</summary>
    public async Task FlushInvalidationsAsync(CancellationToken ct = default)
    {
        if (_invalidator is null) return;
        foreach (var tag in _pendingTags) await _invalidator.InvalidateAsync(tag, ct);
        _pendingTags.Clear();
    }

    private static IEnumerable<string> TagsFor(object entity) => entity switch
    {
        Post or PostTag or PostAttachment or Category or Tag => new[] { "posts", "homepage" },
        Menu or MenuItem => new[] { "menus" },
        Banner => new[] { "banners", "homepage" },
        Document => new[] { "documents", "homepage" },
        Album or AlbumItem => new[] { "albums", "homepage" },
        Page => new[] { "pages" },
        SiteSetting => new[] { "site" },
        LayoutRevision r => r.Target == LayoutRevision.Homepage ? new[] { "homepage" } : new[] { "pages" },
        Redirect => new[] { "redirects" },
        _ => Array.Empty<string>(),
    };

    partial void ConfigureHomepage(ModelBuilder b)
    {
        b.Entity<LayoutRevision>(e =>
        {
            e.ToTable("LayoutRevisions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Target).HasMaxLength(100).IsRequired();
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.Target, x.Version }).IsUnique().HasFilter("[Version] > 0");
            e.HasIndex(x => x.Target).IsUnique().HasFilter("[Status] = 0").HasDatabaseName("UX_Layout_OneDraft");
            e.HasIndex(x => x.Target).IsUnique().HasFilter("[Status] = 1").HasDatabaseName("UX_Layout_OnePublished");
        });
    }
}
