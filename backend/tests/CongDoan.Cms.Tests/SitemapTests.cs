using CongDoan.Application.Identity;
using CongDoan.Testing;

namespace CongDoan.Cms.Tests;

public class SitemapTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public SitemapTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Sitemap_Lists_Public_Content_Only()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var published = await admin.CreatePostAsync(CmsHelpers.Unique("Bài công khai"));
        await admin.PublishAsync(published.GetProperty("id").GetGuid());
        var draft = await admin.CreatePostAsync(CmsHelpers.Unique("Bài nháp"));
        var page = await TestClient.DataAsync(await admin.PostAsync("/api/admin/pages", new { title = CmsHelpers.Unique("Trang ẩn"), body = "<p>x</p>", isVisible = false }), System.Net.HttpStatusCode.Created);

        using var anon = _f.CreateTestClient();
        var map = await TestClient.DataAsync(await anon.GetAsync("/api/public/sitemap"));
        var paths = map.EnumerateArray().Select(e => e.GetProperty("path").GetString()).ToList();

        Assert.Contains("/tin-tuc/" + published.GetProperty("slug").GetString(), paths);
        Assert.DoesNotContain("/tin-tuc/" + draft.GetProperty("slug").GetString(), paths);
        Assert.DoesNotContain("/trang/" + page.GetProperty("slug").GetString(), paths);
        Assert.All(map.EnumerateArray(), e => Assert.StartsWith("/", e.GetProperty("path").GetString()));
    }
}
