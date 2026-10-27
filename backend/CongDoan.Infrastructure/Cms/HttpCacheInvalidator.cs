using CongDoan.Application.Cms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CongDoan.Infrastructure.Cms;

/// <summary>Calls the Next.js revalidation route (POST {Frontend:RevalidateUrl} with a shared secret).</summary>
public sealed class HttpCacheInvalidator : ICacheInvalidator
{
    private readonly HttpClient _http;
    private readonly string? _url;
    private readonly string? _secret;
    private readonly ILogger<HttpCacheInvalidator> _logger;

    public HttpCacheInvalidator(HttpClient http, IConfiguration config, ILogger<HttpCacheInvalidator> logger)
    {
        _http = http;
        _url = config["Frontend:RevalidateUrl"];
        _secret = config["Frontend:RevalidateSecret"];
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(3);
    }

    public async Task InvalidateAsync(string tag, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_url) || string.IsNullOrWhiteSpace(_secret)) return;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_url}?tag={Uri.EscapeDataString(tag)}");
            req.Headers.Add("x-revalidate-secret", _secret);
            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) _logger.LogWarning("Cache revalidation for {Tag} returned {Status}", tag, (int)res.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // The frontend also revalidates on a timer; a missed call only delays freshness.
            _logger.LogWarning(ex, "Cache revalidation for {Tag} failed", tag);
        }
    }
}
