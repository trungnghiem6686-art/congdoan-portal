using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Audit;

[Route("api/admin/audit")]
[RequirePermission(Permissions.AuditView)]
public sealed class AuditController : ApiControllerBase
{
    private readonly AuditQueryService _svc;

    public AuditController(AuditQueryService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AuditQuery query, CancellationToken ct) => Envelope(await _svc.ListAsync(query, ct));
}
