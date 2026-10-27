using CongDoan.Api.Shared;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Identity;

public sealed record SetRolesRequest(IReadOnlyList<RoleGrant> Roles);
public sealed record ImportCsvRequest(string Csv);
public sealed record GroupMembersRequest(IReadOnlyList<Guid>? Add, IReadOnlyList<Guid>? Remove);

[Route("api/admin/users")]
[RequirePermission(Permissions.UserManage)]
public sealed class AdminUsersController : ApiControllerBase
{
    private readonly AdminUserService _svc;

    public AdminUsersController(AdminUserService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery query, [FromQuery] bool deleted, CancellationToken ct) =>
        Envelope(await _svc.ListAsync(query, deleted, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Envelope(await _svc.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAdminUser cmd, CancellationToken ct)
    {
        var created = await _svc.CreateAsync(cmd, ct);
        return Created(new { created.Account.Id, created.Account.UserName, created.Account.DisplayName, initialPassword = created.InitialPassword });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAdminUser cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] SetRolesRequest req, CancellationToken ct) =>
        Envelope(await _svc.SetRolesAsync(id, req.Roles ?? Array.Empty<RoleGrant>(), ct));

    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> Lock(Guid id, CancellationToken ct) { await _svc.LockAsync(id, ct); return Envelope(new { locked = true }); }

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct) { await _svc.UnlockAsync(id, ct); return Envelope(new { locked = false }); }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> Reset(Guid id, CancellationToken ct) => Envelope(new { password = await _svc.ResetPasswordAsync(id, ct) });

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, ct); return Envelope(new { deleted = true }); }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.RestoreAsync(id, ct); return Envelope(new { restored = true }); }
}

[Route("api/admin/roles")]
[RequirePermission(Permissions.RoleManage)]
public sealed class RolesController : ApiControllerBase
{
    private readonly RoleService _svc;

    public RolesController(RoleService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Envelope(await _svc.ListAsync(ct));

    [HttpGet("/api/admin/permissions")]
    public IActionResult Catalog() => Envelope(_svc.Catalog);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRole cmd, CancellationToken ct) => Created(await _svc.CreateAsync(cmd, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveRole cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, ct); return Envelope(new { deleted = true }); }
}

[Route("api/admin/org-units")]
public sealed class OrgUnitsController : ApiControllerBase
{
    private readonly OrgUnitService _svc;

    public OrgUnitsController(OrgUnitService svc) => _svc = svc;

    // Any admin may read the unit tree (needed by pickers); changes need the manage permission.
    [HttpGet]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = AuthSchemes.AdminPolicy)]
    public async Task<IActionResult> List([FromQuery] bool deleted, CancellationToken ct) => Envelope(await _svc.ListAsync(deleted, ct));

    [HttpPost]
    [RequirePermission(Permissions.OrgUnitManage)]
    public async Task<IActionResult> Create([FromBody] SaveOrgUnit cmd, CancellationToken ct) => Created(await _svc.CreateAsync(cmd, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.OrgUnitManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveOrgUnit cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.OrgUnitManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, true, ct); return Envelope(new { deleted = true }); }

    [HttpPost("{id:guid}/restore")]
    [RequirePermission(Permissions.OrgUnitManage)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, false, ct); return Envelope(new { restored = true }); }
}

[Route("api/admin/candidate-groups")]
public sealed class CandidateGroupsController : ApiControllerBase
{
    private readonly CandidateGroupService _svc;

    public CandidateGroupsController(CandidateGroupService svc) => _svc = svc;

    [HttpGet]
    [RequirePermission(Permissions.CandidateView)]
    public async Task<IActionResult> List([FromQuery] bool deleted, CancellationToken ct) => Envelope(await _svc.ListAsync(deleted, ct));

    [HttpPost]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Create([FromBody] SaveGroup cmd, CancellationToken ct) => Created(await _svc.CreateAsync(cmd, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveGroup cmd, CancellationToken ct) { await _svc.UpdateAsync(id, cmd, ct); return Envelope(new { updated = true }); }

    [HttpPut("{id:guid}/members")]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Members(Guid id, [FromBody] GroupMembersRequest req, CancellationToken ct)
    {
        await _svc.SetMembersAsync(id, req.Add ?? Array.Empty<Guid>(), req.Remove ?? Array.Empty<Guid>(), ct);
        return Envelope(new { updated = true });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, true, ct); return Envelope(new { deleted = true }); }

    [HttpPost("{id:guid}/restore")]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.SetDeletedAsync(id, false, ct); return Envelope(new { restored = true }); }
}

[Route("api/admin/candidates")]
public sealed class CandidatesController : ApiControllerBase
{
    private readonly CandidateService _svc;

    public CandidatesController(CandidateService svc) => _svc = svc;

    [HttpGet]
    [RequirePermission(Permissions.CandidateView)]
    public async Task<IActionResult> List([FromQuery] PageQuery query, [FromQuery] Guid? orgUnitId, [FromQuery] Guid? groupId, [FromQuery] bool deleted, CancellationToken ct) =>
        Envelope(await _svc.ListAsync(query, orgUnitId, groupId, deleted, ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CandidateView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Envelope(await _svc.GetAsync(id, ct));

    [HttpPost]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Create([FromBody] SaveCandidate cmd, CancellationToken ct)
    {
        var created = await _svc.CreateAsync(cmd, ct);
        return Created(new { candidate = created.Account, initialPassword = created.InitialPassword });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCandidate cmd, CancellationToken ct) => Envelope(await _svc.UpdateAsync(id, cmd, ct));

    [HttpPost("{id:guid}/lock")]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Lock(Guid id, CancellationToken ct) { await _svc.LockAsync(id, true, ct); return Envelope(new { locked = true }); }

    [HttpPost("{id:guid}/unlock")]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct) { await _svc.LockAsync(id, false, ct); return Envelope(new { locked = false }); }

    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Reset(Guid id, CancellationToken ct) => Envelope(new { password = await _svc.ResetPasswordAsync(id, ct) });

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, false, ct); return Envelope(new { deleted = true }); }

    [HttpPost("{id:guid}/restore")]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, true, ct); return Envelope(new { restored = true }); }

    [HttpPost("import")]
    [RequirePermission(Permissions.CandidateManage)]
    public async Task<IActionResult> Import([FromBody] ImportCsvRequest req, CancellationToken ct) => Envelope(await _svc.ImportAsync(req.Csv, ct));
}
