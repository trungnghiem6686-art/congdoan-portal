using CongDoan.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CongDoan.Infrastructure;

// Used only by `dotnet ef migrations add`; never connects at design time.
internal static class DesignTime
{
    public const string Conn = "Server=localhost;Database=CongDoan_Design;Trusted_Connection=True;TrustServerCertificate=True";

    public static DbContextOptions<T> Options<T>(string schema) where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseSqlServer(Conn, s => s.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .Options;
}

internal sealed class IdentityDesignFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args) => new(DesignTime.Options<IdentityDbContext>(IdentityDbContext.Schema));
}
