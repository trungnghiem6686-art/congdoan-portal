using CongDoan.Application.Identity;
using CongDoan.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Testing;

public static class Seed
{
    public const string Password = "Passw0rd!x";

    public static async Task<AdminUser> AdminAsync(this ApiFactory f, string userName, bool mustChange = false, params string[] roles)
    {
        return await f.ScopedAsync(async sp =>
        {
            var db = sp.GetRequiredService<IIdentityDbContext>();
            var hasher = sp.GetRequiredService<IPasswordService>();
            var user = new AdminUser(userName, userName, hasher.Hash(Password), f.Clock.UtcNow) { MustChangePassword = mustChange };
            db.AdminUsers.Add(user);
            await db.SaveChangesAsync();
            foreach (var r in roles) await SeedRoleLink(sp, user.Id, r);
            return user;
        });
    }

    public static async Task<Candidate> CandidateAsync(this ApiFactory f, string code, bool mustChange = false, Guid? orgUnitId = null)
    {
        return await f.ScopedAsync(async sp =>
        {
            var db = sp.GetRequiredService<IIdentityDbContext>();
            var hasher = sp.GetRequiredService<IPasswordService>();
            var c = new Candidate(code, "Thí sinh " + code, hasher.Hash(Password), f.Clock.UtcNow)
            { MustChangePassword = mustChange, OrgUnitId = orgUnitId };
            db.Candidates.Add(c);
            await db.SaveChangesAsync();
            return c;
        });
    }

    // Overridden in Task 3 once roles exist; kept as a hook so Task 2 tests compile without RBAC.
    private static Task SeedRoleLink(IServiceProvider sp, Guid userId, string role) =>
        RoleSeeder?.Invoke(sp, userId, role) ?? Task.CompletedTask;

    public static Func<IServiceProvider, Guid, string, Task>? RoleSeeder { get; set; }

    public static async Task<TestClient> LoginAdminAsync(this ApiFactory f, string userName, string password = Password)
    {
        var c = f.CreateTestClient();
        var res = await c.PostAsync("/api/admin/auth/login", new { userName, password });
        await TestClient.DataAsync(res);
        await c.CsrfAsync(refresh: true);
        return c;
    }

    public static async Task<TestClient> LoginCandidateAsync(this ApiFactory f, string userName, string password = Password)
    {
        var c = f.CreateTestClient();
        var res = await c.PostAsync("/api/thi/auth/login", new { userName, password });
        await TestClient.DataAsync(res);
        await c.CsrfAsync(refresh: true);
        return c;
    }
}
