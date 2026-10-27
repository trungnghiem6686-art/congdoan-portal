using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Application.Cms;
using CongDoan.Infrastructure.Audit;
using CongDoan.Infrastructure.Cms;
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

        services.AddModuleDbContext<AuditDbContext>(conn, AuditDbContext.Schema);
        services.AddScoped<IAuditDbContext>(sp => sp.GetRequiredService<AuditDbContext>());
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<AuditQueryService>();

        services.AddModuleDbContext<IdentityDbContext>(conn, IdentityDbContext.Schema);
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddScoped<AuthService>();
        services.AddMemoryCache();
        services.AddSingleton<PermissionCatalog>();
        services.AddScoped<IPermissionChecker, PermissionService>();
        services.AddScoped<AdminUserService>();
        services.AddScoped<CandidateService>();
        services.AddScoped<OrgUnitService>();
        services.AddScoped<CandidateGroupService>();
        services.AddScoped<RoleService>();
        services.AddScoped<IdentitySeeder>();

        services.Configure<MediaOptions>(config.GetSection("Media"));
        services.AddModuleDbContext<CmsDbContext>(conn, CmsDbContext.Schema);
        services.AddScoped<ICmsDbContext>(sp => sp.GetRequiredService<CmsDbContext>());
        services.AddSingleton<IFileStore, LocalFileStore>();
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
        services.AddScoped<MediaService>();
        services.AddScoped<IMediaUsageTracker>(sp => sp.GetRequiredService<MediaService>());
        services.Configure<CmsOptions>(config.GetSection("Cms"));
        services.AddSingleton<IContentSanitizer, ContentSanitizer>();
        services.AddScoped<RedirectService>();
        services.AddScoped<ISlugHistory>(sp => sp.GetRequiredService<RedirectService>());
        services.AddScoped<MenuService>();
        services.AddScoped<BannerService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<AlbumService>();
        services.AddScoped<PageService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<PublicSiteService>();
        services.AddScoped<CmsSeeder>();
        services.AddScoped<PostService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<PublicPostService>();
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

    /// <summary>Idempotent reference data (default roles, optional bootstrap admin from configuration).</summary>
    public static async Task SeedAsync(IServiceProvider root, IConfiguration config)
    {
        using var scope = root.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>()
            .SeedAsync(config["Bootstrap:AdminUserName"], config["Bootstrap:AdminPassword"]);
        await scope.ServiceProvider.GetRequiredService<CmsSeeder>().SeedAsync();
    }
}
