using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Exams;

public sealed record ResultChoice(Guid Id, string Content, bool Selected, bool? IsCorrect);

public sealed record ResultQuestion(Guid Id, int OrderNo, string Type, string Content, decimal Points, decimal? Awarded, bool IsCorrect,
    IReadOnlyList<ResultChoice> Choices, string? Explanation);

public sealed record CandidateResult(Guid AttemptId, Guid ExamId, string ExamTitle, int AttemptNo, string Status, bool Visible, DateTime? FinishedUtc,
    decimal? Score, bool? Passed, int? CorrectCount, int QuestionCount, decimal? PassScore, string? Message, IReadOnlyList<ResultQuestion>? Questions);

public sealed record RevisionDto(Guid Id, decimal? OldScore, decimal NewScore, string Reason, Guid? By, DateTime AtUtc);

public sealed record AdminAttemptDto(Guid Id, Guid ExamId, Guid CandidateId, string? CandidateCode, string? CandidateName, int AttemptNo, string Status,
    DateTime StartedUtc, DateTime DeadlineUtc, DateTime? FinishedUtc, decimal? Points, decimal MaxScore, decimal? Score, bool? Passed, int CorrectCount,
    int TabSwitchCount, IReadOnlyList<RevisionDto> Revisions);

public sealed record RegradeRequest(decimal Score, string? Reason);

/// <summary>Results for candidates (release policy) and admins (score vs. answer detail are separate permissions).</summary>
public sealed class ResultService
{
    private readonly IExamDbContext _db;
    private readonly ICandidateDirectory _directory;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public ResultService(IExamDbContext db, ICandidateDirectory directory, ICurrentUser user, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _directory = directory;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<CandidateResult> CandidateAsync(Guid candidateId, Guid attemptId, CancellationToken ct = default)
    {
        var a = await _db.Attempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId && x.CandidateId == candidateId, ct)
                ?? throw AppException.NotFound("Không tìm thấy bài làm.");
        var exam = await _db.Exams.AsNoTracking().SingleAsync(e => e.Id == a.ExamId, ct);
        var count = await _db.AttemptQuestions.CountAsync(q => q.AttemptId == attemptId, ct);
        var visible = a.IsGraded && ResultPolicy.IsReleased(exam, _clock.UtcNow);
        var message = !a.IsFinished ? "Bài thi chưa nộp."
            : !visible ? exam.ResultRelease switch
            {
                ResultRelease.AfterClose => "Kết quả sẽ được công bố sau khi kỳ thi kết thúc.",
                ResultRelease.AfterApproval => "Kết quả sẽ được công bố sau khi hội đồng duyệt.",
                _ => "Đang chấm bài, vui lòng tải lại sau ít giây.",
            } : null;
        IReadOnlyList<ResultQuestion>? questions = visible && exam.ShowAnswerDetail
            ? await QuestionsAsync(attemptId, showKey: true, showExplanation: exam.ShowExplanation, ct)
            : null;
        return new CandidateResult(a.Id, exam.Id, exam.Title, a.AttemptNo, a.Status.ToString(), visible, a.FinishedUtc,
            visible ? a.Score : null, visible ? a.Passed : null, visible ? a.CorrectCount : null, count, visible ? exam.PassScore : null, message, questions);
    }

    public async Task<IReadOnlyList<AdminAttemptDto>> ExamAttemptsAsync(Guid examId, CancellationToken ct = default)
    {
        var attempts = await _db.Attempts.AsNoTracking().Where(a => a.ExamId == examId).OrderBy(a => a.StartedUtc).ToListAsync(ct);
        var people = (await _directory.BriefsAsync(attempts.Select(a => a.CandidateId).Distinct().ToList(), ct)).ToDictionary(b => b.Id);
        return attempts.Select(a => ToAdmin(a, people.GetValueOrDefault(a.CandidateId), Array.Empty<RevisionDto>())).ToList();
    }

    public async Task<AdminAttemptDto> AdminAsync(Guid attemptId, CancellationToken ct = default)
    {
        var a = await _db.Attempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId, ct) ?? throw AppException.NotFound();
        var person = (await _directory.BriefsAsync(new[] { a.CandidateId }, ct)).FirstOrDefault();
        var revisions = await _db.GradeRevisions.AsNoTracking().Where(r => r.AttemptId == attemptId).OrderByDescending(r => r.AtUtc)
            .Select(r => new RevisionDto(r.Id, r.OldScore, r.NewScore, r.Reason, r.By, r.AtUtc)).ToListAsync(ct);
        return ToAdmin(a, person, revisions);
    }

    public async Task<object> AdminDetailAsync(Guid attemptId, CancellationToken ct = default)
    {
        var summary = await AdminAsync(attemptId, ct);
        var events = await _db.AttemptEvents.AsNoTracking().Where(e => e.AttemptId == attemptId).OrderBy(e => e.AtUtc)
            .Select(e => new { e.Kind, e.Detail, e.AtUtc }).ToListAsync(ct);
        await _audit.WriteAsync("exam.result.detail_view", "Attempt", attemptId.ToString(), null, ct);
        return new { summary, questions = await QuestionsAsync(attemptId, true, true, ct), events };
    }

    /// <summary>Manual score override; the original score, reason and actor are kept in GradeRevision.</summary>
    public async Task<AdminAttemptDto> RegradeAsync(Guid attemptId, RegradeRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Reason)) throw AppException.Validation("Cần nêu lý do chấm lại.");
        if (req.Score is < 0 or > 10) throw AppException.Validation("Điểm phải từ 0 đến 10.");
        var a = await _db.Attempts.SingleOrDefaultAsync(x => x.Id == attemptId, ct) ?? throw AppException.NotFound();
        if (!a.IsGraded) throw AppException.Conflict("not_graded", "Bài làm chưa được chấm.");
        var exam = await _db.Exams.AsNoTracking().SingleAsync(e => e.Id == a.ExamId, ct);
        var old = a.Score;
        var score = Math.Round(req.Score, exam.RoundingDecimals, MidpointRounding.AwayFromZero);
        a.Regrade(a.MaxScore * score / 10, score, score >= exam.PassScore, a.CorrectCount);
        _db.GradeRevisions.Add(new GradeRevision(attemptId, old, score, req.Reason.Trim(), _user.AdminId, _clock.UtcNow));
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.result.regrade", "Attempt", attemptId.ToString(), new { oldScore = old, newScore = score, reason = req.Reason }, ct);
        return await AdminAsync(attemptId, ct);
    }

    private async Task<IReadOnlyList<ResultQuestion>> QuestionsAsync(Guid attemptId, bool showKey, bool showExplanation, CancellationToken ct)
    {
        var questions = await _db.AttemptQuestions.AsNoTracking().Where(q => q.AttemptId == attemptId).OrderBy(q => q.OrderNo).ToListAsync(ct);
        var answers = await _db.Answers.AsNoTracking().Where(a => a.AttemptId == attemptId).ToDictionaryAsync(a => a.AttemptQuestionId, ct);
        return questions.Select(q =>
        {
            var picked = answers.TryGetValue(q.Id, out var ans) ? AttemptService.Selected(ans).ToHashSet() : new HashSet<Guid>();
            var choices = AttemptService.Choices(q);
            var key = choices.Where(c => c.IsCorrect).Select(c => c.Id).ToHashSet();
            return new ResultQuestion(q.Id, q.OrderNo, q.Type.ToString(), q.Content, q.Points, ans?.AwardedPoints,
                picked.Count > 0 && picked.SetEquals(key),
                choices.Select(c => new ResultChoice(c.Id, c.Content, picked.Contains(c.Id), showKey ? c.IsCorrect : null)).ToList(),
                showExplanation ? q.Explanation : null);
        }).ToList();
    }

    private static AdminAttemptDto ToAdmin(Attempt a, CandidateBrief? p, IReadOnlyList<RevisionDto> revisions) =>
        new(a.Id, a.ExamId, a.CandidateId, p?.Code, p?.DisplayName, a.AttemptNo, a.Status.ToString(), a.StartedUtc, a.DeadlineUtc, a.FinishedUtc,
            a.Points, a.MaxScore, a.Score, a.Passed, a.CorrectCount, a.TabSwitchCount, revisions);
}

public sealed record ReportFilter(Guid? OrgUnitId = null, Guid? GroupId = null);

public sealed record ReportSummary(int AssignedPeople, int AttemptedPeople, int NotAttemptedPeople, int AttemptsTotal, int FinishedAttempts,
    int InProgressAttempts, int PassedPeople, decimal? PassRatePercent, string PassRateDenominator, decimal? AverageFinalScore,
    decimal? MinFinalScore, decimal? MaxFinalScore, IReadOnlyList<int> Distribution);

public sealed record ReportRow(Guid CandidateId, string Code, string Name, string? OrgUnit, int Attempts, decimal? FinalScore, bool? Passed,
    DateTime? LastFinishedUtc, int TabSwitches);

public sealed record ExamReport(Guid ExamId, string ExamTitle, string ScoreRule, decimal PassScore, ReportSummary Summary, IReadOnlyList<ReportRow> Rows);

public sealed record ExportFile(byte[] Content, string ContentType, string FileName);

/// <summary>Exam statistics. People and attempts are counted separately; every rate states its denominator.</summary>
public sealed class ReportService
{
    private readonly IExamDbContext _db;
    private readonly ExamService _exams;
    private readonly ICandidateDirectory _directory;
    private readonly ISpreadsheetWriter _xlsx;
    private readonly IAuditWriter _audit;

    public ReportService(IExamDbContext db, ExamService exams, ICandidateDirectory directory, ISpreadsheetWriter xlsx, IAuditWriter audit)
    {
        _db = db;
        _exams = exams;
        _directory = directory;
        _xlsx = xlsx;
        _audit = audit;
    }

    public async Task<ExamReport> ExamAsync(Guid examId, ReportFilter f, CancellationToken ct = default)
    {
        var exam = await _db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == examId && !e.IsDeleted, ct) ?? throw AppException.NotFound();
        var assigned = await (await _exams.AssignedQueryAsync(examId, ct)).ToListAsync(ct);
        var people = (await _directory.BriefsAsync(assigned, ct)).ToList();
        if (f.OrgUnitId is { } unit)
        {
            var units = (await _directory.DescendantUnitsAsync(new[] { unit }, ct)).ToHashSet();
            people = people.Where(p => p.OrgUnitId is { } u && units.Contains(u)).ToList();
        }
        if (f.GroupId is { } group)
        {
            var members = await _directory.Expand(Array.Empty<Guid>(), new[] { group }, Array.Empty<Guid>()).ToListAsync(ct);
            people = people.Where(p => members.Contains(p.Id)).ToList();
        }
        var ids = people.Select(p => p.Id).ToHashSet();
        var attempts = (await _db.Attempts.AsNoTracking().Where(a => a.ExamId == examId && a.Status != AttemptStatus.Voided).ToListAsync(ct))
            .Where(a => ids.Contains(a.CandidateId)).ToList();
        var byPerson = attempts.ToLookup(a => a.CandidateId);

        var rows = people.OrderBy(p => p.Code).Select(p =>
        {
            var mine = byPerson[p.Id].ToList();
            var final = ResultPolicy.Final(exam.ScoreRule, mine);
            return new ReportRow(p.Id, p.Code, p.DisplayName, p.OrgUnitName, mine.Count, final?.Score, final?.Passed,
                mine.Where(a => a.FinishedUtc != null).Max(a => a.FinishedUtc), mine.Sum(a => a.TabSwitchCount));
        }).ToList();

        var finals = rows.Where(r => r.FinalScore is not null).Select(r => r.FinalScore!.Value).ToList();
        var attempted = rows.Count(r => r.Attempts > 0);
        var passed = rows.Count(r => r.Passed == true);
        var distribution = new int[10];
        foreach (var s in finals) distribution[Math.Min(9, (int)Math.Floor(s))]++;
        var summary = new ReportSummary(people.Count, attempted, people.Count - attempted, attempts.Count,
            attempts.Count(a => a.IsFinished), attempts.Count(a => !a.IsFinished), passed,
            attempted == 0 ? null : Math.Round(passed * 100m / attempted, 2, MidpointRounding.AwayFromZero), "attemptedPeople",
            finals.Count == 0 ? null : Math.Round(finals.Average(), 2, MidpointRounding.AwayFromZero),
            finals.Count == 0 ? null : finals.Min(), finals.Count == 0 ? null : finals.Max(), distribution);
        return new ExamReport(exam.Id, exam.Title, exam.ScoreRule.ToString(), exam.PassScore, summary, rows);
    }

    public async Task<ExportFile> ExportAsync(Guid examId, ReportFilter f, string format, CancellationToken ct = default)
    {
        var report = await ExamAsync(examId, f, ct);
        var header = new[] { "Mã thí sinh", "Họ tên", "Đơn vị", "Số lượt", "Điểm cuối (thang 10)", "Kết quả", "Nộp gần nhất (giờ VN)", "Số lần rời tab" };
        IReadOnlyList<object?> Row(ReportRow r) => new object?[]
        {
            r.Code, r.Name, r.OrgUnit, r.Attempts, r.FinalScore, r.Passed is null ? "Chưa có" : r.Passed.Value ? "Đạt" : "Chưa đạt",
            VietnamTime.Format(r.LastFinishedUtc), r.TabSwitches,
        };
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        ExportFile file = format == "xlsx"
            ? new(_xlsx.Write("KetQua", header, report.Rows.Select(Row)), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ket-qua-{stamp}.xlsx")
            : new(CsvSanitizer.Document(header, report.Rows.Select(r => Row(r).Select(c => c switch
            {
                decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => c?.ToString(),
            }))), "text/csv; charset=utf-8", $"ket-qua-{stamp}.csv");
        await _audit.WriteAsync("report.export", "Exam", examId.ToString(), new { format, rows = report.Rows.Count, f.OrgUnitId, f.GroupId }, ct);
        return file;
    }
}
