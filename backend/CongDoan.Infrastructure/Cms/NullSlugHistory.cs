using CongDoan.Application.Cms;

namespace CongDoan.Infrastructure.Cms;

/// <summary>Placeholder until redirects are stored (replaced by the redirect service).</summary>
internal sealed class NullSlugHistory : ISlugHistory
{
    public Task RecordAsync(string kind, string oldSlug, string newSlug, CancellationToken ct = default) => Task.CompletedTask;
}
