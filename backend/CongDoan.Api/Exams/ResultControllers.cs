using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Exams;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Exams;

[Route("api/thi/attempts")]
[Authorize(Policy = AuthSchemes.CandidatePolicy)]
public sealed class CandidateResultsController : ApiControllerBase
{
    private readonly ResultService _svc;
    private readonly ICurrentUser _user;

    public CandidateResultsController(ResultService svc, ICurrentUser user)
    {
        _svc = svc;
        _user = user;
    }

    [HttpGet("{attemptId:guid}/result")]
    public async Task<IActionResult> Result(Guid attemptId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store, private";
        return Envelope(await _svc.CandidateAsync(_user.CandidateId!.Value, attemptId, ct));
    }
}

[Route("api/admin")]
public sealed class AdminResultsController : ApiControllerBase
{
    private readonly ResultService _results;
    private readonly ReportService _reports;

    public AdminResultsController(ResultService results, ReportService reports)
    {
        _results = results;
        _reports = reports;
    }

    [HttpGet("exams/{examId:guid}/attempts")]
    [RequirePermission(Permissions.ExamResultView)]
    public async Task<IActionResult> Attempts(Guid examId, CancellationToken ct) => Envelope(await _results.ExamAttemptsAsync(examId, ct));

    [HttpGet("attempts/{attemptId:guid}")]
    [RequirePermission(Permissions.ExamResultView)]
    public async Task<IActionResult> Attempt(Guid attemptId, CancellationToken ct) => Envelope(await _results.AdminAsync(attemptId, ct));

    [HttpGet("attempts/{attemptId:guid}/detail")]
    [RequirePermission(Permissions.ExamResultDetail)]
    public async Task<IActionResult> Detail(Guid attemptId, CancellationToken ct) => Envelope(await _results.AdminDetailAsync(attemptId, ct));

    [HttpPost("attempts/{attemptId:guid}/regrade")]
    [RequirePermission(Permissions.ExamRegrade)]
    public async Task<IActionResult> Regrade(Guid attemptId, [FromBody] RegradeRequest req, CancellationToken ct) => Envelope(await _results.RegradeAsync(attemptId, req, ct));

    [HttpGet("reports/exams/{examId:guid}")]
    [RequirePermission(Permissions.ReportView)]
    public async Task<IActionResult> Report(Guid examId, [FromQuery] ReportFilter filter, CancellationToken ct) => Envelope(await _reports.ExamAsync(examId, filter, ct));

    [HttpPost("reports/exams/{examId:guid}/export")]
    [RequirePermission(Permissions.ReportExport)]
    public async Task<IActionResult> Export(Guid examId, [FromQuery] ReportFilter filter, [FromQuery] string format = "csv", CancellationToken ct = default)
    {
        var file = await _reports.ExportAsync(examId, filter, format == "xlsx" ? "xlsx" : "csv", ct);
        Response.Headers.CacheControl = "no-store";
        return File(file.Content, file.ContentType, file.FileName);
    }
}
