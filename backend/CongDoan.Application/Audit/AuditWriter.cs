using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CongDoan.Application.Shared;
using CongDoan.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Shared;

public interface IAuditDbContext
{
    DbSet<AuditLog> AuditLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IAuditWriter
{
    Task WriteAsync(string action, string entity, string entityId, object? detail = null, CancellationToken ct = default);
}

public sealed partial class AuditWriter : IAuditWriter
{
    private const string Redacted = "[đã ẩn]";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IAuditDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public AuditWriter(IAuditDbContext db, ICurrentUser user, IClock clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task WriteAsync(string action, string entity, string entityId, object? detail = null, CancellationToken ct = default)
    {
        var (actorId, kind) = _user.AdminId is { } a ? (a, "admin") : _user.CandidateId is { } c ? (c, "candidate") : ((Guid?)null, (string?)null);
        _db.AuditLogs.Add(new AuditLog(_clock.UtcNow, action, entity, entityId, Sanitize(detail),
            actorId, _user.UserName, kind, _user.RequestId, _user.IpAddress));
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Serializes detail with secrets and answer keys replaced, whatever the nesting.</summary>
    public static string Sanitize(object? detail)
    {
        if (detail is null) return "{}";
        var node = JsonSerializer.SerializeToNode(detail, Json);
        Redact(node);
        return node?.ToJsonString(Json) ?? "{}";
    }

    private static void Redact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (SensitiveKey().IsMatch(key)) obj[key] = Redacted;
                    else Redact(obj[key]);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) Redact(item);
                break;
        }
    }

    [GeneratedRegex("pass(word)?|pwd|secret|token|hash|correct|answerkey|explanation|solution|cookie", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveKey();
}

public sealed record AuditDto(long Id, DateTime AtUtc, string Action, string Entity, string EntityId, string Detail,
    Guid? ActorId, string? ActorName, string? ActorKind, string? RequestId, string? IpAddress);

public sealed record AuditQuery(int Page = 1, int PageSize = 50, string? Action = null, string? Entity = null,
    string? EntityId = null, Guid? ActorId = null, DateTime? FromUtc = null, DateTime? ToUtc = null);

public sealed class AuditQueryService
{
    private readonly IAuditDbContext _db;

    public AuditQueryService(IAuditDbContext db) => _db = db;

    public async Task<PagedResult<AuditDto>> ListAsync(AuditQuery q, CancellationToken ct = default)
    {
        var page = new PageQuery(q.Page, q.PageSize);
        var query = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Action)) query = query.Where(a => a.Action.StartsWith(q.Action));
        if (!string.IsNullOrWhiteSpace(q.Entity)) query = query.Where(a => a.Entity == q.Entity);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) query = query.Where(a => a.EntityId == q.EntityId);
        if (q.ActorId is not null) query = query.Where(a => a.ActorId == q.ActorId);
        if (q.FromUtc is not null) query = query.Where(a => a.AtUtc >= q.FromUtc);
        if (q.ToUtc is not null) query = query.Where(a => a.AtUtc < q.ToUtc);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.Id).Skip(page.Skip).Take(page.SafeSize)
            .Select(a => new AuditDto(a.Id, a.AtUtc, a.Action, a.Entity, a.EntityId, a.Detail, a.ActorId, a.ActorName, a.ActorKind, a.RequestId, a.IpAddress))
            .ToListAsync(ct);
        return new PagedResult<AuditDto>(items, total, page.SafePage, page.SafeSize);
    }
}
