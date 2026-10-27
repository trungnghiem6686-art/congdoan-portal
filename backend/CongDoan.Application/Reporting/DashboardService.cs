using CongDoan.Application.Cms;
using CongDoan.Application.Exams;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Reporting;

public sealed record DashboardDto(int PublishedPosts, int PendingReviewPosts, int DraftPosts, int NewContacts, int Candidates,
    int ApprovedQuestions, int OpenExams, int AttemptsInProgress, int AttemptsFinishedToday);

/// <summary>Live counts read through each module's own context (no cross-module joins).</summary>
public sealed class DashboardService
{
    private readonly ICmsDbContext _cms;
    private readonly IIdentityDbContext _identity;
    private readonly IExamDbContext _exam;
    private readonly IClock _clock;

    public DashboardService(ICmsDbContext cms, IIdentityDbContext identity, IExamDbContext exam, IClock clock)
    {
        _cms = cms;
        _identity = identity;
        _exam = exam;
        _clock = clock;
    }

    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var dayStart = VietnamTime.FromUtc(now).Date;
        var dayStartUtc = DateTime.SpecifyKind(dayStart.AddHours(-7), DateTimeKind.Utc);
        var posts = _cms.Posts.AsNoTracking().Where(p => !p.IsDeleted);
        return new DashboardDto(
            await posts.CountAsync(p => p.Status == PostStatus.Published, ct),
            await posts.CountAsync(p => p.Status == PostStatus.PendingReview, ct),
            await posts.CountAsync(p => p.Status == PostStatus.Draft, ct),
            await _cms.ContactRequests.CountAsync(c => c.Status == ContactStatus.New, ct),
            await _identity.Candidates.CountAsync(c => !c.IsDeleted, ct),
            await _exam.Questions.CountAsync(q => q.Status == QuestionStatus.Approved && !q.IsDeleted, ct),
            await _exam.Exams.CountAsync(e => e.Status == ExamStatus.Published && !e.IsDeleted && e.OpensAtUtc <= now && e.ClosesAtUtc > now, ct),
            await _exam.Attempts.CountAsync(a => a.Status == AttemptStatus.InProgress, ct),
            await _exam.Attempts.CountAsync(a => a.FinishedUtc >= dayStartUtc && a.Status != AttemptStatus.InProgress, ct));
    }
}
