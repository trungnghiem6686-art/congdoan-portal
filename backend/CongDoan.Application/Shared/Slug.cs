using System.Globalization;
using System.Text;

namespace CongDoan.Application.Shared;

public static class Slug
{
    /// <summary>"Giới thiệu Công đoàn" → "gioi-thieu-cong-doan" (Vietnamese diacritics and đ removed).</summary>
    public static string From(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalized = text.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        var dash = false;
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(lower);
                dash = false;
            }
            else if (!dash && sb.Length > 0)
            {
                sb.Append('-');
                dash = true;
            }
        }
        var slug = sb.ToString().Trim('-');
        return slug.Length > 150 ? slug[..150].TrimEnd('-') : slug;
    }

    /// <summary>Accent-insensitive search form: lower case, diacritics removed, single spaces.</summary>
    public static string SearchText(string? text) => From(text).Replace('-', ' ');
}
