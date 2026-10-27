using CongDoan.Application.Identity;
using CongDoan.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongDoan.Infrastructure.Identity;

public sealed class IdentityDbContext : DbContext, IIdentityDbContext
{
    public const string Schema = "identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AdminUserRole> AdminUserRoles => Set<AdminUserRole>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<CandidateGroup> CandidateGroups => Set<CandidateGroup>();
    public DbSet<CandidateGroupMember> CandidateGroupMembers => Set<CandidateGroupMember>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);
        b.Entity<AdminUser>(e => { MapAccount(e); e.ToTable("AdminUsers"); });
        b.Entity<Candidate>(e =>
        {
            MapAccount(e);
            e.ToTable("Candidates");
            e.Property(x => x.Phone).HasMaxLength(30);
            e.HasIndex(x => x.MemberId);
        });
        b.Entity<Session>(e =>
        {
            e.ToTable("Sessions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Kind, x.AccountId });
        });
        IdentityModel.Configure(b);
    }

    private static void MapAccount<T>(EntityTypeBuilder<T> e) where T : Account
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.UserName).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.UserName).IsUnique();
        e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        e.Property(x => x.Email).HasMaxLength(200);
        e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        e.Property(x => x.RowVersion).IsRowVersion();
        e.HasIndex(x => x.OrgUnitId);
    }
}

/// <summary>Extension point so later identity features (RBAC, org units) add mappings in their own file.</summary>
public static partial class IdentityModel
{
    static partial void ConfigureRbac(ModelBuilder b);

    public static void Configure(ModelBuilder b) => ConfigureRbac(b);
}
