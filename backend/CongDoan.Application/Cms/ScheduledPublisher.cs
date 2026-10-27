using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

/// <summary>
/// Publishes posts whose scheduled time has passed. State lives in the database, so restarts lose nothing;
/// concurrent runs are safe because each row is updated under its RowVersion and the loser skips it.
/// </summary>
public sealed class ScheduledPublisher
{
    private readonly ICmsDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public ScheduledPublisher(ICmsDbContext db, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var due = await _db.Posts.AsNoTracking()
            .Where(p => p.Status == PostStatus.Scheduled && p.ScheduledUtc <= now && !p.IsDeleted)
            .OrderBy(p => p.ScheduledUtc).Select(p => p.Id).Take(200).ToListAsync(ct);
        var published = 0;
        foreach (var id in due)
        {
            var post = await _db.Posts.SingleOrDefaultAsync(p => p.Id == id, ct);
            if (post is null || !post.PublishIfDue(now)) continue;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another worker (or an editor) changed the post first; it is no longer ours to publish.
                _db.Detach(post);
                continue;
            }
            published++;
            await _audit.WriteAsync("cms.post.autopublish", "Post", id.ToString(), new { post.Title, post.PublishedUtc }, ct);
        }
        return published;
    }
}
