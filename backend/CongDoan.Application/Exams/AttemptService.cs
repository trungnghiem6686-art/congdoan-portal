using System.Security.Cryptography;
using System.Text.Json;
using CongDoan.Application.Shared;
using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Exams;

public sealed record SnapshotChoice(Guid Id, string Content, bool IsCorrect);

public sealed record StartResult(Guid AttemptId, bool Resumed, int AttemptNo, DateTime DeadlineUtc, DateTime ServerNowUtc);

public sealed record AttemptChoiceView(Guid Id, string Content);

public sealed record AttemptQuestionView(Guid Id, int OrderNo, string Type, string Content, decimal Points, IReadOnlyList<AttemptChoiceView> Choices);

public sealed record AnswerView(Guid QuestionId, IReadOnlyList<Guid> ChoiceIds, bool Flagged, long Seq);

/// <summary>What the candidate sees while taking the exam. Never contains correct answers or explanations.</summary>
public sealed record AttemptView(Guid AttemptId, Guid ExamId, string ExamTitle, string? Instructions, int AttemptNo, string Status,
    DateTime StartedUtc, DateTime DeadlineUtc, DateTime ServerNowUtc, long LastSeq, bool LogTabSwitches,
    IReadOnlyList<AttemptQuestionView> Questions, IReadOnlyList<AnswerView> Answers);

public sealed class AttemptService
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IExamDbContext _db;
    private readonly ExamService _exams;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly IAttemptFinisher _finisher;

    public AttemptService(IExamDbContext db, ExamService exams, IClock clock, IAuditWriter audit, IAttemptFinisher finisher)
    {
        _db = db;
        _exams = exams;
        _clock = clock;
        _audit = audit;
        _finisher = finisher;
    }

    /// <summary>
    /// Starts (or resumes) the candidate's attempt. Races are settled by the database: the filtered unique index allows one
    /// in-progress attempt and (exam, candidate, attemptNo) is unique, so parallel starts converge on one attempt.
    /// </summary>
    public async Task<StartResult> StartAsync(Guid candidateId, Guid examId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var exam = await _db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == examId && !e.IsDeleted && e.Status != ExamStatus.Draft, ct)
                   ?? throw AppException.NotFound("Không tìm thấy kỳ thi.");
        if (!await _exams.IsAssignedAsync(examId, candidateId, ct))
            throw new AppException("not_assigned", "Bạn không được giao kỳ thi này.", 403);

        var active = await ActiveAsync(examId, candidateId, ct);
        if (active is not null)
        {
            if (active.DeadlineUtc > now) return Resume(active, now);
            await _finisher.FinishAsync(active.Id, AttemptStatus.Expired, ct);
        }

        if (now < exam.OpensAtUtc) throw new AppException("exam_not_open", $"Kỳ thi chưa mở. Thời gian mở: {exam.OpensAtUtc:O}.", 400);
        if (!exam.IsOpen(now)) throw new AppException("exam_closed", "Kỳ thi đã đóng.", 400);

        var used = await _db.Attempts.CountAsync(a => a.ExamId == examId && a.CandidateId == candidateId && a.Status != AttemptStatus.Voided, ct);
        if (used >= exam.MaxAttempts)
            throw new AppException("max_attempts_reached", $"Bạn đã dùng hết {exam.MaxAttempts} lượt làm bài.", 400);

        var questionIds = await _exams.DrawAsync(examId, new Random(RandomNumberGenerator.GetInt32(int.MaxValue)), ct);
        if (questionIds.Count == 0) throw new AppException("exam_not_ready", "Đề thi chưa có câu hỏi.", 400);
        var versions = await _db.Questions.AsNoTracking().Where(q => questionIds.Contains(q.Id))
            .Join(_db.QuestionVersions.AsNoTracking().Include(v => v.Choices), q => q.CurrentVersionId, v => v.Id, (q, v) => new { q, v })
            .ToListAsync(ct);
        var byId = versions.ToDictionary(x => x.q.Id);
        var rng = new Random(RandomNumberGenerator.GetInt32(int.MaxValue));
        var order = exam.ShuffleQuestions ? questionIds.OrderBy(_ => rng.Next()).ToList() : questionIds.ToList();

        var deadline = new[] { now.AddMinutes(exam.DurationMinutes), exam.ClosesAtUtc }.Min();
        var maxScore = order.Sum(id => byId[id].v.Points);
        var attempt = new Attempt(examId, candidateId, used + 1, now, deadline, maxScore);
        _db.Attempts.Add(attempt);
        var no = 0;
        foreach (var id in order)
        {
            var (q, v) = (byId[id].q, byId[id].v);
            var choices = v.Choices.OrderBy(c => c.SortOrder).ToList();
            if (exam.ShuffleChoices && q.Type != QuestionType.TrueFalse) choices = choices.OrderBy(_ => rng.Next()).ToList();
            var snapshot = JsonSerializer.Serialize(choices.Select(c => new SnapshotChoice(c.Id, c.Content, c.IsCorrect)), Json);
            _db.AttemptQuestions.Add(new AttemptQuestion(attempt.Id, ++no, q.Id, v.Id, q.Type, v.Content, v.Explanation, v.Points, snapshot));
        }
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (_db.IsUniqueViolation(ex))
        {
            // Another request for the same candidate won the race: hand back that attempt.
            _db.ResetTracking();
            var winner = await ActiveAsync(examId, candidateId, ct);
            if (winner is not null && winner.DeadlineUtc > now) return Resume(winner, now);
            throw AppException.Conflict("attempt_conflict", "Đang có yêu cầu bắt đầu bài thi khác. Vui lòng tải lại trang.");
        }
        _db.AttemptEvents.Add(new AttemptEvent(attempt.Id, "start", null, now));
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.attempt.start", "Attempt", attempt.Id.ToString(), new { examId, attempt.AttemptNo, questions = order.Count }, ct);
        return new StartResult(attempt.Id, false, attempt.AttemptNo, attempt.DeadlineUtc, now);
    }

    /// <summary>The candidate's own attempt (404 for anyone else's, anti-IDOR).</summary>
    public async Task<AttemptView> ViewAsync(Guid candidateId, Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await OwnAsync(candidateId, attemptId, ct);
        var now = _clock.UtcNow;
        if (!attempt.IsFinished && attempt.DeadlineUtc <= now)
        {
            await _finisher.FinishAsync(attempt.Id, AttemptStatus.Expired, ct);
            attempt = await OwnAsync(candidateId, attemptId, ct);
        }
        var exam = await _db.Exams.AsNoTracking().SingleAsync(e => e.Id == attempt.ExamId, ct);
        var questions = await _db.AttemptQuestions.AsNoTracking().Where(q => q.AttemptId == attemptId).OrderBy(q => q.OrderNo).ToListAsync(ct);
        var answers = await _db.Answers.AsNoTracking().Where(a => a.AttemptId == attemptId).ToListAsync(ct);
        return new AttemptView(attempt.Id, exam.Id, exam.Title, exam.Instructions, attempt.AttemptNo, attempt.Status.ToString(), attempt.StartedUtc,
            attempt.DeadlineUtc, now, attempt.LastSeq, exam.LogTabSwitches,
            questions.Select(q => new AttemptQuestionView(q.Id, q.OrderNo, q.Type.ToString(), q.Content, q.Points,
                Choices(q).Select(c => new AttemptChoiceView(c.Id, c.Content)).ToList())).ToList(),
            answers.Select(a => new AnswerView(a.AttemptQuestionId, Selected(a), a.Flagged, a.Seq)).ToList());
    }

    public async Task<Attempt> OwnAsync(Guid candidateId, Guid attemptId, CancellationToken ct) =>
        await _db.Attempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == attemptId && a.CandidateId == candidateId, ct)
        ?? throw AppException.NotFound("Không tìm thấy bài làm.");

    public static IReadOnlyList<SnapshotChoice> Choices(AttemptQuestion q) =>
        JsonSerializer.Deserialize<List<SnapshotChoice>>(q.ChoicesJson, Json) ?? new List<SnapshotChoice>();

    public static IReadOnlyList<Guid> Selected(Answer a) => JsonSerializer.Deserialize<List<Guid>>(a.SelectedJson, Json) ?? new List<Guid>();

    private Task<Attempt?> ActiveAsync(Guid examId, Guid candidateId, CancellationToken ct) =>
        _db.Attempts.AsNoTracking().SingleOrDefaultAsync(a => a.ExamId == examId && a.CandidateId == candidateId && a.Status == AttemptStatus.InProgress, ct);

    private static StartResult Resume(Attempt a, DateTime now) => new(a.Id, true, a.AttemptNo, a.DeadlineUtc, now);

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

/// <summary>Closes an attempt and grades it from its snapshot (idempotent).</summary>
public interface IAttemptFinisher
{
    Task FinishAsync(Guid attemptId, AttemptStatus status, CancellationToken ct = default);
}
