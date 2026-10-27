using System.Net;
using CongDoan.Application.Cms;
using CongDoan.Application.Identity;
using CongDoan.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace CongDoan.Cms.Tests;

public sealed class MediaFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Media:MaxImageBytes", "2000000");
        builder.UseSetting("Media:MaxVideoBytes", "3000000");
        builder.UseSetting("Media:ChunkSize", "1000000");
    }
}

public class MediaTests : IClassFixture<MediaFactory>
{
    private readonly MediaFactory _f;

    public MediaTests(MediaFactory f) => _f = f;

    private async Task<TestClient> EditorAsync()
    {
        var name = "ed-" + Guid.NewGuid().ToString("N")[..8];
        await _f.AdminAsync(name, false, Roles.ContentAdmin);
        return await _f.LoginAdminAsync(name);
    }

    [Fact]
    public async Task Video_Mp4_Uploaded_In_Chunks_Reassembles_Identical_Bytes()
    {
        using var c = await EditorAsync();
        var bytes = Files.Mp4(2_500_000);
        var start = await TestClient.DataAsync(await c.PostAsync("/api/admin/media/uploads",
            new { fileName = "Lễ kỷ niệm.mp4", size = bytes.Length }), HttpStatusCode.Created);
        var uploadId = start.GetProperty("uploadId").GetGuid();
        var chunk = start.GetProperty("chunkSize").GetInt32();
        Assert.Equal(1_000_000, chunk);

        for (int i = 0, offset = 0; offset < bytes.Length; i++, offset += chunk)
        {
            var part = new ByteArrayContent(bytes, offset, Math.Min(chunk, bytes.Length - offset));
            part.Headers.ContentType = new("application/octet-stream");
            var res = await c.SendAsync(HttpMethod.Put, $"/api/admin/media/uploads/{uploadId}/chunks/{i}", part);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
        var asset = await TestClient.DataAsync(await c.PostAsync($"/api/admin/media/uploads/{uploadId}/complete"));
        Assert.Equal("video", asset.GetProperty("kind").GetString());
        Assert.Equal("video/mp4", asset.GetProperty("contentType").GetString());

        var downloaded = await c.Http.GetByteArrayAsync(asset.GetProperty("url").GetString());
        Assert.Equal(bytes, downloaded);
    }

    [Fact]
    public async Task Upload_Over_Configured_Limit_Rejected_With_Clear_Message()
    {
        using var c = await EditorAsync();
        var res = await c.PostAsync("/api/admin/media/uploads", new { fileName = "big.mp4", size = 3_000_001 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("file_too_large", await TestClient.ErrorCodeAsync(res));
        Assert.Contains("MB", await res.Content.ReadAsStringAsync());

        // A client lying about the size is caught when chunks exceed the declared size.
        var start = await TestClient.DataAsync(await c.PostAsync("/api/admin/media/uploads", new { fileName = "liar.mp4", size = 1000 }), HttpStatusCode.Created);
        var part = new ByteArrayContent(Files.Mp4(5000));
        part.Headers.ContentType = new("application/octet-stream");
        var over = await c.SendAsync(HttpMethod.Put, $"/api/admin/media/uploads/{start.GetProperty("uploadId").GetGuid()}/chunks/0", part);
        Assert.Equal("file_too_large", await TestClient.ErrorCodeAsync(over));
    }

    [Fact]
    public async Task Rejects_Exe_Renamed_As_Jpg_By_Magic_Bytes()
    {
        using var c = await EditorAsync();
        var res = await c.PostAsync("/api/admin/media", Files.Form(Files.Exe(), "anh.jpg", "image/jpeg"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("unsupported_file_type", await TestClient.ErrorCodeAsync(res));
    }

    [Fact]
    public async Task Stored_Name_Is_Random_Not_User_Supplied()
    {
        using var c = await EditorAsync();
        var asset = await TestClient.DataAsync(await c.PostAsync("/api/admin/media",
            Files.Form(Files.Png(), "../../web.config.png", "image/png")), HttpStatusCode.Created);
        var id = asset.GetProperty("id").GetGuid();
        var stored = await _f.ScopedAsync(sp => sp.GetRequiredService<MediaService>().GetStoredPathAsync(id));
        Assert.DoesNotContain("web.config", stored);
        Assert.DoesNotContain("..", stored);
        Assert.Matches(@"^\d{4}/\d{2}/[0-9a-f]{32}\.png$", stored.Replace('\\', '/'));
        Assert.True(File.Exists(Path.Combine(_f.MediaRoot, stored)));
        Assert.Equal("web.config.png", asset.GetProperty("originalName").GetString());
    }

    [Fact]
    public async Task Delete_Blocked_While_Referenced()
    {
        using var c = await EditorAsync();
        var asset = await TestClient.DataAsync(await c.PostAsync("/api/admin/media", Files.Form(Files.Png(), "a.png", "image/png")), HttpStatusCode.Created);
        var id = asset.GetProperty("id").GetGuid();
        var owner = Guid.NewGuid();
        await _f.ScopedAsync(sp => sp.GetRequiredService<IMediaUsageTracker>().SetUsagesAsync("Post", owner, "Bài viết thử", new[] { id }));

        var usages = await TestClient.DataAsync(await c.GetAsync($"/api/admin/media/{id}/usages"));
        Assert.Equal("Post", usages[0].GetProperty("ownerType").GetString());
        var del = await c.DeleteAsync($"/api/admin/media/{id}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
        Assert.Equal("media_in_use", await TestClient.ErrorCodeAsync(del));

        await _f.ScopedAsync(sp => sp.GetRequiredService<IMediaUsageTracker>().SetUsagesAsync("Post", owner, "Bài viết thử", Array.Empty<Guid>()));
        Assert.Equal(HttpStatusCode.OK, (await c.DeleteAsync($"/api/admin/media/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Http.GetAsync(asset.GetProperty("url").GetString())).StatusCode);
    }

    [Fact]
    public async Task Restricted_File_Requires_Permission()
    {
        using var c = await EditorAsync();
        var asset = await TestClient.DataAsync(await c.PostAsync("/api/admin/media",
            Files.Form(Files.Png(10, 10), "noi-bo.png", "image/png", ("visibility", "restricted"))), HttpStatusCode.Created);
        var id = asset.GetProperty("id").GetGuid();

        using var anon = _f.CreateTestClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.Http.GetAsync($"/media/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.Http.GetAsync($"/api/files/{id}")).StatusCode);

        await _f.AdminAsync("no-media-perm", false, Roles.ReportViewer);
        using var other = await _f.LoginAdminAsync("no-media-perm");
        Assert.Equal(HttpStatusCode.Forbidden, (await other.Http.GetAsync($"/api/files/{id}")).StatusCode);

        var ok = await c.Http.GetAsync($"/api/files/{id}");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("nosniff", ok.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Image_Gets_Thumbnail()
    {
        using var c = await EditorAsync();
        var asset = await TestClient.DataAsync(await c.PostAsync("/api/admin/media", Files.Form(Files.Png(1200, 800), "anh-lon.png", "image/png")), HttpStatusCode.Created);
        Assert.Equal(1200, asset.GetProperty("width").GetInt32());
        Assert.Equal(800, asset.GetProperty("height").GetInt32());
        var thumbUrl = asset.GetProperty("thumbnailUrl").GetString();
        Assert.False(string.IsNullOrEmpty(thumbUrl));

        var thumb = await c.Http.GetByteArrayAsync(thumbUrl);
        using var bmp = SKBitmap.Decode(thumb);
        Assert.True(bmp.Width <= 480);
        Assert.Equal(Math.Round(bmp.Width * 800.0 / 1200), bmp.Height, 0);
    }
}
