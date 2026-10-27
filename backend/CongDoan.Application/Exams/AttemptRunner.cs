using System.Text.Json;
using CongDoan.Application.Shared;
using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CongDoan.Application.Exams;

public sealed class ExamOptions
{
    /// <summary>Seconds after the deadline during which a request is still accepted. Default 0.</summary>
    public int DeadlineGraceSeconds { get; set; }
}

/// <summary>Autosave, tab ownership and submission for an attempt in progress. The server clock alone decides lateness.</summary>
public sealed class AttemptRunner : IAttemptFinisher
{
    public static readonly TimeSpan WriterLease = TimeSpan.FromSeconds(45);
    private static readonly HashSet<string> EventKinds = new() { "tab_hidden", "tab_visible", "offline", "online" };

    private readonly IExamDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly TimeSpan _grace;

    public AttemptRunner(IExamDbContext db, IClock clock, IAuditWriter audit, IOptions<ExamOptions> options)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        // Requests arriving after the deadline are refused; grace is explicit configuration (default 0).
        _grace = TimeSpan.FromSeconds(options.Value.DeadlineGraceSeconds);
    }

    public async Task<ClaimResult> ClaimAsync(Guid candidateId, Guid attemptId, bool force, CancellationToken ct = default)
    {
        var attempt = await _db.Attempts.SingleOrDefaultAsync(a => a.Id == attemptId && a.CandidateId == candidateId, ct) ?? throw AppException.NotFound();
        var now = _clock.UtcNow;
        await EnsureWritableAsync(attempt, now, ct);
        var leaseActive = attempt.WriterToken is not null && attempt.WriterSeenUtc + WriterLease > now;
        if (leaseActive && !force)
            throw AppException.Conflict("another_tab_active", "Bài thi đang được làm ở một tab hoặc thiết bị khác. Tab này chỉ để xem.");
        attempt.AssignWriter(AttemptService.NewToken(), now);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw AppException.Conflict("another_tab_active", "Một tab khác vừa nhận quyền làm bài. Vui lòng tải lại."); }
        _db.AttemptEvents.Add(new AttemptEvent(attemptId, force ? "writer_takeover" : "writer_claim", null, now));
        await _db.SaveChangesAsync(ct);
        return new ClaimResult(attempt.WriterToken!, attempt.DeadlineUtc, now);
    }

    public async Task HeartbeatAsync(Guid candidateId, Guid attemptId, string? writer, CancellationToken ct = default)
    {
        var attempt = await _db.Attempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == attemptId && a.CandidateId == candidateId, ct) ?? throw AppException.NotFound();
        var now = _clock.UtcNow;
        await EnsureWritableAsync(attempt, now, ct);
        EnsureWriter(attempt, writer);
        await _db.Attempts.Where(a => a.Id == attemptId && a.WriterToken == writer).ExecuteUpdateAsync(s => s.SetProperty(a => a.WriterSeenUtc, now), ct);
    }

    /// <summary>Stores an answer if its sequence number is newer than the stored one (late or replayed requests are ignored).</summary>
    public async Task<SaveResult> SaveAsync(Guid candidateId, Guid attemptId, Guid attemptQuestionId, SaveAnswer cmd, string? writer, CancellationToken ct = default)
    {
        var attempt = await _db.Attempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == attemptId && a.CandidateId == candidateId, ct) ?? throw AppException.NotFound();
        var now = _clock.UtcNow;
        await EnsureWritableAsync(attempt, now, ct);
        EnsureWriter(attempt, writer);
        if (cmd.Seq <= 0) throw AppException.Validation("Số thứ tự lưu không hợp lệ.");

        var question = await _db.AttemptQuestions.AsNoTracking().SingleOrDefaultAsync(q => q.Id == attemptQuestionId && q.AttemptId == attemptId, ct)
                       ?? throw AppException.NotFound("Câu hỏi không thuộc bài làm này.");
        var allowed = AttemptService.Choices(question).Select(c => c.Id).ToHashSet();
        var picked = (cmd.ChoiceIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (picked.Any(id => !allowed.Contains(id))) throw AppException.Validation("Phương án không thuộc câu hỏi.");
        if (question.Type != QuestionType.Multiple && picked.Count > 1) throw AppException.Validation("Câu hỏi này chỉ chọn một phương án.");
        var json = JsonSerializer.Serialize(picked, AttemptService.Json);

        var accepted = await ConditionalWriteAsync(attemptId, attemptQuestionId, json, cmd, now, ct);
        if (!accepted && !await _db.Answers.AnyAsync(a => a.AttemptId == attemptId && a.AttemptQuestionId == attemptQuestionId, ct))
        {
            // First save of this question; a racing first save may insert concurrently, then fall back to the conditional update.
            _db.Answers.Add(new Answer(attemptId, attemptQuestionId) { SelectedJson = json, Seq = cmd.Seq, Flagged = cmd.Flagged, SavedUtc = now });
            try
            {
                await _db.SaveChangesAsync(ct);
                accepted = true;
            }
            catch (DbUpdateException ex) when (_db.IsUniqueViolation(ex))
            {
                _db.ResetTracking();
                accepted = await ConditionalWriteAsync(attemptId, attemptQuestionId, json, cmd, now, ct);
            }
        }
        if (accepted)
            await _db.Attempts.Where(a => a.Id == attemptId && a.WriterToken == writer)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.WriterSeenUtc, now).SetProperty(a => a.LastSeq, a => a.LastSeq < cmd.Seq ? cmd.Seq : a.LastSeq), ct);
        else if (await _db.Attempts.AsNoTracking().AnyAsync(a => a.Id == attemptId && a.Status != AttemptStatus.InProgress, ct))
            throw AppException.Conflict("attempt_finished", "Bài thi đã kết thúc, không thể sửa câu trả lời.");
        return new SaveResult(accepted, cmd.Seq, now, accepted ? null : "stale");
    }

    private async Task<bool> ConditionalWriteAsync(Guid attemptId, Guid questionId, string json, SaveAnswer cmd, DateTime now, CancellationToken ct) =>
        await _db.Answers.Where(a => a.AttemptId == attemptId && a.AttemptQuestionId == questionId && a.Seq < cmd.Seq
                                     && _db.Attempts.Any(t => t.Id == attemptId && t.Status == AttemptStatus.InProgress))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.SelectedJson, json).SetProperty(a => a.Seq, cmd.Seq)
                .SetProperty(a => a.Flagged, cmd.Flagged).SetProperty(a => a.SavedUtc, now), ct) > 0;

    public async Task<SubmitResult> SubmitAsync(Guid candidateId, Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await _db.Attempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == attemptId && a.CandidateId == candidateId, ct) ?? throw AppException.NotFound();
        var late = _clock.UtcNow > attempt.DeadlineUtc + _grace;
        await FinishAsync(attemptId, late ? AttemptStatus.Expired : AttemptStatus.Submitted, ct);
        return await SummaryAsync(attemptId, ct);
    }

    /// <summary>
    /// Idempotent close-and-grade: the first caller flips the status atomically, grading is saved under the row version,
    /// so repeated or concurrent calls (double click, retry, expiry worker) produce one result and one audit row.
    /// </summary>
    public async Task FinishAsync(Guid attemptId, AttemptStatus status, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        await _db.Attempts.Where(a => a.Id == attemptId && a.Status == AttemptStatus.InProgress)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, status).SetProperty(a => a.FinishedUtc, now), ct);

        _db.ResetTracking();
        var attempt = await _db.Attempts.SingleOrDefaultAsync(a => a.Id == attemptId, ct) ?? throw AppException.NotFound();
        if (attempt.IsGraded || attempt.Status == AttemptStatus.Voided) return;
        var exam = await _db.Exams.AsNoTracking().SingleAsync(e => e.Id == attempt.ExamId, ct);
        var questions = await _db.AttemptQuestions.AsNoTracking().Where(q => q.AttemptId == attemptId).ToListAsync(ct);
        var answers = await _db.Answers.Where(a => a.AttemptId == attemptId).ToListAsync(ct);
        var grade = Grader.Grade(questions, answers, exam.PassScore, exam.RoundingDecimals);
        attempt.ApplyGrade(grade.CorrectCount, grade.Points, grade.Score, grade.Passed);
        foreach (var a in answers) a.AwardedPoints = grade.Awarded.GetValueOrDefault(a.AttemptQuestionId);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ResetTracking();
            return; // graded by a concurrent finisher
        }
        await _audit.WriteAsync("exam.attempt.submit", "Attempt", attemptId.ToString(),
            new { status = attempt.Status.ToString(), attempt.Score, attempt.Passed, grade.CorrectCount }, ct);
    }

    public async Task LogEventAsync(Guid candidateId, Guid attemptId, string kind, CancellationToken ct = default)
    {
        var attempt = await _db.Attempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == attemptId && a.CandidateId == candidateId, ct) ?? throw AppException.NotFound();
        if (!EventKinds.Contains(kind) || attempt.IsFinished) return;
        var exam = await _db.Exams.AsNoTracking().SingleAsync(e => e.Id == attempt.ExamId, ct);
        if (!exam.LogTabSwitches && kind is "tab_hidden" or "tab_visible") return;
        _db.AttemptEvents.Add(new AttemptEvent(attemptId, kind, null, _clock.UtcNow));
        await _db.SaveChangesAsync(ct);
        if (kind == "tab_hidden")
            await _db.Attempts.Where(a => a.Id == attemptId).ExecuteUpdateAsync(s => s.SetProperty(a => a.TabSwitchCount, a => a.TabSwitchCount + 1), ct);
    }

    public async Task<SubmitResult> SummaryAsync(Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await _db.Attempts.AsNoTracking().SingleAsync(a => a.Id == attemptId, ct);
        var exam = await _db.Exams.AsNoTracking().SingleAsync(e => e.Id == attempt.ExamId, ct);
        var count = await _db.AttemptQuestions.CountAsync(q => q.AttemptId == attemptId, ct);
        var visible = attempt.IsGraded && ResultPolicy.IsReleased(exam, _clock.UtcNow);
        return new SubmitResult(attempt.Id, attempt.Status.ToString(), visible, visible ? attempt.Score : null, visible ? attempt.Passed : null,
            visible ? attempt.CorrectCount : null, count);
    }

    private async Task EnsureWritableAsync(Attempt attempt, DateTime now, CancellationToken ct)
    {
        if (attempt.IsFinished) throw AppException.Conflict("attempt_finished", "Bài thi đã kết thúc, không thể sửa câu trả lời.");
        if (now > attempt.DeadlineUtc + _grace)
        {
            await FinishAsync(attempt.Id, AttemptStatus.Expired, ct);
            throw AppException.Conflict("deadline_passed", "Đã hết giờ làm bài. Bài làm đã được nộp với các câu trả lời đã lưu.");
        }
    }

    private static void EnsureWriter(Attempt attempt, string? writer)
    {
        if (!string.IsNullOrEmpty(writer) && writer == attempt.WriterToken) return;
        if (!string.IsNullOrEmpty(writer) && writer == attempt.PreviousWriterToken)
            throw AppException.Conflict("writer_superseded", "Bài thi đã được mở ở tab hoặc thiết bị khác. Tab này chuyển sang chế độ chỉ xem.");
        throw AppException.Conflict("read_only", "Tab này đang ở chế độ chỉ xem.");
    }
}
