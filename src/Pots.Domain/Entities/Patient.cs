namespace Pots.Domain.Entities;

// Decision (v1): 1:1 Patient↔Owner. Caregivers tracking dependents
// (e.g. parent of adolescent POTS patient) deferred. Dropping the
// unique index on owner_user_id later is cheap; adding the model
// support is the larger lift.
// TODO(v1.5): add Locale + TimeZone for reminders, charts, Doctor Report.
public sealed class Patient
{
    // External-facing identity: UUIDv4 to avoid leaking enrollment timestamp.
    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private Patient() { }

    public static Patient Create(Guid ownerUserId, string name)
    {
        if (ownerUserId == Guid.Empty)
            throw new DomainException("Owner is required.");

        var now = DateTimeOffset.UtcNow;
        return new Patient
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Name = NormalizeName(name),
            CreatedAt = now,
            UpdatedAt = now,
            DeletedAt = null,
        };
    }

    public void Rename(string name)
    {
        Name = NormalizeName(name);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SoftDelete()
    {
        if (DeletedAt is not null) return;
        var now = DateTimeOffset.UtcNow;
        DeletedAt = now;
        UpdatedAt = now;
    }

    private static string NormalizeName(string value)
    {
        if (value is null)
            throw new DomainException("Patient name cannot be null.");
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Patient name cannot be empty.");
        if (trimmed.Length > 100)
            throw new DomainException("Patient name is too long.");
        return trimmed;
    }
}
