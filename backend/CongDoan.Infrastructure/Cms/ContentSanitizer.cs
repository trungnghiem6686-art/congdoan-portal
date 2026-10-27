using CongDoan.Application.Cms;
using Ganss.Xss;
using Microsoft.Extensions.Options;

namespace CongDoan.Infrastructure.Cms;

public sealed class ContentSanitizer : IContentSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public ContentSanitizer(IOptions<CmsOptions> options)
    {
        var cms = options.Value;
        _sanitizer = new HtmlSanitizer();
        foreach (var tag in new[] { "figure", "figcaption", "video", "source", "iframe", "audio", "mark" })
            _sanitizer.AllowedTags.Add(tag);
        foreach (var attr in new[] { "controls", "poster", "preload", "allowfullscreen", "frameborder", "allow", "loading", "data-media-id" })
            _sanitizer.AllowedAttributes.Add(attr);
        _sanitizer.AllowedSchemes.Clear();
        foreach (var s in new[] { "http", "https", "mailto", "tel" }) _sanitizer.AllowedSchemes.Add(s);
        _sanitizer.AllowedAttributes.Remove("style");
        _sanitizer.AllowedCssProperties.Clear();
        _sanitizer.AllowDataAttributes = false;
        _sanitizer.RemovingTag += (_, _) => { };
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement el)
            {
                if (el.LocalName == "iframe" && !cms.IsAllowedEmbed(el.GetAttribute("src")))
                    el.Remove();
                else if (el.LocalName == "a" && el.GetAttribute("target") == "_blank")
                    el.SetAttribute("rel", "noopener noreferrer");
            }
        };
    }

    public string Sanitize(string? html) => string.IsNullOrWhiteSpace(html) ? "" : _sanitizer.Sanitize(html);
}
