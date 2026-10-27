using CongDoan.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Infrastructure.Identity;

public static partial class IdentityModel
{
    static partial void ConfigureRbac(ModelBuilder b)
    {
        b.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasMany(x => x.Permissions).WithOne().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermissions");
            e.HasKey(x => new { x.RoleId, x.Permission });
            e.Property(x => x.Permission).HasMaxLength(100);
        });
        b.Entity<AdminUserRole>(e =>
        {
            e.ToTable("AdminUserRoles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AdminUserId, x.RoleId, x.OrgUnitId }).IsUnique();
            e.HasOne<AdminUser>().WithMany().HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OrgUnit>(e =>
        {
            e.ToTable("OrgUnits");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(300).IsRequired();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CandidateGroup>(e =>
        {
            e.ToTable("CandidateGroups");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CandidateGroupMember>(e =>
        {
            e.ToTable("CandidateGroupMembers");
            e.HasKey(x => new { x.GroupId, x.CandidateId });
            e.HasIndex(x => x.CandidateId);
            e.HasOne<CandidateGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Candidate>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Candidate>().HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AdminUser>().HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}
