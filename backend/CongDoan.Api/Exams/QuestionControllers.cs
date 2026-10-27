using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Exams;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Exams;

public sealed record CsvImportRequest(string? Csv);
public sealed record CommitImportRequest(Guid PreviewId);

[Route("api/admin/questions")]
[RequirePermission(Permissions.ExamQuestionManage)]
public sealed class AdminQuestionsController : ApiControllerBase
{
    private readonly QuestionService _svc;
    private readonly QuestionImportService _import;

    public AdminQuestionsController(QuestionService svc, QuestionImportService import)
    {
        _svc = svc;
        _import = import;
    }

    [HttpGet] public async Task<IActionResult> List([FromQuery] QuestionFilter filter, CancellationToken ct) => Envelope(await _svc.ListAsync(filter, ct));
    [HttpGet("topics")] public async Task<IActionResult> Topics(CancellationToken ct) => Envelope(await _svc.TopicsAsync(ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Envelope(await _svc.GetAsync(id, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] SaveQuestion cmd, CancellationToken ct) => Created(await _svc.CreateAsync(cmd, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] SaveQuestion cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));

    [HttpPost("{id:guid}/approve")]
    [RequirePermission(Permissions.ExamQuestionApprove)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => Envelope(await _svc.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/archive")] public async Task<IActionResult> Archive(Guid id, CancellationToken ct) { await _svc.ArchiveAsync(id, ct); return Envelope(new { archived = true }); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, true, ct); return Envelope(new { deleted = true }); }
    [HttpPost("{id:guid}/restore")] public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, false, ct); return Envelope(new { restored = true }); }

    /// <summary>Accepts JSON {csv} or a multipart CSV/XLSX file in field "file".</summary>
    [HttpPost("import/preview")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Preview(CancellationToken ct)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? throw AppException.Validation("Chưa chọn tệp.");
            await using var stream = file.OpenReadStream();
            if (file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return Envelope(await _import.PreviewXlsxAsync(stream, ct));
            using var reader = new StreamReader(stream);
            return Envelope(await _import.PreviewCsvAsync(await reader.ReadToEndAsync(ct), ct));
        }
        var body = await System.Text.Json.JsonSerializer.DeserializeAsync<CsvImportRequest>(Request.Body, Shared.ExceptionMiddleware.Json, ct);
        return Envelope(await _import.PreviewCsvAsync(body?.Csv ?? "", ct));
    }

    [HttpPost("import/commit")]
    public async Task<IActionResult> Commit([FromBody] CommitImportRequest req, CancellationToken ct) => Envelope(new { created = await _import.CommitAsync(req.PreviewId, ct) });

    [HttpGet("import/template")]
    public IActionResult Template([FromQuery] string format = "xlsx") => format == "csv"
        ? File(_import.TemplateCsv(), "text/csv; charset=utf-8", "mau-cau-hoi.csv")
        : File(_import.TemplateXlsx(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "mau-cau-hoi.xlsx");
}
