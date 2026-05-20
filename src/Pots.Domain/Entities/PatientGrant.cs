namespace Pots.Domain.Entities;

public sealed class PatientGrant
{
    // Internal-only identity. UUIDv7 for time-ordered insert locality on the
    // audit-adjacent grant history.
    public Guid Id { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid GranteeUserId { get; private set; }
    public GrantRole Role { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }

    public bool IsActive => RevokedAt is null;

    private PatientGrant() { }

    // Enforces CLAUDE.md §6 ("Patient owns their data"): only the patient
    // owner may issue grants. Application-layer auth must also gate calls
    // to this method, but the invariant lives here as the floor.
    //
    // grantedByUserId and patientOwnerUserId are kept as separate parameters
    // on purpose: the equality check makes the invariant LOUD and testable.
    // Collapsing them would hide the rule. The application layer reads
    // grantedByUserId from the authenticated session and patientOwnerUserId
    // from the Patient aggregate; this method asserts they match.
    public static PatientGrant Issue(
        Guid patientId,
        Guid granteeUserId,
        GrantRole role,
        Guid grantedByUserId,
        Guid patientOwnerUserId)
    {
        if (patientId == Guid.Empty)
            throw new DomainException("Patient is required.");
        if (granteeUserId == Guid.Empty)
            throw new DomainException("Grantee is required.");
        if (grantedByUserId == Guid.Empty)
            throw new DomainException("Granter is required.");
        if (patientOwnerUserId == Guid.Empty)
            throw new DomainException("Patient owner is required.");
        if (grantedByUserId != patientOwnerUserId)
            throw new DomainException("Only the patient owner may issue grants.");
        if (granteeUserId == patientOwnerUserId)
            throw new DomainException("Owner already has full access; cannot grant to themselves.");
        if (!Enum.IsDefined(role))
            throw new DomainException("Grant role is not valid.");

        return new PatientGrant
        {
            Id = Guid.CreateVersion7(),
            PatientId = patientId,
            GranteeUserId = granteeUserId,
            Role = role,
            GrantedAt = DateTimeOffset.UtcNow,
            GrantedByUserId = grantedByUserId,
            RevokedAt = null,
            RevokedByUserId = null,
        };
    }

    public void Revoke(Guid revokedByUserId)
    {
        if (revokedByUserId == Guid.Empty)
            throw new DomainException("Revoker is required.");
        if (RevokedAt is not null) return;
        RevokedAt = DateTimeOffset.UtcNow;
        RevokedByUserId = revokedByUserId;
    }

    // Used by the approve-upgrade flow. Pre-Phase 3, Editor could be issued
    // directly at invite time; from Phase 3 onwards all invites are Viewer
    // and Editor is only reachable through an approved GrantUpgradeRequest.
    // The owner check stays in the application layer (grants_owner_modify
    // RLS policy is the DB floor); this method is the domain invariant on
    // the transition itself.
    public void UpgradeToEditor()
    {
        if (!IsActive)
            throw new DomainException("Cannot upgrade a revoked grant.");
        if (Role == GrantRole.Editor) return;
        Role = GrantRole.Editor;
    }
}
