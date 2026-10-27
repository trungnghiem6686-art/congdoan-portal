using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Exams;

public sealed record SaveExam(string? Title, string? Code, string? Description, string? Instructions, DateTime OpensAtUtc, DateTime ClosesAtUtc,
    int DurationMinutes, int MaxAttempts, string? ScoreRule, decimal PassScore, string? ResultRelease, bool ShowAnswerDetail = false,
    bool ShowExplanation = false, bool ShuffleQuestions = false, bool ShuffleChoices = false, bool LogTabSwitches = false,
    string? QuestionSource = "Manual", int RoundingDecimals = 2, string? RowVersion = null);

public sealed record MatrixRuleInput(string? Topic, string? Difficulty, string? Type, int Count);

public sealed record MatrixRuleDto(Guid Id, string? Topic, string? Difficulty, string? Type, int Count, int Available);

public sealed record AssignmentInput(string TargetType, Guid TargetId);

public sealed record AssignmentDto(Guid Id, string TargetType, Guid TargetId);

public sealed record ExamDto(Guid Id, string? Code, string Title, string? Description, string? Instructions, DateTime OpensAtUtc, DateTime ClosesAtUtc,
    int DurationMinutes, int MaxAttempts, string ScoreRule, decimal PassScore, int RoundingDecimals, string ResultRelease, bool ShowAnswerDetail,
    bool ShowExplanation, bool ShuffleQuestions, bool ShuffleChoices, bool LogTabSwitches, string QuestionSource, string Status, string Phase,
    bool ResultsApproved, IReadOnlyList<Guid> QuestionIds, IReadOnlyList<MatrixRuleDto> Matrix, IReadOnlyList<AssignmentDto> Assignments,
    int AttemptCount, string RowVersion);

public sealed record ExamListItem(Guid Id, string? Code, string Title, DateTime OpensAtUtc, DateTime ClosesAtUtc, string Status, string Phase, int AttemptCount);

public sealed record ExamCheck(bool Ok, IReadOnlyList<string> Problems, int QuestionCount);

public sealed record CandidateExam(Guid Id, string Title, string? Description, DateTime OpensAtUtc, DateTime ClosesAtUtc, int DurationMinutes,
    string Phase, int MaxAttempts, int AttemptsUsed, int AttemptsRemaining, Guid? ActiveAttemptId, decimal? FinalScore, bool? Passed, bool ResultVisible);

public sealed class ExamService
{
    private readonly IExamDbContext _db;
    private readonly QuestionService _questions;
    private readonly ICandidateDirectory _directory;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public ExamService(IExamDbContext db, QuestionService questions, ICandidateDirectory directory, ICurrentUser user, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _questions = questions;
        _directory = directory;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<PagedResult<ExamListItem>> ListAsync(PageQuery q, string? status, bool deleted, CancellationToken ct = default)
    {
        var query = _db.Exams.AsNoTracking().Where(e => e.IsDeleted == deleted);
        if (Enum.TryParse<ExamStatus>(status, true, out var st)) query = query.Where(e => e.Status == st);
        if (!string.IsNullOrWhiteSpace(q.Q)) query = query.Where(e => e.Title.Contains(q.Q) || (e.Code != null && e.Code.Contains(q.Q)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(e => e.OpensAtUtc).Skip(q.Skip).Take(q.SafeSize)
            .Select(e => new { e, attempts = _db.Attempts.Count(a => a.ExamId == e.Id) }).ToListAsync(ct);
        var now = _clock.UtcNow;
        return new PagedResult<ExamListItem>(rows.Select(r => new ExamListItem(r.e.Id, r.e.Code, r.e.Title, r.e.OpensAtUtc, r.e.ClosesAtUtc,
            r.e.Status.ToString(), r.e.Phase(now), r.attempts)).ToList(), total, q.SafePage, q.SafeSize);
    }

    public async Task<ExamDto> GetAsync(Guid id, CancellationToken ct = default) => await ToDtoAsync(await LoadAsync(id, ct), ct);

    public async Task<ExamDto> CreateAsync(SaveExam cmd, CancellationToken ct = default)
    {
        var exam = new Exam(Required(cmd.Title), _user.AdminId, _clock.UtcNow);
        Apply(exam, cmd);
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.exam.create", "Exam", exam.Id.ToString(), new { exam.Title }, ct);
        return await ToDtoAsync(exam, ct);
    }

    public async Task<ExamDto> UpdateAsync(Guid id, SaveExam cmd, CancellationToken ct = default)
    {
        var exam = await LoadAsync(id, ct);
        if (cmd.RowVersion is not null) _db.ExpectRowVersion(exam, Cms.RowVersions.Decode(cmd.RowVersion));
        var started = await _db.Attempts.AnyAsync(a => a.ExamId == id, ct);
        if (started && (exam.QuestionSource.ToString() != cmd.QuestionSource || exam.DurationMinutes != cmd.DurationMinutes))
            throw AppException.Conflict("exam_started", "Kỳ thi đã có lượt làm bài; không đổi nguồn đề hoặc thời lượng.");
        exam.Title = Required(cmd.Title);
        Apply(exam, cmd);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.exam.update", "Exam", id.ToString(), new { exam.Title, exam.OpensAtUtc, exam.ClosesAtUtc }, ct);
        return await ToDtoAsync(exam, ct);
    }

    public async Task<ExamDto> SetQuestionsAsync(Guid id, IReadOnlyList<Guid> questionIds, CancellationToken ct = default)
    {
        var exam = await EditableAsync(id, ct);
        var ids = questionIds.Distinct().ToList();
        await _questions.EnsureUsableAsync(ids, ct);
        _db.ExamQuestions.RemoveRange(await _db.ExamQuestions.Where(q => q.ExamId == id).ToListAsync(ct));
        for (var i = 0; i < ids.Count; i++) _db.ExamQuestions.Add(new ExamQuestion(id, ids[i], i));
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.exam.questions", "Exam", id.ToString(), new { count = ids.Count }, ct);
        return await ToDtoAsync(exam, ct);
    }

    public async Task<ExamDto> SetMatrixAsync(Guid id, IReadOnlyList<MatrixRuleInput> rules, CancellationToken ct = default)
    {
        var exam = await EditableAsync(id, ct);
        _db.ExamMatrixRules.RemoveRange(await _db.ExamMatrixRules.Where(r => r.ExamId == id).ToListAsync(ct));
        var order = 0;
        foreach (var r in rules ?? Array.Empty<MatrixRuleInput>())
        {
            if (r.Count is < 1 or > 500) throw AppException.Validation("Số câu mỗi quy tắc phải từ 1 đến 500.");
            Difficulty? d = string.IsNullOrWhiteSpace(r.Difficulty) ? null
                : Enum.TryParse<Difficulty>(r.Difficulty, true, out var dv) ? dv : throw AppException.Validation("Mức độ không hợp lệ.");
            QuestionType? t = string.IsNullOrWhiteSpace(r.Type) ? null
                : Enum.TryParse<QuestionType>(r.Type, true, out var tv) ? tv : throw AppException.Validation("Loại câu hỏi không hợp lệ.");
            _db.ExamMatrixRules.Add(new ExamMatrixRule(id, string.IsNullOrWhiteSpace(r.Topic) ? null : r.Topic.Trim(), d, t, r.Count, order++));
        }
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(exam, ct);
    }

    public async Task<IReadOnlyList<AssignmentDto>> UpdateAssignmentsAsync(Guid id, IReadOnlyList<AssignmentInput>? add, IReadOnlyList<Guid>? remove, CancellationToken ct = default)
    {
        await LoadAsync(id, ct);
        var current = await _db.ExamAssignments.Where(a => a.ExamId == id).ToListAsync(ct);
        _db.ExamAssignments.RemoveRange(current.Where(a => remove?.Contains(a.Id) == true || remove?.Contains(a.TargetId) == true));
        foreach (var a in add ?? Array.Empty<AssignmentInput>())
        {
            if (!Enum.TryParse<AssignmentTarget>(a.TargetType, true, out var type)) throw AppException.Validation("Loại đối tượng giao thi không hợp lệ.");
            if (current.Any(x => x.TargetType == type && x.TargetId == a.TargetId)) continue;
            var entity = new ExamAssignment(id, type, a.TargetId);
            _db.ExamAssignments.Add(entity);
            current.Add(entity);
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.exam.assignments", "Exam", id.ToString(), new { added = add?.Count ?? 0, removed = remove?.Count ?? 0 }, ct);
        return await AssignmentsAsync(id, ct);
    }

    public async Task<PagedResult<CandidateBrief>> CandidatesAsync(Guid id, PageQuery q, CancellationToken ct = default)
    {
        await LoadAsync(id, ct);
        return await _directory.PageAsync(await AssignedQueryAsync(id, ct), q, ct);
    }

    /// <summary>All candidate ids the exam is assigned to (direct, group members, org unit and below).</summary>
    public async Task<IQueryable<Guid>> AssignedQueryAsync(Guid examId, CancellationToken ct = default)
    {
        var rows = await _db.ExamAssignments.AsNoTracking().Where(a => a.ExamId == examId).ToListAsync(ct);
        var units = await _directory.DescendantUnitsAsync(rows.Where(r => r.TargetType == AssignmentTarget.OrgUnit).Select(r => r.TargetId).ToList(), ct);
        return _directory.Expand(
            rows.Where(r => r.TargetType == AssignmentTarget.Candidate).Select(r => r.TargetId).ToList(),
            rows.Where(r => r.TargetType == AssignmentTarget.Group).Select(r => r.TargetId).ToList(),
            units.ToList());
    }

    public async Task<bool> IsAssignedAsync(Guid examId, Guid candidateId, CancellationToken ct = default)
    {
        var membership = await _directory.MembershipAsync(candidateId, ct);
        if (membership is null) return false;
        return await AssignedFilter(_db.ExamAssignments.AsNoTracking(), membership).AnyAsync(a => a.ExamId == examId, ct);
    }

    public async Task<ExamCheck> CheckAsync(Guid id, CancellationToken ct = default)
    {
        var exam = await LoadAsync(id, ct);
        var problems = new List<string>();
        if (exam.ClosesAtUtc <= exam.OpensAtUtc) problems.Add("Giờ đóng phải sau giờ mở.");
        if (!await _db.ExamAssignments.AnyAsync(a => a.ExamId == id, ct)) problems.Add("Kỳ thi chưa được giao cho thí sinh, nhóm hoặc đơn vị nào.");
        var count = 0;
        if (exam.QuestionSource == QuestionSource.Manual)
        {
            var ids = await _db.ExamQuestions.Where(q => q.ExamId == id).Select(q => q.QuestionId).ToListAsync(ct);
            count = ids.Count;
            if (count == 0) problems.Add("Chưa chọn câu hỏi cho đề thi.");
            var usable = await UsableQuestions().CountAsync(q => ids.Contains(q.Id), ct);
            if (usable < count) problems.Add($"Có {count - usable} câu hỏi chưa được duyệt hoặc đã bị xóa.");
        }
        else
        {
            var rules = await _db.ExamMatrixRules.AsNoTracking().Where(r => r.ExamId == id).OrderBy(r => r.SortOrder).ToListAsync(ct);
            if (rules.Count == 0) problems.Add("Chưa có ma trận đề.");
            var index = 0;
            foreach (var r in rules)
            {
                index++;
                var available = await Matching(r).CountAsync(ct);
                if (available < r.Count) problems.Add($"Quy tắc {index} cần {r.Count} câu nhưng ngân hàng chỉ có {available} câu phù hợp đã duyệt.");
            }
            count = rules.Sum(r => r.Count);
            if (problems.Count == 0)
            {
                try { await DrawAsync(id, new Random(0), ct); }
                catch (AppException ex) { problems.Add(ex.Message); }
            }
        }
        return new ExamCheck(problems.Count == 0, problems, count);
    }

    public async Task<ExamDto> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var check = await CheckAsync(id, ct);
        if (!check.Ok) throw new AppException("exam_not_ready", "Kỳ thi chưa đủ điều kiện công bố: " + string.Join(" ", check.Problems), 400);
        var exam = await LoadAsync(id, ct);
        exam.Publish(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.exam.publish", "Exam", id.ToString(), new { check.QuestionCount }, ct);
        return await ToDtoAsync(exam, ct);
    }

    public async Task<ExamDto> SetStatusAsync(Guid id, string action, CancellationToken ct = default)
    {
        var exam = await LoadAsync(id, ct);
        switch (action)
        {
            case "unpublish":
                if (await _db.Attempts.AnyAsync(a => a.ExamId == id, ct))
                    throw AppException.Conflict("exam_started", "Kỳ thi đã có lượt làm bài, không thể hủy công bố. Hãy đóng kỳ thi.");
                exam.Unpublish();
                break;
            case "close": exam.Close(); break;
            case "archive": exam.Archive(); break;
            case "approve-results": exam.ApproveResults(_clock.UtcNow); break;
            default: throw AppException.Validation("Thao tác không hợp lệ.");
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync($"exam.exam.{action}", "Exam", id.ToString(), null, ct);
        return await ToDtoAsync(exam, ct);
    }

    /// <summary>Soft delete is only allowed before anyone has taken the exam; afterwards archive it.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var exam = await LoadAsync(id, ct);
        if (await _db.Attempts.AnyAsync(a => a.ExamId == id, ct))
            throw AppException.Conflict("exam_has_results", "Kỳ thi đã có bài làm/kết quả nên không thể xóa; chỉ có thể đóng hoặc lưu trữ.");
        exam.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.exam.delete", "Exam", id.ToString(), null, ct);
    }

    /// <summary>Question ids for one attempt: manual list, or random draw (most specific rules first, never duplicates).</summary>
    public async Task<IReadOnlyList<Guid>> DrawAsync(Guid examId, Random rng, CancellationToken ct = default)
    {
        var exam = await _db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == examId, ct) ?? throw AppException.NotFound();
        if (exam.QuestionSource == QuestionSource.Manual)
            return await _db.ExamQuestions.AsNoTracking().Where(q => q.ExamId == examId).OrderBy(q => q.SortOrder)
                .Join(UsableQuestions(), eq => eq.QuestionId, q => q.Id, (eq, q) => q.Id).ToListAsync(ct);

        var rules = await _db.ExamMatrixRules.AsNoTracking().Where(r => r.ExamId == examId).OrderBy(r => r.SortOrder).ToListAsync(ct);
        var pools = new List<(ExamMatrixRule Rule, List<Guid> Pool)>();
        foreach (var r in rules) pools.Add((r, await Matching(r).Select(q => q.Id).ToListAsync(ct)));
        var chosen = new List<Guid>();
        var taken = new HashSet<Guid>();
        foreach (var (rule, pool) in pools.OrderBy(p => p.Pool.Count))
        {
            var free = pool.Where(id => !taken.Contains(id)).ToList();
            if (free.Count < rule.Count)
                throw new AppException("exam_not_ready", $"Không đủ câu hỏi khác nhau cho quy tắc \"{rule.Topic ?? "mọi chủ đề"}\" (cần {rule.Count}, còn {free.Count}).", 400);
            foreach (var id in free.OrderBy(_ => rng.Next()).Take(rule.Count))
            {
                taken.Add(id);
                chosen.Add(id);
            }
        }
        return chosen;
    }

    /// <summary>Published exams assigned to the candidate with their own attempt summary.</summary>
    public async Task<IReadOnlyList<CandidateExam>> CandidateExamsAsync(Guid candidateId, CancellationToken ct = default)
    {
        var membership = await _directory.MembershipAsync(candidateId, ct);
        if (membership is null) return Array.Empty<CandidateExam>();
        var examIds = await AssignedFilter(_db.ExamAssignments.AsNoTracking(), membership).Select(a => a.ExamId).Distinct().ToListAsync(ct);
        var exams = await _db.Exams.AsNoTracking()
            .Where(e => examIds.Contains(e.Id) && !e.IsDeleted && e.Status != ExamStatus.Draft && e.Status != ExamStatus.Archived)
            .OrderBy(e => e.OpensAtUtc).ToListAsync(ct);
        var attempts = await _db.Attempts.AsNoTracking().Where(a => a.CandidateId == candidateId && examIds.Contains(a.ExamId)).ToListAsync(ct);
        var now = _clock.UtcNow;
        return exams.Select(e =>
        {
            var mine = attempts.Where(a => a.ExamId == e.Id && a.Status != AttemptStatus.Voided).ToList();
            var active = mine.FirstOrDefault(a => a.Status == AttemptStatus.InProgress && a.DeadlineUtc > now);
            var visible = ResultPolicy.IsReleased(e, now);
            var final = visible ? ResultPolicy.Final(e.ScoreRule, mine) : null;
            return new CandidateExam(e.Id, e.Title, e.Description, e.OpensAtUtc, e.ClosesAtUtc, e.DurationMinutes, e.Phase(now), e.MaxAttempts,
                mine.Count, Math.Max(0, e.MaxAttempts - mine.Count), active?.Id, final?.Score, final?.Passed, visible);
        }).ToList();
    }

    public IQueryable<Question> UsableQuestions() => _db.Questions.AsNoTracking().Where(q => q.Status == QuestionStatus.Approved && !q.IsDeleted);

    private IQueryable<Question> Matching(ExamMatrixRule r)
    {
        var q = UsableQuestions();
        if (r.Topic is not null) q = q.Where(x => x.Topic == r.Topic);
        if (r.Difficulty is not null) q = q.Where(x => x.Difficulty == r.Difficulty);
        if (r.Type is not null) q = q.Where(x => x.Type == r.Type);
        return q;
    }

    private static IQueryable<ExamAssignment> AssignedFilter(IQueryable<ExamAssignment> q, CandidateMembership m)
    {
        var units = m.OrgUnitIds.ToList();
        var groups = m.GroupIds.ToList();
        return q.Where(a => (a.TargetType == AssignmentTarget.Candidate && a.TargetId == m.CandidateId)
                            || (a.TargetType == AssignmentTarget.Group && groups.Contains(a.TargetId))
                            || (a.TargetType == AssignmentTarget.OrgUnit && units.Contains(a.TargetId)));
    }

    private async Task<Exam> LoadAsync(Guid id, CancellationToken ct) =>
        await _db.Exams.SingleOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct) ?? throw AppException.NotFound();

    private async Task<Exam> EditableAsync(Guid id, CancellationToken ct)
    {
        var exam = await LoadAsync(id, ct);
        if (await _db.Attempts.AnyAsync(a => a.ExamId == id, ct))
            throw AppException.Conflict("exam_started", "Kỳ thi đã có lượt làm bài; không thể đổi đề.");
        return exam;
    }

    private async Task<IReadOnlyList<AssignmentDto>> AssignmentsAsync(Guid id, CancellationToken ct) =>
        await _db.ExamAssignments.AsNoTracking().Where(a => a.ExamId == id)
            .Select(a => new AssignmentDto(a.Id, a.TargetType.ToString(), a.TargetId)).ToListAsync(ct);

    private static void Apply(Exam e, SaveExam cmd)
    {
        if (cmd.ClosesAtUtc <= cmd.OpensAtUtc) throw AppException.Validation("Giờ đóng phải sau giờ mở.");
        if (cmd.DurationMinutes is < 1 or > 600) throw AppException.Validation("Thời lượng phải từ 1 đến 600 phút.");
        if (cmd.MaxAttempts is < 1 or > 50) throw AppException.Validation("Số lượt làm bài phải từ 1 đến 50.");
        if (cmd.PassScore is < 0 or > 10) throw AppException.Validation("Điểm đạt phải từ 0 đến 10.");
        if (cmd.RoundingDecimals is < 0 or > 3) throw AppException.Validation("Số chữ số làm tròn từ 0 đến 3.");
        e.Code = string.IsNullOrWhiteSpace(cmd.Code) ? null : cmd.Code.Trim();
        e.Description = cmd.Description?.Trim();
        e.Instructions = cmd.Instructions?.Trim();
        e.OpensAtUtc = DateTime.SpecifyKind(cmd.OpensAtUtc, DateTimeKind.Utc);
        e.ClosesAtUtc = DateTime.SpecifyKind(cmd.ClosesAtUtc, DateTimeKind.Utc);
        e.DurationMinutes = cmd.DurationMinutes;
        e.MaxAttempts = cmd.MaxAttempts;
        e.ScoreRule = Enum.TryParse<ScoreRule>(cmd.ScoreRule, true, out var sr) ? sr : throw AppException.Validation("Cách lấy điểm không hợp lệ.");
        e.PassScore = cmd.PassScore;
        e.RoundingDecimals = cmd.RoundingDecimals;
        e.ResultRelease = Enum.TryParse<ResultRelease>(cmd.ResultRelease, true, out var rr) ? rr : throw AppException.Validation("Chế độ công bố kết quả không hợp lệ.");
        e.ShowAnswerDetail = cmd.ShowAnswerDetail;
        e.ShowExplanation = cmd.ShowExplanation;
        e.ShuffleQuestions = cmd.ShuffleQuestions;
        e.ShuffleChoices = cmd.ShuffleChoices;
        e.LogTabSwitches = cmd.LogTabSwitches;
        e.QuestionSource = Enum.TryParse<QuestionSource>(cmd.QuestionSource, true, out var qs) ? qs : throw AppException.Validation("Nguồn đề không hợp lệ.");
    }

    private static string Required(string? title) =>
        string.IsNullOrWhiteSpace(title) ? throw AppException.Validation("Tên kỳ thi là bắt buộc.") : title.Trim();

    private async Task<ExamDto> ToDtoAsync(Exam e, CancellationToken ct)
    {
        var qids = await _db.ExamQuestions.AsNoTracking().Where(q => q.ExamId == e.Id).OrderBy(q => q.SortOrder).Select(q => q.QuestionId).ToListAsync(ct);
        var rules = await _db.ExamMatrixRules.AsNoTracking().Where(r => r.ExamId == e.Id).OrderBy(r => r.SortOrder).ToListAsync(ct);
        var matrix = new List<MatrixRuleDto>();
        foreach (var r in rules)
            matrix.Add(new MatrixRuleDto(r.Id, r.Topic, r.Difficulty?.ToString(), r.Type?.ToString(), r.Count, await Matching(r).CountAsync(ct)));
        var attempts = await _db.Attempts.CountAsync(a => a.ExamId == e.Id, ct);
        return new ExamDto(e.Id, e.Code, e.Title, e.Description, e.Instructions, e.OpensAtUtc, e.ClosesAtUtc, e.DurationMinutes, e.MaxAttempts,
            e.ScoreRule.ToString(), e.PassScore, e.RoundingDecimals, e.ResultRelease.ToString(), e.ShowAnswerDetail, e.ShowExplanation, e.ShuffleQuestions,
            e.ShuffleChoices, e.LogTabSwitches, e.QuestionSource.ToString(), e.Status.ToString(), e.Phase(_clock.UtcNow), e.ResultsApproved, qids, matrix,
            await AssignmentsAsync(e.Id, ct), attempts, Cms.RowVersions.Encode(e.RowVersion));
    }
}

/// <summary>When candidates may see results, and which attempt counts as the final score.</summary>
public static class ResultPolicy
{
    public static bool IsReleased(Exam e, DateTime nowUtc) => e.ResultRelease switch
    {
        ResultRelease.Immediately => true,
        ResultRelease.AfterClose => e.IsOver(nowUtc),
        _ => e.ResultsApproved,
    };

    public static Attempt? Final(ScoreRule rule, IEnumerable<Attempt> attempts)
    {
        var graded = attempts.Where(a => a.IsFinished && a.Status != AttemptStatus.Voided && a.Score is not null).ToList();
        if (graded.Count == 0) return null;
        return rule switch
        {
            ScoreRule.Latest => graded.MaxBy(a => a.AttemptNo),
            ScoreRule.First => graded.MinBy(a => a.AttemptNo),
            _ => graded.OrderByDescending(a => a.Score).ThenBy(a => a.AttemptNo).First(),
        };
    }
}
