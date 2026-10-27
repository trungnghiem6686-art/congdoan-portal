using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Infrastructure.Identity;
using CongDoan.Infrastructure.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CongDoan.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Default")
                   ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
        services.TryAddSingleton<IClock, SystemClock>();

        services.AddModuleDbContext<IdentityDbContext>(conn, IdentityDbContext.Schema);
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddScoped<AuthService>();
        return services;
    }

    /// <summary>Registers a module's DbContext in its own schema with its own migrations history table.</summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string conn, string schema)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(o => o.UseSqlServer(conn, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            sql.EnableRetryOnFailure(3);
        }));
        services.AddSingleton(new ModuleDatabase(typeof(TContext)));
        return services;
    }
}

public sealed record ModuleDatabase(Type ContextType);

public static class MigrationRunner
{
    public static async Task MigrateAllAsync(IServiceProvider root)
    {
        using var scope = root.CreateScope();
        foreach (var module in scope.ServiceProvider.GetServices<ModuleDatabase>())
        {
            var ctx = (DbContext)scope.ServiceProvider.GetRequiredService(module.ContextType);
            await ctx.Database.MigrateAsync();
        }
    }
}
