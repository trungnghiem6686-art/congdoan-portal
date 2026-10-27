namespace CongDoan.Application.Cms;

/// <summary>Cleans rich text: no scripts/handlers, safe URLs only, iframes only from allow-listed hosts.</summary>
public interface IContentSanitizer
{
    string Sanitize(string? html);
}

public sealed class CmsOptions
{
    /// <summary>Hosts allowed in embedded iframes (https only).</summary>
    public List<string> EmbedHosts { get; set; } = new()
    {
        "www.youtube.com", "youtube.com", "www.youtube-nocookie.com", "player.vimeo.com", "www.facebook.com",
    };

    public bool IsAllowedEmbed(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
        && EmbedHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase);
}

public static class UrlValidator
{
    /// <summary>Allows http(s) absolute URLs, site-relative paths, anchors, mailto and tel; rejects javascript:, data: etc.</summary>
    public static bool IsSafe(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var u = url.Trim();
        if (u.Any(char.IsControl)) return false;
        if (u.StartsWith('#')) return true;
        if (u.StartsWith('/')) return !u.StartsWith("//") && !u.StartsWith("/\\");
        if (!Uri.TryCreate(u, UriKind.Absolute, out var abs)) return false;
        return abs.Scheme is "http" or "https" or "mailto" or "tel";
    }
}
