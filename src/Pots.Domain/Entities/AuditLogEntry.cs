namespace Pots.Domain.Entities;

public sealed class AuditLogEntry
{
    // UUIDv7: append-only audit benefits from time-ordered insert locality.
    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid? PatientId { get; private set; }
    public string Action { get; private set; } = null!;
    public string EntityType { get; private set; } = null!;
    public Guid? EntityId { get; private set; }
    public string? ChangesJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AuditLogEntry() { }

    public static AuditLogEntry Record(
        Guid actorUserId,
        string action,
        string entityType,
        Guid? entityId,
        Guid? patientId,
        string? changesJson)
    {
        if (actorUserId == Guid.Empty)
            throw new DomainException("Actor is required.");
        if (string.IsNullOrWhiteSpace(action))
            throw new DomainException("Audit action is required.");
        if (string.IsNullOrWhiteSpace(entityType))
            throw new DomainException("Audit entity type is required.");

        return new AuditLogEntry
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actorUserId,
            Action = action.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId,
            PatientId = patientId,
            ChangesJson = changesJson,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
