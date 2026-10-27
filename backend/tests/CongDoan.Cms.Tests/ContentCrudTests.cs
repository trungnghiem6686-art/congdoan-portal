using System.Net;
using System.Text.Json;
using CongDoan.Application.Cms;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Cms.Tests;

public class SlugTests
{
    [Theory]
    [InlineData("Giới thiệu", "gioi-thieu")]
    [InlineData("Công đoàn Đường sắt Việt Nam", "cong-doan-duong-sat-viet-nam")]
    [InlineData("  Tin tức – Sự kiện!! 2026 ", "tin-tuc-su-kien-2026")]
    [InlineData("Ủy ban Kiểm tra", "uy-ban-kiem-tra")]
    public void Slug_Derived_From_Title_Without_Diacritics(string title, string expected) =>
        Assert.Equal(expected, Slug.From(title));

    [Theory]
    [InlineData("https://congdoan.vn/a", true)]
    [InlineData("/tin-tuc/abc", true)]
    [InlineData("mailto:lienhe@example.com", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData(" JavaScript:alert(1)", false)]
    [InlineData("data:text/html,<script>", false)]
    [InlineData("//evil.example", false)]
    [InlineData("vbscript:x", false)]
    public void UrlValidator_Allows_Only_Safe_Schemes(string url, bool safe) => Assert.Equal(safe, UrlValidator.IsSafe(url));
}

public class ContentCrudTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public ContentCrudTests(ApiFactory f) => _f = f;

    private Task<TestClient> AdminAsync() => _f.AdminWithRoleAsync(Roles.ContentAdmin);

    private static async Task<Guid> MainMenuAsync(TestClient c)
    {
        var menus = await TestClient.DataAsync(await c.GetAsync("/api/admin/menus"));
        return menus.EnumerateArray().Single(m => m.GetProperty("code").GetString() == "main").GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> AddItemAsync(TestClient c, Guid menuId, object body, HttpStatusCode expected = HttpStatusCode.Created) =>
        await TestClient.DataAsync(await c.PostAsync($"/api/admin/menus/{menuId}/items", body), expected);

    [Fact]
    public async Task Menu_Item_Slug_Derived_From_Label()
    {
        using var c = await AdminAsync();
        var menu = await MainMenuAsync(c);
        var tag = Guid.NewGuid().ToString("N")[..5];
        var item = await AddItemAsync(c, menu, new { label = $"Giới thiệu {tag}", linkType = "None" });
        Assert.Equal($"gioi-thieu-{tag}", item.GetProperty("slug").GetString());
        Assert.Equal($"/gioi-thieu-{tag}", item.GetProperty("path").GetString());
    }

    [Fact]
    public async Task Rename_Updates_Slug_And_Old_Url_Redirects_301()
    {
        using var c = await AdminAsync();
        var menu = await MainMenuAsync(c);
        var tag = Guid.NewGuid().ToString("N")[..5];
        var item = await AddItemAsync(c, menu, new { label = $"Hoạt động {tag}", linkType = "None" });
        var id = item.GetProperty("id").GetGuid();

        var renamed = await TestClient.DataAsync(await c.PutAsync($"/api/admin/menu-items/{id}",
            new { label = $"Phong trào {tag}", linkType = "None", rowVersion = item.GetProperty("rowVersion").GetString() }));
        Assert.Equal($"phong-trao-{tag}", renamed.GetProperty("slug").GetString());

        using var anon = _f.CreateTestClient();
        var resolved = await TestClient.DataAsync(await anon.GetAsync($"/api/public/resolve?path=/hoat-dong-{tag}"));
        Assert.Equal("redirect", resolved.GetProperty("type").GetString());
        Assert.Equal(301, resolved.GetProperty("status").GetInt32());
        Assert.Equal($"/phong-trao-{tag}", resolved.GetProperty("location").GetString());

        var current = await TestClient.DataAsync(await anon.GetAsync($"/api/public/resolve?path=/phong-trao-{tag}"));
        Assert.Equal("menu", current.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Renamed_Post_Old_Slug_Redirects_301()
    {
        using var c = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..5];
        var post = await c.CreatePostAsync($"Đại hội {tag}");
        await c.PublishAsync(post.GetProperty("id").GetGuid());
        var current = await TestClient.DataAsync(await c.GetAsync($"/api/admin/posts/{post.GetProperty("id").GetGuid()}"));
        await TestClient.DataAsync(await c.PutAsync($"/api/admin/posts/{post.GetProperty("id").GetGuid()}",
            new { title = $"Đại hội đại biểu {tag}", body = "<p>x</p>", rowVersion = current.GetProperty("rowVersion").GetString() }));

        using var anon = _f.CreateTestClient();
        var resolved = await TestClient.DataAsync(await anon.GetAsync($"/api/public/resolve?path=/tin-tuc/dai-hoi-{tag}"));
        Assert.Equal($"/tin-tuc/dai-hoi-dai-bieu-{tag}", resolved.GetProperty("location").GetString());
    }

    [Fact]
    public async Task Duplicate_Slug_Rejected_With_Suggestion()
    {
        using var c = await AdminAsync();
        var menu = await MainMenuAsync(c);
        var label = "Văn hóa " + Guid.NewGuid().ToString("N")[..5];
        var first = await AddItemAsync(c, menu, new { label, linkType = "None" });
        var dup = await c.PostAsync($"/api/admin/menus/{menu}/items", new { label, linkType = "None" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Contains(first.GetProperty("slug").GetString() + "-2", await dup.Content.ReadAsStringAsync());

        var reserved = await c.PostAsync($"/api/admin/menus/{menu}/items", new { label = "Tin tức", linkType = "None" });
        Assert.Equal("duplicate_slug", await TestClient.ErrorCodeAsync(reserved));
    }

    [Fact]
    public async Task Url_Menu_Item_With_Reserved_Name_Gets_Free_Slug()
    {
        using var c = await AdminAsync();
        var menu = await MainMenuAsync(c);
        var item = await AddItemAsync(c, menu, new { label = "Tìm kiếm", linkType = "Url", url = "/tim-kiem" });
        Assert.StartsWith("tim-kiem-", item.GetProperty("slug").GetString());
        var again = await AddItemAsync(c, menu, new { label = "Tìm kiếm", linkType = "Url", url = "/tim-kiem?q=x" });
        Assert.NotEqual(item.GetProperty("slug").GetString(), again.GetProperty("slug").GetString());
    }

    [Fact]
    public async Task Manual_Slug_Edit_Persists()
    {
        using var c = await AdminAsync();
        var menu = await MainMenuAsync(c);
        var tag = Guid.NewGuid().ToString("N")[..5];
        var item = await AddItemAsync(c, menu, new { label = $"Thể thao {tag}", slug = $"the-thao-tu-dat-{tag}", linkType = "None" });
        Assert.Equal($"the-thao-tu-dat-{tag}", item.GetProperty("slug").GetString());

        var renamed = await TestClient.DataAsync(await c.PutAsync($"/api/admin/menu-items/{item.GetProperty("id").GetGuid()}",
            new { label = $"Thể thao mới {tag}", linkType = "None", rowVersion = item.GetProperty("rowVersion").GetString() }));
        Assert.Equal($"the-thao-tu-dat-{tag}", renamed.GetProperty("slug").GetString());
    }

    [Fact]
    public async Task Menu_Cycle_Rejected()
    {
        using var c = await AdminAsync();
        var menu = await MainMenuAsync(c);
        var a = await AddItemAsync(c, menu, new { label = CmsHelpers.Unique("Cha"), linkType = "None" });
        var b = await AddItemAsync(c, menu, new { label = CmsHelpers.Unique("Con"), linkType = "None", parentId = a.GetProperty("id").GetGuid() });

        var res = await c.PutAsync($"/api/admin/menu-items/{a.GetProperty("id").GetGuid()}", new
        {
            label = a.GetProperty("label").GetString(), linkType = "None", parentId = b.GetProperty("id").GetGuid(),
            rowVersion = a.GetProperty("rowVersion").GetString(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("menu_cycle", await TestClient.ErrorCodeAsync(res));
    }

    [Fact]
    public async Task javascript_Url_Rejected()
    {
        using var c = await AdminAsync();
        var menu = await MainMenuAsync(c);
        var res = await c.PostAsync($"/api/admin/menus/{menu}/items", new { label = CmsHelpers.Unique("Xấu"), linkType = "Url", url = "javascript:alert(1)" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("unsafe_url", await TestClient.ErrorCodeAsync(res));

        var banner = await c.PostAsync("/api/admin/banners", new { title = "Banner xấu", position = "home_top", linkUrl = "javascript:void(0)" });
        Assert.Equal("unsafe_url", await TestClient.ErrorCodeAsync(banner));
    }

    [Fact]
    public async Task Banner_Outside_Schedule_Not_Public()
    {
        using var c = await AdminAsync();
        var now = _f.Clock.UtcNow;
        var position = "pos_" + Guid.NewGuid().ToString("N")[..6];
        async Task<Guid> Create(string title, DateTime? start, DateTime? end) =>
            (await TestClient.DataAsync(await c.PostAsync("/api/admin/banners",
                new { title, position, linkUrl = "/tin-tuc", startsUtc = start, endsUtc = end }), HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var future = await Create("Sắp tới", now.AddDays(1), null);
        var past = await Create("Đã qua", now.AddDays(-5), now.AddDays(-1));
        var live = await Create("Đang chạy", now.AddDays(-1), now.AddDays(1));

        using var anon = _f.CreateTestClient();
        var list = await TestClient.DataAsync(await anon.GetAsync($"/api/public/banners?position={position}"));
        var ids = list.EnumerateArray().Select(b => b.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(new[] { live }, ids);
        Assert.DoesNotContain(future, ids);
        Assert.DoesNotContain(past, ids);
    }

    [Fact]
    public async Task Reorder_Persists()
    {
        using var c = await AdminAsync();
        var menu = await MainMenuAsync(c);
        var parent = await AddItemAsync(c, menu, new { label = CmsHelpers.Unique("Nhóm"), linkType = "None" });
        var pid = parent.GetProperty("id").GetGuid();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
            ids.Add((await AddItemAsync(c, menu, new { label = CmsHelpers.Unique("Mục " + i), linkType = "None", parentId = pid })).GetProperty("id").GetGuid());
        ids.Reverse();

        var res = await c.PostAsync($"/api/admin/menus/{menu}/reorder",
            new { items = ids.Select((id, i) => new { id, parentId = (Guid?)pid, sortOrder = i }) });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var tree = await TestClient.DataAsync(await c.GetAsync($"/api/admin/menus/{menu}/items"));
        var children = tree.EnumerateArray().Single(n => n.GetProperty("id").GetGuid() == pid).GetProperty("children")
            .EnumerateArray().Select(n => n.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(ids, children);
    }

    [Fact]
    public async Task Embed_From_Unlisted_Host_Rejected()
    {
        var html = await _f.ScopedAsync(sp => Task.FromResult(sp.GetRequiredService<IContentSanitizer>().Sanitize(
            "<iframe src=\"https://www.youtube.com/embed/abc\"></iframe><iframe src=\"https://evil.example/x\"></iframe><iframe src=\"http://www.youtube.com/embed/abc\"></iframe>")));
        Assert.Contains("https://www.youtube.com/embed/abc", html);
        Assert.DoesNotContain("evil.example", html);
        Assert.DoesNotContain("http://www.youtube.com", html);

        using var c = await AdminAsync();
        var album = await TestClient.DataAsync(await c.PostAsync("/api/admin/albums", new { title = CmsHelpers.Unique("Video"), kind = "Video" }), HttpStatusCode.Created);
        var bad = await c.PostAsync($"/api/admin/albums/{album.GetProperty("id").GetGuid()}/items", new { embedUrl = "https://evil.example/v.mp4", caption = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("embed_not_allowed", await TestClient.ErrorCodeAsync(bad));
        var good = await c.PostAsync($"/api/admin/albums/{album.GetProperty("id").GetGuid()}/items", new { embedUrl = "https://www.youtube.com/embed/abc", caption = "Clip" });
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
    }

    public static IEnumerable<object[]> Groups() => new[]
    {
        new object[] { "banners", "{\"title\":\"Banner A\",\"position\":\"home_top\",\"linkUrl\":\"/tin-tuc\"}", "{\"title\":\"Banner B\",\"position\":\"home_top\",\"linkUrl\":\"/tin-tuc\"}", "title" },
        new object[] { "documents", "{\"number\":\"01/NQ-CĐ\",\"title\":\"Nghị quyết A\",\"docType\":\"Nghị quyết\",\"issuer\":\"Ban Thường vụ\",\"issuedDate\":\"2026-01-15\"}", "{\"number\":\"01/NQ-CĐ\",\"title\":\"Nghị quyết B\",\"docType\":\"Nghị quyết\",\"issuer\":\"Ban Thường vụ\",\"issuedDate\":\"2026-01-15\"}", "title" },
        new object[] { "albums", "{\"title\":\"Album A\",\"kind\":\"Photo\"}", "{\"title\":\"Album B\",\"kind\":\"Photo\"}", "title" },
        new object[] { "pages", "{\"title\":\"Trang A\",\"body\":\"<p>a</p>\"}", "{\"title\":\"Trang B\",\"body\":\"<p>b</p>\"}", "title" },
    };

    [Theory]
    [MemberData(nameof(Groups))]
    public async Task Each_Group_Create_Update_Hide_Restore(string group, string create, string update, string field)
    {
        using var c = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..5];
        HttpContent Json(string s) => new StringContent(s.Replace("\"title\":\"", $"\"title\":\"{tag} "), System.Text.Encoding.UTF8, "application/json");

        var created = await TestClient.DataAsync(await c.SendAsync(HttpMethod.Post, $"/api/admin/{group}", Json(create)), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetGuid();

        var updateBody = JsonSerializer.Deserialize<Dictionary<string, object>>(update.Replace("\"title\":\"", $"\"title\":\"{tag} "))!;
        updateBody["rowVersion"] = created.GetProperty("rowVersion").GetString()!;
        var updated = await TestClient.DataAsync(await c.PutAsync($"/api/admin/{group}/{id}", updateBody));
        Assert.EndsWith("B", updated.GetProperty(field).GetString());

        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/admin/{group}/{id}/hide")).StatusCode);
        Assert.False((await TestClient.DataAsync(await c.GetAsync($"/api/admin/{group}/{id}"))).GetProperty("isVisible").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/admin/{group}/{id}/show")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await c.DeleteAsync($"/api/admin/{group}/{id}")).StatusCode);
        var trash = await TestClient.DataAsync(await c.GetAsync($"/api/admin/{group}?deleted=true&pageSize=200"));
        Assert.Contains(trash.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/admin/{group}/{id}/restore")).StatusCode);
        var live = await TestClient.DataAsync(await c.GetAsync($"/api/admin/{group}?pageSize=200"));
        Assert.Contains(live.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Site_Settings_And_Footer_Editable_And_Public()
    {
        using var c = await AdminAsync();
        var res = await c.PutAsync("/api/admin/settings/footer", new
        {
            copyright = "© MẪU Công đoàn",
            columns = new[] { new { title = "Liên kết", links = new[] { new { label = "Tin tức", url = "/tin-tuc" } } } },
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var bad = await c.PutAsync("/api/admin/settings/footer", new
        {
            copyright = "x",
            columns = new[] { new { title = "Xấu", links = new[] { new { label = "x", url = "javascript:alert(1)" } } } },
        });
        Assert.Equal("unsafe_url", await TestClient.ErrorCodeAsync(bad));
        var unknown = await c.PutAsync("/api/admin/settings/khong-co", new { a = 1 });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        using var anon = _f.CreateTestClient();
        var site = await TestClient.DataAsync(await anon.GetAsync("/api/public/site"));
        Assert.Equal("© MẪU Công đoàn", site.GetProperty("footer").GetProperty("copyright").GetString());
    }
}
