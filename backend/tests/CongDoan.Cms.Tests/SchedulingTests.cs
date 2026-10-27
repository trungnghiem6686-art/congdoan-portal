using System.Net;
using CongDoan.Application.Cms;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Cms.Tests;

public class SchedulingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public SchedulingTests(ApiFactory f) => _f = f;

    private Task<int> RunOnceAsync() => _f.ScopedAsync(sp => sp.GetRequiredService<ScheduledPublisher>().RunOnceAsync());

    [Fact]
    public async Task Scheduled_Post_Publishes_When_Clock_Passes()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var post = await admin.CreatePostAsync(CmsHelpers.Unique("Hẹn giờ"));
        var id = post.GetProperty("id").GetGuid();
        var at = _f.Clock.UtcNow.AddHours(2);
        await TestClient.DataAsync(await admin.PostAsync($"/api/admin/posts/{id}/transition", new { to = "Scheduled", scheduledUtc = at }));
        using var anon = _f.CreateTestClient();
        var slug = post.GetProperty("slug").GetString();

        await RunOnceAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/posts/{slug}")).StatusCode);

        _f.Clock.Advance(TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(1)));
        Assert.True(await RunOnceAsync() >= 1);
        var pub = await TestClient.DataAsync(await anon.GetAsync($"/api/public/posts/{slug}"));
        Assert.Equal(at, pub.GetProperty("publishedUtc").GetDateTime().ToUniversalTime(), TimeSpan.FromSeconds(1));
        var admView = await TestClient.DataAsync(await admin.GetAsync($"/api/admin/posts/{id}"));
        Assert.Equal("Published", admView.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Worker_Restart_Does_Not_Double_Publish()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var id = (await admin.CreatePostAsync(CmsHelpers.Unique("Một lần"))).GetProperty("id").GetGuid();
        await TestClient.DataAsync(await admin.PostAsync($"/api/admin/posts/{id}/transition",
            new { to = "Scheduled", scheduledUtc = _f.Clock.UtcNow.AddMinutes(10) }));
        _f.Clock.Advance(TimeSpan.FromMinutes(11));

        // Two workers (e.g. old and new process during a restart) racing on the same due post.
        await Task.WhenAll(RunOnceAsync(), RunOnceAsync(), RunOnceAsync());
        await RunOnceAsync();

        var rows = await _f.ScopedAsync(sp => sp.GetRequiredService<IAuditDbContext>().AuditLogs
            .CountAsync(a => a.Action == "cms.post.autopublish" && a.EntityId == id.ToString()));
        Assert.Equal(1, rows);
    }
}
