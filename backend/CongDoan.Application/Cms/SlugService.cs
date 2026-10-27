using CongDoan.Application.Shared;

namespace CongDoan.Application.Cms;

public static class SlugRules
{
    /// <summary>Slug to store: manual value (normalised) or derived from the title.</summary>
    public static (string Slug, bool Manual) Resolve(string? manual, string title)
    {
        var slug = string.IsNullOrWhiteSpace(manual) ? Slug.From(title) : Slug.From(manual);
        if (slug.Length == 0) throw AppException.Validation("Không tạo được đường dẫn từ tên. Vui lòng nhập đường dẫn.");
        return (slug, !string.IsNullOrWhiteSpace(manual));
    }

    /// <summary>Conflict with a free alternative ("ten-2", "ten-3"...).</summary>
    public static async Task<AppException> DuplicateAsync(string slug, Func<string, Task<bool>> taken)
    {
        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{slug}-{i}";
            if (!await taken(candidate))
                return new SlugConflictException(slug, candidate);
        }
        return new SlugConflictException(slug, slug + "-" + Guid.NewGuid().ToString("N")[..6]);
    }
}

public sealed class SlugConflictException : AppException
{
    public SlugConflictException(string slug, string suggestion)
        : base("duplicate_slug", $"Đường dẫn \"{slug}\" đã được dùng. Gợi ý: \"{suggestion}\".", 409) => Suggestion = suggestion;

    public string Suggestion { get; }
}
