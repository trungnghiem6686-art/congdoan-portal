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
    private readonly ICurrentUser _user;

    public CandidateAttemptsController(AttemptService attempts, ICurrentUser user)
    {
        _attempts = attempts;
        _user = user;
    }

    private Guid Me => _user.CandidateId!.Value;

    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context) =>
        Response.Headers.CacheControl = "no-store, private";

    [HttpPost("exams/{examId:guid}/start")]
    public async Task<IActionResult> Start(Guid examId, CancellationToken ct) => Envelope(await _attempts.StartAsync(Me, examId, ct));

    [HttpGet("attempts/{attemptId:guid}")]
    public async Task<IActionResult> View(Guid attemptId, CancellationToken ct) => Envelope(await _attempts.ViewAsync(Me, attemptId, ct));
}
