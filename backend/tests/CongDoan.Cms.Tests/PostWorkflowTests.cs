using System.Net;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Cms.Tests;

public class PostWorkflowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public PostWorkflowTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Editor_Cannot_Skip_To_Published()
    {
        using var editor = await _f.AdminWithRoleAsync(Roles.Editor);
        var post = await editor.CreatePostAsync(CmsHelpers.Unique("Tin biên tập"));
        var id = post.GetProperty("id").GetGuid();
        Assert.Equal("Draft", post.GetProperty("status").GetString());

        var skip = await editor.PostAsync($"/api/admin/posts/{id}/transition", new { to = "Published" });
        Assert.Equal(HttpStatusCode.Forbidden, skip.StatusCode);
        var approve = await editor.PostAsync($"/api/admin/posts/{id}/transition", new { to = "Approved" });
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);

        var submit = await TestClient.DataAsync(await editor.PostAsync($"/api/admin/posts/{id}/transition", new { to = "PendingReview" }));
        Assert.Equal("PendingReview", submit.GetProperty("status").GetString());

        using var reviewer = await _f.AdminWithRoleAsync(Roles.Reviewer);
        var approved = await TestClient.DataAsync(await reviewer.PostAsync($"/api/admin/posts/{id}/transition", new { to = "Approved" }));
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        var published = await reviewer.PublishAsync(id);
        Assert.Equal("Published", published.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Invalid_Transition_Rejected_Even_For_Publisher()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var id = (await admin.CreatePostAsync(CmsHelpers.Unique("Tin lưu trữ"))).GetProperty("id").GetGuid();
        var res = await admin.PostAsync($"/api/admin/posts/{id}/transition", new { to = "Archived" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_transition", await TestClient.ErrorCodeAsync(res));
    }

    [Fact]
    public async Task Concurrent_Edit_With_Stale_RowVersion_Returns_409()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var post = await admin.CreatePostAsync(CmsHelpers.Unique("Tin đồng thời"));
        var id = post.GetProperty("id").GetGuid();
        var version = post.GetProperty("rowVersion").GetString();

        var first = await admin.PutAsync($"/api/admin/posts/{id}", new { title = post.GetProperty("title").GetString(), body = "<p>Bản A</p>", rowVersion = version });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await admin.PutAsync($"/api/admin/posts/{id}", new { title = post.GetProperty("title").GetString(), body = "<p>Bản B</p>", rowVersion = version });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("concurrency_conflict", await TestClient.ErrorCodeAsync(second));

        var current = await TestClient.DataAsync(await admin.GetAsync($"/api/admin/posts/{id}"));
        Assert.Contains("Bản A", current.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Draft_Not_Visible_On_Public_Endpoint()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var post = await admin.CreatePostAsync(CmsHelpers.Unique("Tin nháp công khai"));
        var slug = post.GetProperty("slug").GetString();
        using var anon = _f.CreateTestClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/posts/{slug}")).StatusCode);
        var list = await TestClient.DataAsync(await anon.GetAsync("/api/public/posts?pageSize=100"));
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), p => p.GetProperty("slug").GetString() == slug);

        await admin.PublishAsync(post.GetProperty("id").GetGuid());
        var pub = await TestClient.DataAsync(await anon.GetAsync($"/api/public/posts/{slug}"));
        Assert.Equal(post.GetProperty("title").GetString(), pub.GetProperty("title").GetString());
        Assert.False(pub.TryGetProperty("rowVersion", out _));
    }

    [Fact]
    public async Task Script_Tag_Stripped_From_Body()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var post = await admin.CreatePostAsync(CmsHelpers.Unique("Tin xss"),
            "<p onclick=\"alert(1)\">Xin chào</p><script>alert('x')</script><img src=\"javascript:alert(1)\"><a href=\"javascript:alert(2)\">link</a><figure><img src=\"/media/00000000-0000-0000-0000-000000000001/a.png\" alt=\"Ảnh\"><figcaption>Chú thích</figcaption></figure>");
        var body = post.GetProperty("body").GetString()!;
        Assert.DoesNotContain("<script", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Xin chào", body);
        Assert.Contains("<figcaption>Chú thích</figcaption>", body);
    }

    [Fact]
    public async Task Soft_Delete_And_Restore()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var post = await admin.CreatePostAsync(CmsHelpers.Unique("Tin thùng rác"));
        var id = post.GetProperty("id").GetGuid();
        var slug = post.GetProperty("slug").GetString();
        await admin.PublishAsync(id);
        using var anon = _f.CreateTestClient();

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/admin/posts/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/posts/{slug}")).StatusCode);
        var trash = await TestClient.DataAsync(await admin.GetAsync("/api/admin/posts?deleted=true&pageSize=200"));
        Assert.Contains(trash.GetProperty("items").EnumerateArray(), p => p.GetProperty("id").GetGuid() == id);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/posts/{id}/restore")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/public/posts/{slug}")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_Slug_Rejected()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var title = CmsHelpers.Unique("Hội nghị Công đoàn");
        var first = await admin.CreatePostAsync(title);
        var res = await admin.PostAsync("/api/admin/posts", new { title = "Khác", body = "<p>x</p>", slug = first.GetProperty("slug").GetString() });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("duplicate_slug", await TestClient.ErrorCodeAsync(res));
        Assert.Contains(first.GetProperty("slug").GetString() + "-2", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Publish_Writes_AuditRow_With_Actor()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var id = (await admin.CreatePostAsync(CmsHelpers.Unique("Tin kiểm toán"))).GetProperty("id").GetGuid();
        await admin.PublishAsync(id);
        var me = await TestClient.DataAsync(await admin.GetAsync("/api/admin/auth/me"));

        var row = await _f.ScopedAsync(sp => sp.GetRequiredService<IAuditDbContext>().AuditLogs
            .SingleAsync(a => a.Action == "cms.post.transition" && a.EntityId == id.ToString()));
        Assert.Equal(me.GetProperty("id").GetGuid(), row.ActorId);
        Assert.Contains("Published", row.Detail);
    }

    [Fact]
    public async Task Editor_Cannot_Edit_Someone_Elses_Post()
    {
        using var a = await _f.AdminWithRoleAsync(Roles.Editor);
        using var b = await _f.AdminWithRoleAsync(Roles.Editor);
        var post = await a.CreatePostAsync(CmsHelpers.Unique("Tin của A"));
        var res = await b.PutAsync($"/api/admin/posts/{post.GetProperty("id").GetGuid()}",
            new { title = "Sửa trộm", body = "<p>x</p>", rowVersion = post.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
