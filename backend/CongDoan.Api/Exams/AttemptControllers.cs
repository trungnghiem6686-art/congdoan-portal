using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Exams;
using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Exams;

/// <summary>Candidate exam runner. Every response is personal: never cached anywhere.</summary>
[Route("api/thi")]
[Authorize(Policy = AuthSchemes.CandidatePolicy)]
public sealed class CandidateAttemptsController : ApiControllerBase
{
    private readonly AttemptService _attempts;
    private readonly AttemptRunner _runner;
    private readonly ICurrentUser _user;

    public CandidateAttemptsController(AttemptService attempts, AttemptRunner runner, ICurrentUser user)
    {
        _attempts = attempts;
        _runner = runner;
        _user = user;
    }

    public sealed record ClaimRequest(bool Force);
    public sealed record EventRequest(string Kind);

    private string? Writer => Request.Headers["X-Attempt-Writer"].ToString();

    private Guid Me => _user.CandidateId!.Value;

    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context) =>
        Response.Headers.CacheControl = "no-store, private";

    [HttpPost("exams/{examId:guid}/start")]
    public async Task<IActionResult> Start(Guid examId, CancellationToken ct) => Envelope(await _attempts.StartAsync(Me, examId, ct));

    [HttpGet("attempts/{attemptId:guid}")]
    public async Task<IActionResult> View(Guid attemptId, CancellationToken ct) => Envelope(await _attempts.ViewAsync(Me, attemptId, ct));

    [HttpPost("attempts/{attemptId:guid}/claim")]
    public async Task<IActionResult> Claim(Guid attemptId, [FromBody] ClaimRequest req, CancellationToken ct) => Envelope(await _runner.ClaimAsync(Me, attemptId, req.Force, ct));

    [HttpPost("attempts/{attemptId:guid}/heartbeat")]
    public async Task<IActionResult> Heartbeat(Guid attemptId, CancellationToken ct) { await _runner.HeartbeatAsync(Me, attemptId, Writer, ct); return Envelope(new { ok = true }); }

    [HttpPut("attempts/{attemptId:guid}/answers/{questionId:guid}")]
    public async Task<IActionResult> Save(Guid attemptId, Guid questionId, [FromBody] SaveAnswer cmd, CancellationToken ct) =>
        Envelope(await _runner.SaveAsync(Me, attemptId, questionId, cmd, Writer, ct));

    [HttpPost("attempts/{attemptId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid attemptId, CancellationToken ct) => Envelope(await _runner.SubmitAsync(Me, attemptId, ct));

    [HttpPost("attempts/{attemptId:guid}/events")]
    public async Task<IActionResult> Event(Guid attemptId, [FromBody] EventRequest req, CancellationToken ct) { await _runner.LogEventAsync(Me, attemptId, req.Kind, ct); return Envelope(new { ok = true }); }
}
