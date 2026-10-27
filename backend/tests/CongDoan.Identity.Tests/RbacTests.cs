using System.Net;
using System.Text.Json;
using CongDoan.Api.Identity;
using CongDoan.Application.Identity;
using CongDoan.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Identity.Tests;

/// <summary>Test-only endpoint guarded by the publish permission (real post endpoints arrive with the CMS).</summary>
[Route("api/admin/_probe")]
public sealed class PublishProbeController : ControllerBase
{
    [HttpPost("publish")]
    [RequirePermission(Permissions.CmsPostPublish)]
    public IActionResult Publish() => Ok(new { success = true });
}

public sealed class RbacFactory : ApiFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s => s.AddControllers().AddApplicationPart(typeof(PublishProbeController).Assembly));
    }
}

public class RbacTests : IClassFixture<RbacFactory>
{
    private readonly RbacFactory _f;

    public RbacTests(RbacFactory f) => _f = f;

    [Fact]
    public async Task Editor_Cannot_Publish_Returns_403()
    {
        await _f.AdminAsync("editor1", false, Roles.Editor);
        await _f.AdminAsync("reviewer1", false, Roles.Reviewer);
        using var editor = await _f.LoginAdminAsync("editor1");
        using var reviewer = await _f.LoginAdminAsync("reviewer1");

        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsync("/api/admin/_probe/publish")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.PostAsync("/api/admin/_probe/publish")).StatusCode);
    }

    [Fact]
    public async Task Role_Change_Takes_Effect_On_Next_Request()
    {
        await _f.AdminAsync("sys-rc", false, Roles.SystemAdmin);
        var target = await _f.AdminAsync("promote-me", false, Roles.Editor);
        using var admin = await _f.LoginAdminAsync("sys-rc");
        using var user = await _f.LoginAdminAsync("promote-me");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync("/api/admin/_probe/publish")).StatusCode);

        var set = await admin.PutAsync($"/api/admin/users/{target.Id}/roles",
            new { roles = new[] { new { roleCode = Roles.Reviewer } } });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await user.PostAsync("/api/admin/_probe/publish")).StatusCode);
    }

    [Fact]
    public async Task Scoped_Role_Cannot_Access_Other_OrgUnit_Resource()
    {
        await _f.AdminAsync("sys-scope", false, Roles.SystemAdmin);
        using var sys = await _f.LoginAdminAsync("sys-scope");
        var unitA = (await TestClient.DataAsync(await sys.PostAsync("/api/admin/org-units", new { code = "A-" + Guid.NewGuid().ToString("N")[..6], name = "Đơn vị A" }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var unitB = (await TestClient.DataAsync(await sys.PostAsync("/api/admin/org-units", new { code = "B-" + Guid.NewGuid().ToString("N")[..6], name = "Đơn vị B" }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var inA = await _f.CandidateAsync("cand-a-" + Guid.NewGuid().ToString("N")[..6], orgUnitId: unitA);
        var inB = await _f.CandidateAsync("cand-b-" + Guid.NewGuid().ToString("N")[..6], orgUnitId: unitB);

        var mgr = await TestClient.DataAsync(await sys.PostAsync("/api/admin/users", new
        {
            userName = "mgr-a-" + Guid.NewGuid().ToString("N")[..6],
            displayName = "Quản lý thi đơn vị A",
            password = "Start1234x",
            roles = new[] { new { roleCode = Roles.ExamManager, orgUnitId = (Guid?)unitA } },
        }), HttpStatusCode.Created);
        using var m = _f.CreateTestClient();
        await TestClient.DataAsync(await m.PostAsync("/api/admin/auth/login", new { userName = mgr.GetProperty("userName").GetString(), password = "Start1234x" }));
        await m.CsrfAsync(true);
        await m.PostAsync("/api/admin/auth/change-password", new { currentPassword = "Start1234x", newPassword = "Changed1234x" });

        Assert.Equal(HttpStatusCode.OK, (await m.GetAsync($"/api/admin/candidates/{inA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await m.GetAsync($"/api/admin/candidates/{inB.Id}")).StatusCode);

        var list = await TestClient.DataAsync(await m.GetAsync("/api/admin/candidates?pageSize=200"));
        var ids = list.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(inA.Id, ids);
        Assert.DoesNotContain(inB.Id, ids);
    }

    [Fact]
    public async Task Admin_List_Search_Filters_By_Q()
    {
        await _f.AdminAsync("sys-q", false, Roles.SystemAdmin);
        using var admin = await _f.LoginAdminAsync("sys-q");
        var tag = Guid.NewGuid().ToString("N")[..6];
        await _f.CandidateAsync($"q-{tag}-one");
        await _f.CandidateAsync($"other-{Guid.NewGuid():N}"[..20]);

        var list = await TestClient.DataAsync(await admin.GetAsync($"/api/admin/candidates?q={tag}&pageSize=5"));
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(5, list.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Me_Permissions_Lists_Effective_Codes()
    {
        await _f.AdminAsync("perm-editor", false, Roles.Editor);
        using var c = await _f.LoginAdminAsync("perm-editor");
        var data = await TestClient.DataAsync(await c.GetAsync("/api/admin/auth/permissions"));
        var codes = data.EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains(Permissions.CmsPostCreate, codes);
        Assert.DoesNotContain(Permissions.CmsPostPublish, codes);
    }

    [Fact]
    public async Task Locking_User_Revokes_Their_Sessions()
    {
        await _f.AdminAsync("sys-lock", false, Roles.SystemAdmin);
        var victim = await _f.AdminAsync("victim", false, Roles.Editor);
        using var admin = await _f.LoginAdminAsync("sys-lock");
        using var v = await _f.LoginAdminAsync("victim");

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/users/{victim.Id}/lock")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await v.GetAsync("/api/admin/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Candidate_Import_Is_All_Or_Nothing_With_Row_Errors()
    {
        await _f.AdminAsync("sys-imp", false, Roles.SystemAdmin);
        using var admin = await _f.LoginAdminAsync("sys-imp");
        var tag = Guid.NewGuid().ToString("N")[..6];
        var csv = $"ma,ho_ten,email\nimp-{tag}-1,Nguyễn Văn A,a@example.com\n,Thiếu mã,\nimp-{tag}-1,Trùng mã,\n";

        var bad = await admin.PostAsync("/api/admin/candidates/import", new { csv });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var body = JsonDocument.Parse(await bad.Content.ReadAsStringAsync()).RootElement;
        var rows = body.GetProperty("data").GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("row").GetInt32()).ToList();
        Assert.Contains(3, rows);
        Assert.Contains(4, rows);
        var count = await _f.ScopedAsync(sp => sp.GetRequiredService<IIdentityDbContext>().Candidates.CountAsync(c => c.UserName.StartsWith("imp-" + tag)));
        Assert.Equal(0, count);

        var good = await TestClient.DataAsync(await admin.PostAsync("/api/admin/candidates/import",
            new { csv = $"ma,ho_ten,email\nimp-{tag}-1,Nguyễn Văn A,a@example.com\nimp-{tag}-2,Trần Thị B,\n" }));
        Assert.Equal(2, good.GetProperty("created").GetInt32());
        Assert.All(good.GetProperty("credentials").EnumerateArray(), x => Assert.True(x.GetProperty("password").GetString()!.Length >= 10));
    }
}

/// <summary>Own database so no other system admin exists.</summary>
public class LastSystemAdminTests : IClassFixture<RbacFactory>
{
    private readonly RbacFactory _f;

    public LastSystemAdminTests(RbacFactory f) => _f = f;

    [Fact]
    public async Task Cannot_Lock_Or_Delete_Last_SystemAdmin()
    {
        var root = await _f.AdminAsync("root-only", false, Roles.SystemAdmin);
        using var c = await _f.LoginAdminAsync("root-only");

        var lockRes = await c.PostAsync($"/api/admin/users/{root.Id}/lock");
        Assert.Equal(HttpStatusCode.Conflict, lockRes.StatusCode);
        Assert.Equal("last_system_admin", await TestClient.ErrorCodeAsync(lockRes));

        var del = await c.DeleteAsync($"/api/admin/users/{root.Id}");
        Assert.Equal("last_system_admin", await TestClient.ErrorCodeAsync(del));

        var demote = await c.PutAsync($"/api/admin/users/{root.Id}/roles", new { roles = new[] { new { roleCode = Roles.Editor } } });
        Assert.Equal("last_system_admin", await TestClient.ErrorCodeAsync(demote));
    }
}
