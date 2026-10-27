using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using CongDoan.Application.Cms;
using CongDoan.Application.Identity;
using CongDoan.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CongDoan.Cms.Tests;

public sealed class RecordingInvalidator : ICacheInvalidator
{
    public ConcurrentQueue<string> Tags { get; } = new();
    public Task InvalidateAsync(string tag, CancellationToken ct = default) { Tags.Enqueue(tag); return Task.CompletedTask; }
}

/// <summary>One database per class: homepage state is global.</summary>
public sealed class HomepageFactory : ApiFactory
{
    public RecordingInvalidator Invalidator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<ICacheInvalidator>();
            s.AddSingleton<ICacheInvalidator>(Invalidator);
        });
    }
}

public class HomepageBuilderTests : IClassFixture<HomepageFactory>
{
    private readonly HomepageFactory _f;

    public HomepageBuilderTests(HomepageFactory f) => _f = f;

    private static object Intro(string html, string? id = null) => new { id, type = "intro_text", title = "Giới thiệu", visible = true, config = new { html, layout = "plain" } };
    private static object News(string? id = null) => new { id, type = "news_list", title = "Tin mới", visible = true, config = new { limit = 5, layout = "list" } };

    private static async Task<JsonElement> SaveDraftAsync(TestClient c, params object[] sections) =>
        await TestClient.DataAsync(await c.PutAsync("/api/admin/homepage/draft", new { sections }));

    private static async Task<JsonElement> PublishAsync(TestClient c)
    {
        var state = await TestClient.DataAsync(await c.GetAsync("/api/admin/homepage"));
        return await TestClient.DataAsync(await c.PostAsync("/api/admin/homepage/publish", new { revisionId = state.GetProperty("draft").GetProperty("id").GetGuid() }));
    }

    private async Task<string> PublicIntroAsync()
    {
        using var anon = _f.CreateTestClient();
        var home = await TestClient.DataAsync(await anon.GetAsync("/api/public/homepage"));
        return home.GetProperty("sections").EnumerateArray().First(s => s.GetProperty("type").GetString() == "intro_text")
            .GetProperty("config").GetProperty("html").GetString()!;
    }

    [Fact]
    public async Task Draft_Change_Not_In_Public_Response()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        await SaveDraftAsync(c, Intro("<p>Bản công khai D1</p>"));
        await PublishAsync(c);
        await SaveDraftAsync(c, Intro("<p>Bản nháp D2</p>"));

        Assert.Contains("Bản công khai D1", await PublicIntroAsync());
        var preview = await TestClient.DataAsync(await c.GetAsync("/api/admin/homepage/preview"));
        Assert.Contains("Bản nháp D2", preview.GetRawText());
    }

    [Fact]
    public async Task Publish_Swaps_Atomically_And_Invalidates_Cache()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        await SaveDraftAsync(c, Intro("<p>Phiên bản P1</p>"));
        var before = _f.Invalidator.Tags.Count(t => t == "homepage");
        var published = await PublishAsync(c);
        Assert.Equal("Published", published.GetProperty("status").GetString());
        Assert.Contains("Phiên bản P1", await PublicIntroAsync());
        Assert.True(_f.Invalidator.Tags.Count(t => t == "homepage") > before);

        var state = await TestClient.DataAsync(await c.GetAsync("/api/admin/homepage"));
        var history = state.GetProperty("history").EnumerateArray().ToList();
        Assert.Single(history, h => h.GetProperty("status").GetString() == "Published");
        Assert.Equal(JsonValueKind.Null, state.GetProperty("draft").ValueKind);
    }

    [Fact]
    public async Task Rollback_Restores_Previous_Version()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        await SaveDraftAsync(c, Intro("<p>Cũ R1</p>"));
        var v1 = (await PublishAsync(c)).GetProperty("version").GetInt32();
        await SaveDraftAsync(c, Intro("<p>Mới R2</p>"));
        await PublishAsync(c);
        Assert.Contains("Mới R2", await PublicIntroAsync());

        var rolled = await TestClient.DataAsync(await c.PostAsync("/api/admin/homepage/rollback", new { version = v1 }));
        Assert.True(rolled.GetProperty("version").GetInt32() > v1);
        Assert.Contains("Cũ R1", await PublicIntroAsync());
    }

    [Fact]
    public async Task Empty_Section_Rejected()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var res = await c.PutAsync("/api/admin/homepage/draft", new
        {
            sections = new object[] { new { type = "quick_links", title = "Tiện ích", visible = true, config = new { items = Array.Empty<object>(), layout = "icons" } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_section", await TestClient.ErrorCodeAsync(res));
        var emptyHtml = await c.PutAsync("/api/admin/homepage/draft", new { sections = new[] { Intro("   ") } });
        Assert.Equal("invalid_section", await TestClient.ErrorCodeAsync(emptyHtml));
    }

    [Fact]
    public async Task Unknown_Section_Type_Rejected()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var res = await c.PutAsync("/api/admin/homepage/draft", new { sections = new object[] { new { type = "custom_script", config = new { js = "alert(1)" } } } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_section", await TestClient.ErrorCodeAsync(res));
        Assert.Contains("custom_script", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Stable_SectionId_Survives_Reorder()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var draft = await SaveDraftAsync(c, Intro("<p>A</p>"), News());
        var ids = draft.GetProperty("sections").EnumerateArray().Select(s => s.GetProperty("id").GetString()!).ToList();
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));

        var reordered = await SaveDraftAsync(c, News(ids[1]), Intro("<p>A</p>", ids[0]));
        var after = reordered.GetProperty("sections").EnumerateArray().Select(s => s.GetProperty("id").GetString()).ToList();
        Assert.Equal(new[] { ids[1], ids[0] }, after);

        var dup = await c.PutAsync("/api/admin/homepage/draft", new { sections = new[] { News(ids[0]), Intro("<p>x</p>", ids[0]) } });
        Assert.Equal("invalid_section", await TestClient.ErrorCodeAsync(dup));
    }

    [Fact]
    public async Task Section_Html_Is_Sanitized_And_Schedule_Respected()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var now = _f.Clock.UtcNow;
        await SaveDraftAsync(c,
            Intro("<p>An toàn</p><script>alert(1)</script>"),
            new { type = "news_list", title = "Hết hạn", visible = true, endsUtc = now.AddDays(-1), config = new { limit = 5, layout = "list" } },
            new { type = "news_list", title = "Ẩn", visible = false, config = new { limit = 5, layout = "list" } });
        await PublishAsync(c);

        using var anon = _f.CreateTestClient();
        var home = await TestClient.DataAsync(await anon.GetAsync("/api/public/homepage"));
        var text = home.GetRawText();
        Assert.DoesNotContain("<script", text);
        Assert.DoesNotContain("Hết hạn", text);
        Assert.DoesNotContain("\"Ẩn\"", text);
    }

    [Fact]
    public async Task News_Section_Resolves_Published_Posts_Only()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var live = await c.CreatePostAsync(CmsHelpers.Unique("Tin trang chủ"));
        await c.PublishAsync(live.GetProperty("id").GetGuid());
        var draft = await c.CreatePostAsync(CmsHelpers.Unique("Tin nháp trang chủ"));
        await SaveDraftAsync(c, new { type = "news_list", title = "Tin mới", visible = true, config = new { limit = 20, layout = "list" } });
        await PublishAsync(c);

        using var anon = _f.CreateTestClient();
        var home = await TestClient.DataAsync(await anon.GetAsync("/api/public/homepage"));
        var section = home.GetProperty("sections").EnumerateArray().Single(s => s.GetProperty("type").GetString() == "news_list");
        var slugs = section.GetProperty("data").GetProperty("posts").EnumerateArray().Select(p => p.GetProperty("slug").GetString()).ToList();
        Assert.Contains(live.GetProperty("slug").GetString(), slugs);
        Assert.DoesNotContain(draft.GetProperty("slug").GetString(), slugs);
    }

    [Fact]
    public async Task Page_Built_From_Sections_Same_Rules()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var page = await TestClient.DataAsync(await c.PostAsync("/api/admin/pages", new { title = CmsHelpers.Unique("Trang khối"), body = "" }), HttpStatusCode.Created);
        var id = page.GetProperty("id").GetGuid();

        var bad = await c.PutAsync($"/api/admin/pages/{id}/layout/draft", new { sections = new object[] { new { type = "nope", config = new { } } } });
        Assert.Equal("invalid_section", await TestClient.ErrorCodeAsync(bad));

        var draft = await TestClient.DataAsync(await c.PutAsync($"/api/admin/pages/{id}/layout/draft", new { sections = new[] { Intro("<p>Khối trang tĩnh</p>") } }));
        await TestClient.DataAsync(await c.PostAsync($"/api/admin/pages/{id}/layout/publish", new { revisionId = draft.GetProperty("id").GetGuid() }));

        using var anon = _f.CreateTestClient();
        var pub = await TestClient.DataAsync(await anon.GetAsync($"/api/public/pages/{page.GetProperty("slug").GetString()}"));
        Assert.Contains("Khối trang tĩnh", pub.GetProperty("sections").GetRawText());
    }

    [Fact]
    public async Task Editor_Cannot_Publish_Homepage()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.Editor);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsync("/api/admin/homepage/draft", new { sections = new[] { Intro("<p>x</p>") } })).StatusCode);
    }
}
