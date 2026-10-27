using System.Text.Json;
using CongDoan.Application.Cms;
using CongDoan.Application.Shared;
using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Exams;

public sealed record TemplateSettings(SaveExam Exam, IReadOnlyList<Guid>? QuestionIds, IReadOnlyList<MatrixRuleInput>? Matrix);

public sealed record SaveExamTemplate(string? Name, string? Description, TemplateSettings Settings);

public sealed record ExamTemplateDto(Guid Id, string Name, string? Description, TemplateSettings Settings, DateTime UpdatedUtc);

public sealed record FromTemplate(string? Title, DateTime OpensAtUtc, DateTime ClosesAtUtc);

public sealed class ExamTemplateService
{
    private readonly IExamDbContext _db;
    private readonly ExamService _exams;
    private readonly IClock _clock;

    public ExamTemplateService(IExamDbContext db, ExamService exams, IClock clock)
    {
        _db = db;
        _exams = exams;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ExamTemplateDto>> ListAsync(CancellationToken ct = default) =>
        (await _db.ExamTemplates.AsNoTracking().Where(t => !t.IsDeleted).OrderBy(t => t.Name).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<ExamTemplateDto> SaveAsync(Guid? id, SaveExamTemplate cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw AppException.Validation("Tên mẫu đề là bắt buộc.");
        var t = id is null ? null : await _db.ExamTemplates.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw AppException.NotFound();
        if (t is null) _db.ExamTemplates.Add(t = new ExamTemplate(cmd.Name.Trim()));
        t.Name = cmd.Name.Trim();
        t.Description = cmd.Description?.Trim();
        t.SettingsJson = JsonSerializer.Serialize(cmd.Settings, SectionCatalog.Json);
        t.UpdatedUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.ExamTemplates.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        t.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>New draft exam with the template's settings, questions/matrix and the given window.</summary>
    public async Task<ExamDto> CreateExamAsync(Guid templateId, FromTemplate cmd, CancellationToken ct = default)
    {
        var t = await _db.ExamTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == templateId && !x.IsDeleted, ct) ?? throw AppException.NotFound();
        var s = ToDto(t).Settings;
        var exam = await _exams.CreateAsync(s.Exam with { Title = cmd.Title ?? s.Exam.Title ?? t.Name, OpensAtUtc = cmd.OpensAtUtc, ClosesAtUtc = cmd.ClosesAtUtc, RowVersion = null }, ct);
        if (s.QuestionIds is { Count: > 0 }) exam = await _exams.SetQuestionsAsync(exam.Id, s.QuestionIds, ct);
        if (s.Matrix is { Count: > 0 }) exam = await _exams.SetMatrixAsync(exam.Id, s.Matrix, ct);
        return exam;
    }

    private static ExamTemplateDto ToDto(ExamTemplate t) =>
        new(t.Id, t.Name, t.Description, JsonSerializer.Deserialize<TemplateSettings>(t.SettingsJson, SectionCatalog.Json)!, t.UpdatedUtc);
}

/// <summary>Public list for the homepage "exam_notice" block: titles and windows only.</summary>
public sealed class ExamNoticeProvider : IExamNoticeProvider
{
    private readonly IExamDbContext _db;
    private readonly IClock _clock;

    public ExamNoticeProvider(IExamDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<object>> OpenExamsAsync(int limit, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var exams = await _db.Exams.AsNoTracking()
            .Where(e => e.Status == ExamStatus.Published && !e.IsDeleted && e.ClosesAtUtc > now)
            .OrderBy(e => e.OpensAtUtc).Take(limit).ToListAsync(ct);
        return exams.Select(e => (object)new { e.Id, e.Title, e.OpensAtUtc, e.ClosesAtUtc, e.DurationMinutes, phase = e.Phase(now) }).ToList();
    }
}
