using CongDoan.Application.Shared;
using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CongDoan.Application.Exams;

/// <summary>
/// Closes and grades attempts whose deadline has passed, even if the browser is gone. Deadlines are stored, so a restart
/// never extends them. Also grades attempts that were closed but not graded (e.g. a crash between the two steps).
/// </summary>
public sealed class ExpirySweeper
{
    private readonly IExamDbContext _db;
    private readonly IAttemptFinisher _finisher;
    private readonly IClock _clock;
    private readonly TimeSpan _grace;

    public ExpirySweeper(IExamDbContext db, IAttemptFinisher finisher, IClock clock, IOptions<ExamOptions> options)
    {
        _db = db;
        _finisher = finisher;
        _clock = clock;
        _grace = TimeSpan.FromSeconds(options.Value.DeadlineGraceSeconds);
    }

    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var cutoff = _clock.UtcNow - _grace;
        var expired = await _db.Attempts.AsNoTracking()
            .Where(a => a.Status == AttemptStatus.InProgress && a.DeadlineUtc < cutoff)
            .OrderBy(a => a.DeadlineUtc).Select(a => a.Id).Take(500).ToListAsync(ct);
        var ungraded = await _db.Attempts.AsNoTracking()
            .Where(a => (a.Status == AttemptStatus.Submitted || a.Status == AttemptStatus.Expired) && a.Score == null)
            .Select(a => new { a.Id, a.Status }).Take(500).ToListAsync(ct);

        foreach (var id in expired) await _finisher.FinishAsync(id, AttemptStatus.Expired, ct);
        foreach (var a in ungraded) await _finisher.FinishAsync(a.Id, a.Status, ct);
        return expired.Count + ungraded.Count;
    }
}
