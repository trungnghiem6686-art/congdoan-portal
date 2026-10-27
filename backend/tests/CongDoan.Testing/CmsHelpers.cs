using System.Net;
using System.Text.Json;
using CongDoan.Application.Identity;

namespace CongDoan.Testing;

public static class CmsHelpers
{
    public static string Unique(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..8];

    public static async Task<TestClient> AdminWithRoleAsync(this ApiFactory f, string role)
    {
        var name = Unique(role.Replace('_', '-'));
        await f.AdminAsync(name, false, role);
        return await f.LoginAdminAsync(name);
    }

    public static async Task<JsonElement> CreatePostAsync(this TestClient c, string title, string body = "<p>Nội dung</p>", object? extra = null)
    {
        var payload = new Dictionary<string, object?> { ["title"] = title, ["body"] = body, ["summary"] = "Tóm tắt" };
        if (extra is not null)
            foreach (var p in extra.GetType().GetProperties()) payload[p.Name] = p.GetValue(extra);
        return await TestClient.DataAsync(await c.PostAsync("/api/admin/posts", payload), HttpStatusCode.Created);
    }

    public static async Task<JsonElement> PublishAsync(this TestClient c, Guid postId) =>
        await TestClient.DataAsync(await c.PostAsync($"/api/admin/posts/{postId}/transition", new { to = "Published" }));
}
