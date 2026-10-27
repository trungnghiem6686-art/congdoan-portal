using System.Net;
using CongDoan.Application.Cms;
using CongDoan.Application.Identity;
using CongDoan.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Cms.Tests;

public class PublicApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public PublicApiTests(ApiFactory f) => _f = f;

    private static async Task<List<System.Text.Json.JsonElement>> SearchAsync(TestClient c, string q, string? type = null)
    {
        var data = await TestClient.DataAsync(await c.GetAsync($"/api/public/search?q={Uri.EscapeDataString(q)}&pageSize=50{(type is null ? "" : "&type=" + type)}"));
        return data.GetProperty("items").EnumerateArray().ToList();
    }

    [Fact]
    public async Task Search_Matches_Without_Diacritics()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var post = await admin.CreatePostAsync($"Hội nghị Công đoàn ngành {tag}", $"<p>Đại hội Công đoàn Đường sắt {tag}</p>");
        await admin.PublishAsync(post.GetProperty("id").GetGuid());
        using var anon = _f.CreateTestClient();

        var plain = await SearchAsync(anon, $"cong doan {tag}");
        Assert.Contains(plain, r => r.GetProperty("url").GetString() == "/tin-tuc/" + post.GetProperty("slug").GetString());
        var accented = await SearchAsync(anon, $"CÔNG ĐOÀN {tag}");
        Assert.Single(accented);
        Assert.Empty(await SearchAsync(anon, $"cong doan {tag} khongtontai"));
    }

    [Fact]
    public async Task Search_Never_Returns_Draft_Or_Internal_Or_Exam_Content()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var token = "bimat" + Guid.NewGuid().ToString("N")[..6];
        await admin.CreatePostAsync($"Nháp {token}");
        var hiddenPage = await TestClient.DataAsync(await admin.PostAsync("/api/admin/pages", new { title = $"Trang ẩn {token}", body = "<p>x</p>" }), HttpStatusCode.Created);
        await admin.PostAsync($"/api/admin/pages/{hiddenPage.GetProperty("id").GetGuid()}/hide");
        var doc = await TestClient.DataAsync(await admin.PostAsync("/api/admin/documents",
            new { number = $"99/{token}", title = $"Văn bản xóa {token}", docType = "Công văn", issuedDate = "2026-02-01" }), HttpStatusCode.Created);
        await admin.DeleteAsync($"/api/admin/documents/{doc.GetProperty("id").GetGuid()}");
        var scheduled = await admin.CreatePostAsync($"Hẹn giờ {token}");
        await admin.PostAsync($"/api/admin/posts/{scheduled.GetProperty("id").GetGuid()}/transition",
            new { to = "Scheduled", scheduledUtc = _f.Clock.UtcNow.AddDays(2) });

        using var anon = _f.CreateTestClient();
        Assert.Empty(await SearchAsync(anon, token));

        var visible = await TestClient.DataAsync(await admin.PostAsync("/api/admin/pages", new { title = $"Trang hiện {token}", body = "<p>y</p>" }), HttpStatusCode.Created);
        var results = await SearchAsync(anon, token);
        Assert.Single(results);
        Assert.All(results, r => Assert.Contains(r.GetProperty("type").GetString(), new[] { "post", "page", "document", "album" }));
        Assert.Equal(visible.GetProperty("path").GetString(), results[0].GetProperty("url").GetString());
    }

    [Fact]
    public async Task Contact_Returns_Success_Only_After_Row_Saved()
    {
        using var anon = _f.CreateTestClient();
        var res = await anon.PostAsync("/api/public/contact", new
        {
            name = "Nguyễn Văn A", email = "a@example.com", phone = "0900000000", subject = "Hỏi về thủ tục", message = "Xin hướng dẫn thủ tục kết nạp đoàn viên.",
        });
        var data = await TestClient.DataAsync(res);
        var id = data.GetProperty("id").GetGuid();
        var saved = await _f.ScopedAsync(sp => sp.GetRequiredService<ICmsDbContext>().ContactRequests.AnyAsync(c => c.Id == id));
        Assert.True(saved);

        var invalid = await anon.PostAsync("/api/public/contact", new { name = "", email = "not-an-email", message = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var spam = await anon.PostAsync("/api/public/contact", new { name = "Bot", email = "b@example.com", message = "Mua ngay", website = "http://spam" });
        Assert.Equal(HttpStatusCode.BadRequest, spam.StatusCode);

        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var list = await TestClient.DataAsync(await admin.GetAsync("/api/admin/contacts?pageSize=100"));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Document_Filter_By_Number_And_Date()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var tag = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        async Task Create(string number, string date) => await TestClient.DataAsync(await admin.PostAsync("/api/admin/documents",
            new { number, title = $"Quyết định {tag}", docType = "Quyết định", issuer = "Ban Thường vụ", issuedDate = date }), HttpStatusCode.Created);
        await Create($"12/QĐ-{tag}", "2025-03-10");
        await Create($"45/QĐ-{tag}", "2026-01-20");
        await Create($"46/QĐ-{tag}", "2026-06-01");
        using var anon = _f.CreateTestClient();

        async Task<List<string>> Numbers(string query) =>
            (await TestClient.DataAsync(await anon.GetAsync("/api/public/documents?" + query))).GetProperty("items").EnumerateArray()
            .Select(d => d.GetProperty("number").GetString()!).ToList();

        Assert.Equal(new[] { $"45/QĐ-{tag}" }, await Numbers($"number={Uri.EscapeDataString("45/QĐ-" + tag)}"));
        Assert.Equal(new[] { $"45/QĐ-{tag}" }, await Numbers($"q={tag}&from=2026-01-01&to=2026-03-31"));
        Assert.Equal(3, (await Numbers($"q={tag}&docType={Uri.EscapeDataString("Quyết định")}")).Count);
        var first = await TestClient.DataAsync(await anon.GetAsync($"/api/public/documents?q={tag}&pageSize=1"));
        var slug = first.GetProperty("items")[0].GetProperty("slug").GetString();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/public/documents/{slug}")).StatusCode);
    }

    [Fact]
    public async Task Public_Categories_And_Albums_Show_Only_Visible()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ContentAdmin);
        var cat = await TestClient.DataAsync(await admin.PostAsync("/api/admin/categories", new { name = CmsHelpers.Unique("Chuyên mục") }), HttpStatusCode.Created);
        var album = await TestClient.DataAsync(await admin.PostAsync("/api/admin/albums", new { title = CmsHelpers.Unique("Album"), kind = "Photo" }), HttpStatusCode.Created);
        await admin.PostAsync($"/api/admin/albums/{album.GetProperty("id").GetGuid()}/hide");
        using var anon = _f.CreateTestClient();

        var cats = await TestClient.DataAsync(await anon.GetAsync("/api/public/categories"));
        Assert.Contains(cats.EnumerateArray(), c => c.GetProperty("slug").GetString() == cat.GetProperty("slug").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/albums/{album.GetProperty("slug").GetString()}")).StatusCode);
    }
}

public sealed class ContactLimitFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimit:ContactPermitPerMinute", "3");
    }
}

public class ContactRateLimitTests : IClassFixture<ContactLimitFactory>
{
    private readonly ContactLimitFactory _f;

    public ContactRateLimitTests(ContactLimitFactory f) => _f = f;

    [Fact]
    public async Task Contact_Rate_Limited()
    {
        using var anon = _f.CreateTestClient();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            codes.Add((await anon.PostAsync("/api/public/contact", new { name = "A", email = "a@example.com", message = "Nội dung " + i })).StatusCode);
        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[^1]);
    }
}
