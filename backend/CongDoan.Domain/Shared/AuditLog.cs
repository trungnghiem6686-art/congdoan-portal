namespace CongDoan.Domain.Shared;

public sealed class AuditLog
{
    private AuditLog() { }

    public AuditLog(DateTime atUtc, string action, string entity, string entityId, string detail,
        Guid? actorId, string? actorName, string? actorKind, string? requestId, string? ipAddress)
    {
        AtUtc = atUtc;
        Action = action;
        Entity = entity;
        EntityId = entityId;
        Detail = detail;
        ActorId = actorId;
        ActorName = actorName;
        ActorKind = actorKind;
        RequestId = requestId;
        IpAddress = ipAddress;
    }

    public long Id { get; private set; }
    public DateTime AtUtc { get; private set; }
    public string Action { get; private set; } = "";
    public string Entity { get; private set; } = "";
    public string EntityId { get; private set; } = "";
    public string Detail { get; private set; } = "{}";
    public Guid? ActorId { get; private set; }
    public string? ActorName { get; private set; }
    public string? ActorKind { get; private set; }
    public string? RequestId { get; private set; }
    public string? IpAddress { get; private set; }
}
