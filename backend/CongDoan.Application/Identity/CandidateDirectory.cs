using CongDoan.Application.Shared;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Identity;

public sealed record CandidateMembership(Guid CandidateId, IReadOnlyList<Guid> OrgUnitIds, IReadOnlyList<Guid> GroupIds);

public sealed record CandidateBrief(Guid Id, string Code, string DisplayName, Guid? OrgUnitId, string? OrgUnitName);

/// <summary>What other modules may know about candidates (no direct access to identity tables).</summary>
public interface ICandidateDirectory
{
    /// <summary>The candidate's org unit with all its ancestors, and the groups they belong to.</summary>
    Task<CandidateMembership?> MembershipAsync(Guid candidateId, CancellationToken ct = default);

    /// <summary>Active candidates targeted directly, through a group, or through an org unit (descendants included).</summary>
    IQueryable<Guid> Expand(IReadOnlyCollection<Guid> candidateIds, IReadOnlyCollection<Guid> groupIds, IReadOnlyCollection<Guid> orgUnitIds);

    Task<IReadOnlyList<Guid>> DescendantUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, CancellationToken ct = default);

    Task<IReadOnlyList<CandidateBrief>> BriefsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    Task<PagedResult<CandidateBrief>> PageAsync(IQueryable<Guid> ids, PageQuery q, CancellationToken ct = default);
}

public sealed class CandidateDirectory : ICandidateDirectory
{
    private readonly IIdentityDbContext _db;

    public CandidateDirectory(IIdentityDbContext db) => _db = db;

    public async Task<CandidateMembership?> MembershipAsync(Guid candidateId, CancellationToken ct = default)
    {
        var c = await _db.Candidates.AsNoTracking().Where(x => x.Id == candidateId && !x.IsDeleted).Select(x => new { x.Id, x.OrgUnitId }).SingleOrDefaultAsync(ct);
        if (c is null) return null;
        var units = new List<Guid>();
        if (c.OrgUnitId is { } unit)
        {
            var parents = await _db.OrgUnits.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.ParentId, ct);
            for (Guid? u = unit; u is not null && !units.Contains(u.Value); u = parents.GetValueOrDefault(u.Value)) units.Add(u.Value);
        }
        var groups = await _db.CandidateGroupMembers.AsNoTracking().Where(m => m.CandidateId == candidateId)
            .Join(_db.CandidateGroups.Where(g => !g.IsDeleted), m => m.GroupId, g => g.Id, (m, g) => g.Id).ToListAsync(ct);
        return new CandidateMembership(c.Id, units, groups);
    }

    public IQueryable<Guid> Expand(IReadOnlyCollection<Guid> candidateIds, IReadOnlyCollection<Guid> groupIds, IReadOnlyCollection<Guid> orgUnitIds) =>
        _db.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && (candidateIds.Contains(c.Id)
                || _db.CandidateGroupMembers.Any(m => m.CandidateId == c.Id && groupIds.Contains(m.GroupId))
                || (c.OrgUnitId != null && orgUnitIds.Contains(c.OrgUnitId.Value))))
            .Select(c => c.Id);

    public async Task<IReadOnlyList<Guid>> DescendantUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, CancellationToken ct = default)
    {
        if (orgUnitIds.Count == 0) return Array.Empty<Guid>();
        var all = await _db.OrgUnits.AsNoTracking().Where(o => !o.IsDeleted).Select(o => new { o.Id, o.ParentId }).ToListAsync(ct);
        var result = orgUnitIds.ToList();
        for (var i = 0; i < result.Count; i++)
            result.AddRange(all.Where(o => o.ParentId == result[i] && !result.Contains(o.Id)).Select(o => o.Id));
        return result;
    }

    public async Task<IReadOnlyList<CandidateBrief>> BriefsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        await Briefs(_db.Candidates.AsNoTracking().Where(c => ids.Contains(c.Id))).ToListAsync(ct);

    public async Task<PagedResult<CandidateBrief>> PageAsync(IQueryable<Guid> ids, PageQuery q, CancellationToken ct = default)
    {
        var query = _db.Candidates.AsNoTracking().Where(c => ids.Contains(c.Id));
        if (!string.IsNullOrWhiteSpace(q.Q)) query = query.Where(c => c.UserName.Contains(q.Q) || c.DisplayName.Contains(q.Q));
        var total = await query.CountAsync(ct);
        var items = await Briefs(query.OrderBy(c => c.UserName).Skip(q.Skip).Take(q.SafeSize)).ToListAsync(ct);
        return new PagedResult<CandidateBrief>(items, total, q.SafePage, q.SafeSize);
    }

    private IQueryable<CandidateBrief> Briefs(IQueryable<Domain.Identity.Candidate> q) =>
        from c in q
        join o in _db.OrgUnits on c.OrgUnitId equals o.Id into units
        from o in units.DefaultIfEmpty()
        select new CandidateBrief(c.Id, c.UserName, c.DisplayName, c.OrgUnitId, o == null ? null : o.Name);
}
