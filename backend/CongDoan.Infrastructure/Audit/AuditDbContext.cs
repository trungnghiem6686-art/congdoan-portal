using CongDoan.Application.Shared;
using CongDoan.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CongDoan.Infrastructure.Audit;

public sealed class AuditDbContext : DbContext, IAuditDbContext
{
    public const string Schema = "audit";

    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);
        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.Entity).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            e.Property(x => x.ActorName).HasMaxLength(200);
            e.Property(x => x.ActorKind).HasMaxLength(20);
            e.Property(x => x.RequestId).HasMaxLength(64);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => x.AtUtc);
            e.HasIndex(x => new { x.Entity, x.EntityId });
            e.HasIndex(x => x.ActorId);
        });
    }
}

internal sealed class AuditDesignFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args) => new(DesignTime.Options<AuditDbContext>(AuditDbContext.Schema));
}
