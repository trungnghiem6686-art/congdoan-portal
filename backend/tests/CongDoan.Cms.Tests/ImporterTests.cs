using System.Text.Json;
using CongDoan.Application.Cms;
using CongDoan.Application.Cms.Import;
using CongDoan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Cms.Tests;

/// <summary>Legacy import, run against a labelled sample export (MẪU) — no real source exists yet.</summary>
public class ImporterTests : IClassFixture<ApiFactory>, IDisposable
{
    private readonly ApiFactory _f;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cd_import_" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _tag = Guid.NewGuid().ToString("N")[..6];

    public ImporterTests(ApiFactory f)
    {
        _f = f;
        WriteFixture();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>3 posts (one with a broken image, one invalid), 1 category, 1 image used twice.</summary>
    private void WriteFixture()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "images"));
        File.WriteAllBytes(Path.Combine(_dir, "images", $"anh-{_tag}.png"), Files.Png(400, 300));
        File.WriteAllBytes(Path.Combine(_dir, "images", "hong.jpg"), "khong phai anh"u8.ToArray());
        var manifest = new
        {
            categories = new[] { new { key = "c1", name = $"Hoạt động MẪU {_tag}", oldUrl = $"/old-{_tag}/chuyenmuc/c1.html" } },
            posts = new object[]
            {
                new
                {
                    key = "p1", title = $"Bài MẪU một {_tag}", summary = "Tóm tắt MẪU", categoryKey = "c1",
                    bodyHtml = $"<p>Nội dung MẪU</p><img src=\"images/anh-{_tag}.png\" alt=\"MẪU\"><script>alert(1)</script>",
                    coverImage = $"images/anh-{_tag}.png", publishedUtc = "2019-05-01T03:00:00Z", oldUrl = $"/old-{_tag}/tin/1-bai-mot.html",
                },
                new
                {
                    key = "p2", title = $"Bài MẪU hai {_tag}", summary = (string?)null, categoryKey = "c1",
                    bodyHtml = "<p>Có ảnh hỏng</p><img src=\"images/hong.jpg\"><img src=\"images/khong-co.png\">",
                    coverImage = (string?)null, publishedUtc = "2020-01-02T03:00:00Z", oldUrl = $"/old-{_tag}/tin/2-bai-hai.html",
                },
                new
                {
                    key = "p3", title = "", summary = (string?)null, categoryKey = (string?)null, bodyHtml = "<p>Thiếu tiêu đề</p>",
                    coverImage = (string?)null, publishedUtc = "2020-01-03T03:00:00Z", oldUrl = $"/old-{_tag}/tin/3.html",
                },
            },
        };
        File.WriteAllText(Path.Combine(_dir, "manifest.json"), JsonSerializer.Serialize(manifest));
    }

    private Task<ImportReport> RunAsync(bool dryRun = false) =>
        _f.ScopedAsync(sp => sp.GetRequiredService<LegacyImporter>().RunAsync(new ImportOptions(_dir, dryRun, $"mau-{_tag}")));

    private Task<List<CongDoan.Domain.Cms.Post>> ImportedAsync() =>
        _f.ScopedAsync(sp => sp.GetRequiredService<ICmsDbContext>().Posts.AsNoTracking()
            .Where(p => p.SourceKey!.StartsWith($"mau-{_tag}:")).ToListAsync());

    [Fact]
    public async Task Rerun_Does_Not_Duplicate()
    {
        var first = await RunAsync();
        var second = await RunAsync();

        Assert.Equal(2, first.Created);
        Assert.Equal(0, second.Created);
        Assert.Equal(2, second.Skipped);
        var posts = await ImportedAsync();
        Assert.Equal(2, posts.Count);
        var categories = await _f.ScopedAsync(sp => sp.GetRequiredService<ICmsDbContext>().Categories.CountAsync(c => c.Name == $"Hoạt động MẪU {_tag}"));
        Assert.Equal(1, categories);
        var media = await _f.ScopedAsync(sp => sp.GetRequiredService<ICmsDbContext>().MediaAssets.CountAsync(m => m.OriginalName == $"anh-{_tag}.png"));
        Assert.Equal(1, media);
    }

    [Fact]
    public async Task Broken_Image_Logged_Not_Fatal()
    {
        var report = await RunAsync();

        var p2 = (await ImportedAsync()).Single(p => p.SourceKey == $"mau-{_tag}:p2");
        Assert.True(p2.IsPublic(DateTime.UtcNow));
        Assert.DoesNotContain("hong.jpg", p2.Body);
        Assert.DoesNotContain("khong-co.png", p2.Body);
        Assert.Contains(report.Issues, i => i.SourceKey == "p2" && i.Kind == "image" && i.Message.Contains("hong.jpg"));
        Assert.Contains(report.Issues, i => i.SourceKey == "p2" && i.Kind == "image" && i.Message.Contains("khong-co.png"));

        var p1 = (await ImportedAsync()).Single(p => p.SourceKey == $"mau-{_tag}:p1");
        Assert.Contains("/media/", p1.Body);
        Assert.DoesNotContain("<script", p1.Body);
        Assert.NotNull(p1.CoverMediaId);
        Assert.Equal(new DateTime(2019, 5, 1, 3, 0, 0, DateTimeKind.Utc), p1.PublishedUtc);
    }

    [Fact]
    public async Task Old_Url_Redirects_301_Not_Home()
    {
        await RunAsync();
        using var anon = _f.CreateTestClient();
        var p1 = (await ImportedAsync()).Single(p => p.SourceKey == $"mau-{_tag}:p1");

        var post = await TestClient.DataAsync(await anon.GetAsync($"/api/public/resolve?path=/old-{_tag}/tin/1-bai-mot.html"));
        Assert.Equal(301, post.GetProperty("status").GetInt32());
        Assert.Equal(PublicPaths.For("post", p1.Slug), post.GetProperty("location").GetString());

        var category = await TestClient.DataAsync(await anon.GetAsync($"/api/public/resolve?path=/old-{_tag}/chuyenmuc/c1.html"));
        Assert.Equal(301, category.GetProperty("status").GetInt32());
        Assert.StartsWith(PublicPaths.Category, category.GetProperty("location").GetString());

        // The invalid post was not imported: its old URL is a 404, never a redirect to the home page.
        var failed = await TestClient.DataAsync(await anon.GetAsync($"/api/public/resolve?path=/old-{_tag}/tin/3.html"));
        Assert.Equal(404, failed.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Reconciliation_Counts_Match_Fixture()
    {
        var dry = await RunAsync(dryRun: true);
        Assert.Equal(3, dry.SourcePosts);
        Assert.Empty(await ImportedAsync());

        var report = await RunAsync();
        Assert.Equal(3, report.SourcePosts);
        Assert.Equal(2, report.Created);
        Assert.Equal(1, report.Failed);
        Assert.Equal(report.SourcePosts, report.Created + report.Skipped + report.Failed);
        Assert.Equal((await ImportedAsync()).Count, report.Created);
        Assert.Contains(report.Issues, i => i.SourceKey == "p3" && i.Kind == "post");

        var csv = report.ToCsv();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(lines, l => l.StartsWith("source_posts,3"));
        Assert.Contains(lines, l => l.StartsWith("created,2"));
        Assert.Contains(lines, l => l.StartsWith("failed,1"));
        Assert.Contains(lines, l => l.Contains("p2") && l.Contains("hong.jpg"));
    }
}
