using System.Text.Json;
using CongDoan.Application.Cms;
using CongDoan.Application.Shared;
using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Exams;

public sealed record ChoiceInput(string? Content, bool IsCorrect);

public sealed record SaveQuestion(string? Type, string? Topic, string? Difficulty, string? Content, string? Explanation, decimal Points,
    IReadOnlyList<ChoiceInput>? Choices, string? Code = null, Guid? OrgUnitId = null, string? RowVersion = null);

public sealed record ChoiceDto(Guid Id, string Content, bool IsCorrect, int SortOrder);

public sealed record QuestionVersionInfo(Guid Id, int VersionNo, DateTime CreatedUtc, Guid? CreatedBy);

public sealed record QuestionDto(Guid Id, string? Code, string Type, string Topic, string Difficulty, Guid? OrgUnitId, string Status,
    int VersionNo, Guid VersionId, string Content, string? Explanation, decimal Points, IReadOnlyList<ChoiceDto> Choices,
    DateTime UpdatedUtc, bool IsDeleted, string RowVersion, IReadOnlyList<QuestionVersionInfo> Versions);

public sealed record QuestionListItem(Guid Id, string? Code, string Type, string Topic, string Difficulty, string Status, int VersionNo,
    string Content, DateTime UpdatedUtc);

public sealed record QuestionFilter(int Page = 1, int PageSize = 20, string? Q = null, string? Topic = null, string? Difficulty = null,
    string? Type = null, string? Status = null, Guid? OrgUnitId = null, bool Deleted = false);

/// <summary>A question as validated, ready to be stored.</summary>
public sealed record ValidQuestion(QuestionType Type, string Topic, Difficulty Difficulty, string Content, string? Explanation, decimal Points,
    IReadOnlyList<(string Content, bool IsCorrect)> Choices, string? Code, Guid? OrgUnitId);

public static class QuestionValidator
{
    public const int MaxChoices = 8;

    public static ValidQuestion Validate(SaveQuestion q, IContentSanitizer sanitizer)
    {
        var errors = Errors(q, out var valid, sanitizer);
        if (errors.Count > 0) throw AppException.Validation(string.Join(" ", errors));
        return valid!;
    }

    public static List<string> Errors(SaveQuestion q, out ValidQuestion? valid, IContentSanitizer sanitizer)
    {
        valid = null;
        var errors = new List<string>();
        if (!Enum.TryParse<QuestionType>(q.Type, true, out var type)) errors.Add("Loại câu hỏi không hợp lệ.");
        if (!Enum.TryParse<Difficulty>(q.Difficulty, true, out var difficulty)) errors.Add("Mức độ không hợp lệ.");
        if (string.IsNullOrWhiteSpace(q.Topic)) errors.Add("Chủ đề là bắt buộc.");
        var content = sanitizer.Sanitize(q.Content);
        if (PostService.StripTags(content).Trim().Length == 0) errors.Add("Nội dung câu hỏi là bắt buộc.");
        if (q.Points <= 0 || q.Points > 100) errors.Add("Điểm phải lớn hơn 0 và không quá 100.");

        var choices = (q.Choices ?? Array.Empty<ChoiceInput>())
            .Where(c => !string.IsNullOrWhiteSpace(c.Content)).Select(c => (Content: c.Content!.Trim(), c.IsCorrect)).ToList();
        var correct = choices.Count(c => c.IsCorrect);
        if (choices.Count < 2) errors.Add("Cần ít nhất 2 phương án.");
        if (choices.Count > MaxChoices) errors.Add($"Tối đa {MaxChoices} phương án.");
        if (correct == 0) errors.Add("Cần ít nhất một đáp án đúng.");
        if (choices.Select(c => Slug.SearchText(c.Content)).GroupBy(x => x).Any(g => g.Count() > 1)) errors.Add("Có phương án trùng nhau.");
        if (type is QuestionType.Single or QuestionType.TrueFalse && correct > 1) errors.Add("Câu một đáp án chỉ được có đúng một đáp án đúng.");
        if (type == QuestionType.TrueFalse && choices.Count != 2) errors.Add("Câu đúng/sai phải có đúng 2 phương án.");
        if (errors.Count == 0)
            valid = new ValidQuestion(type, q.Topic!.Trim(), difficulty, content, sanitizer.Sanitize(q.Explanation), q.Points, choices,
                string.IsNullOrWhiteSpace(q.Code) ? null : q.Code.Trim(), q.OrgUnitId);
        return errors;
    }
}

public sealed class QuestionService
{
    private readonly IExamDbContext _db;
    private readonly IContentSanitizer _sanitizer;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public QuestionService(IExamDbContext db, IContentSanitizer sanitizer, ICurrentUser user, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _sanitizer = sanitizer;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<PagedResult<QuestionListItem>> ListAsync(QuestionFilter f, CancellationToken ct = default)
    {
        var page = new PageQuery(f.Page, f.PageSize);
        var q = _db.Questions.AsNoTracking().Where(x => x.IsDeleted == f.Deleted);
        if (!string.IsNullOrWhiteSpace(f.Topic)) q = q.Where(x => x.Topic == f.Topic);
        if (Enum.TryParse<Difficulty>(f.Difficulty, true, out var d)) q = q.Where(x => x.Difficulty == d);
        if (Enum.TryParse<QuestionType>(f.Type, true, out var t)) q = q.Where(x => x.Type == t);
        if (Enum.TryParse<QuestionStatus>(f.Status, true, out var s)) q = q.Where(x => x.Status == s);
        if (f.OrgUnitId is not null) q = q.Where(x => x.OrgUnitId == f.OrgUnitId);
        var rows = from x in q
                   join v in _db.QuestionVersions on x.CurrentVersionId equals v.Id
                   select new { x, v.Content };
        if (!string.IsNullOrWhiteSpace(f.Q)) rows = rows.Where(r => r.Content.Contains(f.Q) || (r.x.Code != null && r.x.Code.Contains(f.Q)));
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(r => r.x.UpdatedUtc).Skip(page.Skip).Take(page.SafeSize)
            .Select(r => new QuestionListItem(r.x.Id, r.x.Code, r.x.Type.ToString(), r.x.Topic, r.x.Difficulty.ToString(), r.x.Status.ToString(),
                r.x.CurrentVersionNo, r.Content, r.x.UpdatedUtc)).ToListAsync(ct);
        return new PagedResult<QuestionListItem>(items, total, page.SafePage, page.SafeSize);
    }

    public async Task<IReadOnlyList<string>> TopicsAsync(CancellationToken ct = default) =>
        await _db.Questions.AsNoTracking().Where(q => !q.IsDeleted).Select(q => q.Topic).Distinct().OrderBy(t => t).ToListAsync(ct);

    public async Task<QuestionDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var q = await _db.Questions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        return await ToDtoAsync(q, ct);
    }

    public async Task<QuestionDto> CreateAsync(SaveQuestion cmd, CancellationToken ct = default)
    {
        var v = QuestionValidator.Validate(cmd, _sanitizer);
        var q = Create(v);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.question.create", "Question", q.Id.ToString(), new { q.Topic, q.Type }, ct);
        return await ToDtoAsync(q, ct);
    }

    public Question Create(ValidQuestion v, string? sourceKey = null)
    {
        var q = new Question(v.Type, v.Topic, v.Difficulty, _user.AdminId, _clock.UtcNow) { Code = v.Code, OrgUnitId = v.OrgUnitId, SourceKey = sourceKey };
        var version = q.AddVersion(v.Content, v.Explanation, v.Points, v.Choices, _user.AdminId, _clock.UtcNow);
        _db.Questions.Add(q);
        _db.QuestionVersions.Add(version);
        return q;
    }

    public async Task<QuestionDto> UpdateAsync(Guid id, SaveQuestion cmd, CancellationToken ct = default)
    {
        var v = QuestionValidator.Validate(cmd, _sanitizer);
        var q = await _db.Questions.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw AppException.NotFound();
        _db.ExpectRowVersion(q, RowVersions.Decode(cmd.RowVersion));
        q.Type = v.Type;
        q.Topic = v.Topic;
        q.Difficulty = v.Difficulty;
        q.Code = v.Code;
        q.OrgUnitId = v.OrgUnitId;
        _db.QuestionVersions.Add(q.AddVersion(v.Content, v.Explanation, v.Points, v.Choices, _user.AdminId, _clock.UtcNow));
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.question.update", "Question", id.ToString(), new { q.CurrentVersionNo }, ct);
        return await ToDtoAsync(q, ct);
    }

    public async Task<QuestionDto> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var q = await _db.Questions.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw AppException.NotFound();
        q.Approve(_user.AdminId, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.question.approve", "Question", id.ToString(), new { q.CurrentVersionNo }, ct);
        return await ToDtoAsync(q, ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var q = await _db.Questions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        q.Archive();
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("exam.question.archive", "Question", id.ToString(), null, ct);
    }

    public async Task SetDeletedAsync(Guid id, bool deleted, CancellationToken ct = default)
    {
        var q = await _db.Questions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        q.IsDeleted = deleted;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(deleted ? "exam.question.delete" : "exam.question.restore", "Question", id.ToString(), null, ct);
    }

    /// <summary>Only approved, non-deleted questions may be put into an exam.</summary>
    public async Task EnsureUsableAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var list = ids.Distinct().ToList();
        var ok = await _db.Questions.AsNoTracking()
            .CountAsync(q => list.Contains(q.Id) && q.Status == QuestionStatus.Approved && !q.IsDeleted, ct);
        if (ok != list.Count)
            throw new AppException("question_not_approved", $"Có {list.Count - ok} câu hỏi chưa được duyệt hoặc đã bị xóa.", 400);
    }

    private async Task<QuestionDto> ToDtoAsync(Question q, CancellationToken ct)
    {
        var current = await _db.QuestionVersions.AsNoTracking().Include(v => v.Choices).SingleAsync(v => v.Id == q.CurrentVersionId, ct);
        var versions = await _db.QuestionVersions.AsNoTracking().Where(v => v.QuestionId == q.Id).OrderByDescending(v => v.VersionNo)
            .Select(v => new QuestionVersionInfo(v.Id, v.VersionNo, v.CreatedUtc, v.CreatedBy)).ToListAsync(ct);
        return new QuestionDto(q.Id, q.Code, q.Type.ToString(), q.Topic, q.Difficulty.ToString(), q.OrgUnitId, q.Status.ToString(), q.CurrentVersionNo,
            current.Id, current.Content, current.Explanation, current.Points,
            current.Choices.OrderBy(c => c.SortOrder).Select(c => new ChoiceDto(c.Id, c.Content, c.IsCorrect, c.SortOrder)).ToList(),
            q.UpdatedUtc, q.IsDeleted, RowVersions.Encode(q.RowVersion), versions);
    }
}

public sealed record ImportPreviewRow(int Row, string Type, string Topic, string Difficulty, string Content, int ChoiceCount, string Correct);

public sealed record ImportPreview(Guid PreviewId, int RowCount, IReadOnlyList<RowError> Errors, IReadOnlyList<ImportPreviewRow> Rows);

public sealed class QuestionImportService
{
    public static readonly string[] Header =
        { "chu_de", "muc_do", "loai", "noi_dung", "dap_an_a", "dap_an_b", "dap_an_c", "dap_an_d", "dap_an_dung", "giai_thich", "diem" };

    private static readonly string[] Letters = { "a", "b", "c", "d", "e", "f", "g", "h" };
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromHours(2);

    private readonly IExamDbContext _db;
    private readonly IContentSanitizer _sanitizer;
    private readonly QuestionService _questions;
    private readonly IQuestionTemplate _template;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public QuestionImportService(IExamDbContext db, IContentSanitizer sanitizer, QuestionService questions, IQuestionTemplate template,
        ICurrentUser user, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _sanitizer = sanitizer;
        _questions = questions;
        _template = template;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public Task<ImportPreview> PreviewCsvAsync(string csv, CancellationToken ct = default) => PreviewAsync(Csv.Parse(csv ?? ""), ct);

    public Task<ImportPreview> PreviewXlsxAsync(Stream xlsx, CancellationToken ct = default)
    {
        IReadOnlyList<string[]> rows;
        try { rows = _template.ReadXlsx(xlsx); }
        catch (Exception ex) when (ex is not AppException) { throw AppException.Validation("Không đọc được tệp Excel."); }
        return PreviewAsync(rows, ct);
    }

    /// <summary>Validates every row; nothing is written to the question bank until commit.</summary>
    private async Task<ImportPreview> PreviewAsync(IReadOnlyList<string[]> table, CancellationToken ct)
    {
        if (table.Count == 0) throw AppException.Validation("Tệp trống.");
        var header = table[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        string Cell(string[] r, string col) { var i = Array.IndexOf(header, col); return i >= 0 && i < r.Length ? r[i].Trim() : ""; }

        var errors = new List<RowError>();
        var valid = new List<(int Row, SaveQuestion Q)>();
        var rows = new List<ImportPreviewRow>();
        for (var r = 1; r < table.Count; r++)
        {
            var cells = table[r];
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            var rowNo = r + 1;
            var rowErrors = new List<string>();
            var type = Cell(cells, "loai").ToLowerInvariant() switch
            {
                "mot_dap_an" or "single" => "Single",
                "nhieu_dap_an" or "multiple" => "Multiple",
                "dung_sai" or "truefalse" => "TrueFalse",
                var other => other,
            };
            var difficulty = Cell(cells, "muc_do").ToLowerInvariant() switch
            {
                "de" or "dễ" or "easy" => "Easy",
                "trung_binh" or "trung bình" or "medium" => "Medium",
                "kho" or "khó" or "hard" => "Hard",
                var other => other,
            };
            var options = Letters.Select(l => Cell(cells, "dap_an_" + l)).ToList();
            var correctLetters = Cell(cells, "dap_an_dung").ToLowerInvariant().Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (var l in correctLetters)
            {
                var idx = Array.IndexOf(Letters, l);
                if (idx < 0 || string.IsNullOrWhiteSpace(options[idx])) rowErrors.Add($"đáp án đúng \"{l.ToUpperInvariant()}\" không có phương án tương ứng");
            }
            var choices = options.Select((text, i) => new ChoiceInput(text, correctLetters.Contains(Letters[i]))).Where(c => !string.IsNullOrWhiteSpace(c.Content)).ToList();
            var pointsText = Cell(cells, "diem");
            var points = 1m;
            if (pointsText.Length > 0 && !decimal.TryParse(pointsText.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out points))
                rowErrors.Add("điểm không phải là số");
            var save = new SaveQuestion(type, Cell(cells, "chu_de"), difficulty, Cell(cells, "noi_dung"), Cell(cells, "giai_thich"), points, choices, Cell(cells, "ma"));
            rowErrors.AddRange(QuestionValidator.Errors(save, out _, _sanitizer).Select(e => e.TrimEnd('.').ToLowerInvariant()));
            rows.Add(new ImportPreviewRow(rowNo, type, save.Topic ?? "", difficulty, save.Content ?? "", choices.Count, string.Join(",", correctLetters).ToUpperInvariant()));
            if (rowErrors.Count > 0) errors.Add(new RowError(rowNo, string.Join("; ", rowErrors.Distinct())));
            else valid.Add((rowNo, save));
        }
        if (rows.Count == 0) throw AppException.Validation("Tệp không có dòng dữ liệu.");
        if (rows.Count > 5000) throw AppException.Validation("Mỗi lần nhập tối đa 5000 câu.");

        var import = new QuestionImport(_user.AdminId, _clock.UtcNow, rows.Count, errors.Count, JsonSerializer.Serialize(valid.Select(v => v.Q).ToList()));
        _db.QuestionImports.Add(import);
        await _db.SaveChangesAsync(ct);
        return new ImportPreview(import.Id, rows.Count, errors, rows.Take(200).ToList());
    }

    /// <summary>Writes every previewed question in one transaction, or nothing if the preview had any error.</summary>
    public async Task<int> CommitAsync(Guid previewId, CancellationToken ct = default)
    {
        var created = 0;
        await _db.InTransactionAsync(async () =>
        {
            var import = await _db.QuestionImports.SingleOrDefaultAsync(i => i.Id == previewId && i.CreatedBy == _user.AdminId, ct)
                         ?? throw AppException.NotFound("Không tìm thấy bản xem trước.");
            if (import.CommittedUtc is not null) throw AppException.Conflict("already_committed", "Bản xem trước này đã được nhập.");
            if (import.CreatedUtc.Add(PreviewLifetime) < _clock.UtcNow) throw AppException.Validation("Bản xem trước đã hết hạn, vui lòng tải lại tệp.");
            if (import.ErrorCount > 0)
                throw new AppException("import_invalid", $"Tệp còn {import.ErrorCount} dòng lỗi. Sửa tệp và xem trước lại; không câu nào được ghi.", 400);
            var rows = JsonSerializer.Deserialize<List<SaveQuestion>>(import.PayloadJson) ?? new();
            foreach (var row in rows) _questions.Create(QuestionValidator.Validate(row, _sanitizer));
            import.CommittedUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            created = rows.Count;
        }, ct: ct);
        await _audit.WriteAsync("exam.question.import", "QuestionImport", previewId.ToString(), new { created }, ct);
        return created;
    }

    public byte[] TemplateCsv()
    {
        var sample = "Kiến thức chung,de,mot_dap_an,\"Công đoàn Việt Nam được thành lập năm nào? (MẪU)\",1929,1930,1945,1950,A,Câu hỏi mẫu,1";
        return System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(string.Join(",", Header) + "\n" + sample + "\n")).ToArray();
    }

    public byte[] TemplateXlsx() => _template.Xlsx(new[]
    {
        new[] { "Kiến thức chung", "de", "mot_dap_an", "Công đoàn Việt Nam được thành lập năm nào? (MẪU)", "1929", "1930", "1945", "1950", "A", "Câu hỏi mẫu", "1" },
    });
}
