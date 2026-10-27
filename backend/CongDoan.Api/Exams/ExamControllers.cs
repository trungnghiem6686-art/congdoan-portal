using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Exams;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Exams;

public sealed record SetQuestionsRequest(IReadOnlyList<Guid> QuestionIds);
public sealed record SetMatrixRequest(IReadOnlyList<MatrixRuleInput> Rules);
public sealed record AssignmentsRequest(IReadOnlyList<AssignmentInput>? Add, IReadOnlyList<Guid>? Remove);

[Route("api/admin/exams")]
[RequirePermission(Permissions.ExamManage)]
public sealed class AdminExamsController : ApiControllerBase
{
    private readonly ExamService _svc;
    private readonly ExamTemplateService _templates;

    public AdminExamsController(ExamService svc, ExamTemplateService templates)
    {
        _svc = svc;
        _templates = templates;
    }

    [HttpGet] public async Task<IActionResult> List([FromQuery] PageQuery query, [FromQuery] string? status, [FromQuery] bool deleted, CancellationToken ct) => Envelope(await _svc.ListAsync(query, status, deleted, ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Envelope(await _svc.GetAsync(id, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] SaveExam cmd, CancellationToken ct) => Created(await _svc.CreateAsync(cmd, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] SaveExam cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));
    [HttpPut("{id:guid}/questions")] public async Task<IActionResult> Questions(Guid id, [FromBody] SetQuestionsRequest req, CancellationToken ct) => Envelope(await _svc.SetQuestionsAsync(id, req.QuestionIds ?? Array.Empty<Guid>(), ct));
    [HttpPut("{id:guid}/matrix")] public async Task<IActionResult> Matrix(Guid id, [FromBody] SetMatrixRequest req, CancellationToken ct) => Envelope(await _svc.SetMatrixAsync(id, req.Rules, ct));
    [HttpPut("{id:guid}/assignments")] public async Task<IActionResult> Assign(Guid id, [FromBody] AssignmentsRequest req, CancellationToken ct) => Envelope(await _svc.UpdateAssignmentsAsync(id, req.Add, req.Remove, ct));
    [HttpGet("{id:guid}/candidates")] public async Task<IActionResult> Candidates(Guid id, [FromQuery] PageQuery query, CancellationToken ct) => Envelope(await _svc.CandidatesAsync(id, query, ct));
    [HttpPost("{id:guid}/check")] public async Task<IActionResult> Check(Guid id, CancellationToken ct) => Envelope(await _svc.CheckAsync(id, ct));

    [HttpPost("{id:guid}/publish")]
    [RequirePermission(Permissions.ExamPublish)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct) => Envelope(await _svc.PublishAsync(id, ct));

    [HttpPost("{id:guid}/unpublish")]
    [RequirePermission(Permissions.ExamPublish)]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct) => Envelope(await _svc.SetStatusAsync(id, "unpublish", ct));

    [HttpPost("{id:guid}/close")] public async Task<IActionResult> Close(Guid id, CancellationToken ct) => Envelope(await _svc.SetStatusAsync(id, "close", ct));
    [HttpPost("{id:guid}/archive")] public async Task<IActionResult> Archive(Guid id, CancellationToken ct) => Envelope(await _svc.SetStatusAsync(id, "archive", ct));

    [HttpPost("{id:guid}/approve-results")]
    [RequirePermission(Permissions.ExamPublish)]
    public async Task<IActionResult> ApproveResults(Guid id, CancellationToken ct) => Envelope(await _svc.SetStatusAsync(id, "approve-results", ct));

    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, ct); return Envelope(new { deleted = true }); }

    [HttpGet("/api/admin/exam-templates")] public async Task<IActionResult> Templates(CancellationToken ct) => Envelope(await _templates.ListAsync(ct));
    [HttpPost("/api/admin/exam-templates")] public async Task<IActionResult> CreateTemplate([FromBody] SaveExamTemplate cmd, CancellationToken ct) => Created(await _templates.SaveAsync(null, cmd, ct));
    [HttpPut("/api/admin/exam-templates/{templateId:guid}")] public async Task<IActionResult> UpdateTemplate(Guid templateId, [FromBody] SaveExamTemplate cmd, CancellationToken ct) => Envelope(await _templates.SaveAsync(templateId, cmd, ct));
    [HttpDelete("/api/admin/exam-templates/{templateId:guid}")] public async Task<IActionResult> DeleteTemplate(Guid templateId, CancellationToken ct) { await _templates.DeleteAsync(templateId, ct); return Envelope(new { deleted = true }); }
    [HttpPost("from-template/{templateId:guid}")] public async Task<IActionResult> FromTemplate(Guid templateId, [FromBody] FromTemplate cmd, CancellationToken ct) => Created(await _templates.CreateExamAsync(templateId, cmd, ct));
}

[Route("api/thi/exams")]
[Authorize(Policy = AuthSchemes.CandidatePolicy)]
public sealed class CandidateExamsController : ApiControllerBase
{
    private readonly ExamService _svc;
    private readonly ICurrentUser _user;

    public CandidateExamsController(ExamService svc, ICurrentUser user)
    {
        _svc = svc;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Envelope(await _svc.CandidateExamsAsync(_user.CandidateId!.Value, ct));
    }
}
