using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Audit;

[Route("api/admin/dashboard")]
[Authorize(Policy = AuthSchemes.AdminPolicy)]
public sealed class DashboardController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromServices] DashboardService svc, CancellationToken ct) => Envelope(await svc.GetAsync(ct));
}
