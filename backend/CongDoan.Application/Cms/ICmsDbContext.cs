using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public partial interface ICmsDbContext
{
    DbSet<MediaAsset> MediaAssets { get; }
    DbSet<MediaUpload> MediaUploads { get; }
    DbSet<MediaUsage> MediaUsages { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
