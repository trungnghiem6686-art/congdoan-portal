using System.Net;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Shared.Tests;

public class AuditTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public AuditTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Audit_Redacts_Password_And_CorrectAnswer_Fields()
    {
        var entityId = Guid.NewGuid().ToString();
        await _f.ScopedAsync(sp => sp.GetRequiredService<IAuditWriter>().WriteAsync("test.redact", "Probe", entityId, new
        {
            userName = "visible-name",
            password = "pw-SECRET-1",
            newPassword = "pw-SECRET-2",
            token = "tok-SECRET-3",
            choices = new[] { new { text = "Đáp án A", isCorrect = true } },
            correctChoiceIds = new[] { "SECRET-4" },
            explanation = "SECRET-5 lời giải",
        }));

        var row = await _f.ScopedAsync(sp => sp.GetRequiredService<IAuditDbContext>().AuditLogs.SingleAsync(a => a.EntityId == entityId));
        Assert.Contains("visible-name", row.Detail);
        Assert.Contains("Đáp án A", row.Detail);
        Assert.DoesNotContain("SECRET", row.Detail);
        Assert.DoesNotContain("true", row.Detail);
        Assert.Equal(_f.Clock.UtcNow, row.AtUtc);
    }

    [Fact]
    public async Task Publish_Writes_AuditRow_With_Actor()
    {
        // Admin state-changing action (account lock) stands in until CMS publish exists (Task 6 asserts publish itself).
        var actor = await _f.AdminAsync("audit-actor", false, Roles.SystemAdmin);
        var target = await _f.AdminAsync("audit-target", false, Roles.Editor);
        using var c = await _f.LoginAdminAsync("audit-actor");

        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/admin/users/{target.Id}/lock")).StatusCode);

        var row = await _f.ScopedAsync(sp => sp.GetRequiredService<IAuditDbContext>().AuditLogs
            .SingleAsync(a => a.Action == "identity.user.lock" && a.EntityId == target.Id.ToString()));
        Assert.Equal(actor.Id, row.ActorId);
        Assert.Equal("audit-actor", row.ActorName);
        Assert.False(string.IsNullOrEmpty(row.RequestId));

        var list = await TestClient.DataAsync(await c.GetAsync($"/api/admin/audit?entityId={target.Id}"));
        Assert.Equal(1, list.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Failed_Login_Is_Audited_Without_Password()
    {
        await _f.AdminAsync("audit-fail");
        using var c = _f.CreateTestClient();
        await c.PostAsync("/api/admin/auth/login", new { userName = "audit-fail", password = "wrong-SECRET-pw" });

        var row = await _f.ScopedAsync(sp => sp.GetRequiredService<IAuditDbContext>().AuditLogs
            .FirstAsync(a => a.Action == "auth.login.failed" && a.Detail.Contains("audit-fail")));
        Assert.DoesNotContain("SECRET", row.Detail);
    }
}
