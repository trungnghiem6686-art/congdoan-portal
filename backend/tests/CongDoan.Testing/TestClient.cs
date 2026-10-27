using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CongDoan.Testing;

/// <summary>HTTP client that handles cookies and the anti-forgery header like the real frontend.</summary>
public sealed class TestClient : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string? _csrf;

    public TestClient(HttpClient http) => Http = http;

    public HttpClient Http { get; }

    public async Task<string> CsrfAsync(bool refresh = false)
    {
        if (_csrf is null || refresh)
        {
            var res = await Http.GetAsync("/api/antiforgery");
            res.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            _csrf = doc.RootElement.GetProperty("data").GetProperty("token").GetString();
        }
        return _csrf!;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, bool csrf = true)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is HttpContent content) req.Content = content;
        else if (body is not null) req.Content = JsonContent.Create(body, options: Json);
        if (csrf && method != HttpMethod.Get) req.Headers.Add("X-XSRF-TOKEN", await CsrfAsync());
        return await Http.SendAsync(req);
    }

    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url);
    public Task<HttpResponseMessage> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);
    public Task<HttpResponseMessage> PutAsync(string url, object? body = null) => SendAsync(HttpMethod.Put, url, body);
    public Task<HttpResponseMessage> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url);

    public static async Task<JsonElement> DataAsync(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        if (res.StatusCode != expected)
            throw new Xunit.Sdk.XunitException($"Expected {(int)expected} but got {(int)res.StatusCode}: {text}");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("data").Clone();
    }

    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text)) return null;
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object
            ? e.GetProperty("code").GetString()
            : null;
    }

    public void Dispose() => Http.Dispose();
}
