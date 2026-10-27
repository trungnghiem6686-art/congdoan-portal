using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CongDoan.Testing;

/// <summary>
/// Boots the real API against a throw-away SQL Server database (one per factory instance).
/// Connection comes from CONGDOAN_TEST_SQL; the default targets the local dev container only.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultServer =
        "Server=localhost,1433;User Id=sa;Password=Dev_Local_Only_123!;TrustServerCertificate=True";

    public FakeClock Clock { get; } = new(new DateTime(2026, 3, 1, 1, 0, 0, DateTimeKind.Utc));
    public string DatabaseName { get; } = "cd_test_" + Guid.NewGuid().ToString("N")[..12];
    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), "cd_media_" + Guid.NewGuid().ToString("N")[..8]);

    public static string ServerConnection =>
        Environment.GetEnvironmentVariable("CONGDOAN_TEST_SQL") is { Length: > 0 } s ? s : DefaultServer;

    public string ConnectionString =>
        new SqlConnectionStringBuilder(ServerConnection) { InitialCatalog = DatabaseName }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("RateLimit:LoginPermitPerMinute", "1000");
        builder.UseSetting("RateLimit:ContactPermitPerMinute", "1000");
        builder.UseSetting("Media:RootPath", MediaRoot);
        builder.UseSetting("Workers:Enabled", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }

    public T Scoped<T>(Func<IServiceProvider, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider);
    }

    public async Task<T> ScopedAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public async Task ScopedAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider);
    }

    public TestClient CreateTestClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        try
        {
            SqlConnection.ClearAllPools();
            using var conn = new SqlConnection(ServerConnection);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"IF DB_ID('{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Best effort cleanup of a throw-away test database.
        }
        try { if (Directory.Exists(MediaRoot)) Directory.Delete(MediaRoot, true); } catch { }
    }
}
