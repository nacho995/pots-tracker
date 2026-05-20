namespace Pots.Domain.Entities;

// A Viewer-grant holder requests an upgrade to Editor on a specific patient.
// The patient owner then approves or denies. The requester can also cancel
// while still Pending.
//
// State machine:
//   Pending  → Approved   (owner approves; in the same transaction the
//                          underlying grant.Role moves Viewer → Editor)
//   Pending  → Denied     (owner denies; grant role is unchanged)
//   Pending  → Cancelled  (requester withdraws; grant role is unchanged)
//
// Approved/Denied/Cancelled are terminal. A denied or cancelled requester
// may create a NEW request later — by design the row stays as historical
// audit, but only ONE Pending row may exist per grant at a time (enforced
// at the DB layer with a partial unique index in the migration).
//
// Why a separate entity rather than fields on PatientGrant:
//   - Multiple requests over time on the same grant (denied then re-asked)
//     need a history; a single PatientGrant row can't hold that.
//   - The patient owner needs an inbox-style view of pending requests; a
//     dedicated table makes the query a `WHERE status = 'Pending'`.
public sealed class GrantUpgradeRequest
{
    public Guid Id { get; private set; }
    public Guid GrantId { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public Guid PatientId { get; private set; }
    public GrantUpgradeRequestStatus Status { get; private set; }
    public string? Message { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }

    public bool IsPending => Status == GrantUpgradeRequestStatus.Pending;

    private GrantUpgradeRequest() { }

    // The grant must be a CURRENT Viewer grant owned by the requester.
    // The application layer is responsible for loading the grant, confirming
    // those preconditions, and passing the canonical patient/requester ids
    // here so the domain check can assert the linkage.
    public static GrantUpgradeRequest Create(
        PatientGrant grant,
        Guid requesterUserId,
        string? message)
    {
        if (grant is null) throw new DomainException("Grant is required.");
        if (requesterUserId == Guid.Empty)
            throw new DomainException("Requester is required.");
        if (grant.GranteeUserId != requesterUserId)
            throw new DomainException("Only the grant's grantee may request an upgrade.");
        if (!grant.IsActive)
            throw new DomainException("Cannot request upgrade on a revoked grant.");
        if (grant.Role != GrantRole.Viewer)
            throw new DomainException("Only Viewer grants can be upgraded.");

        var trimmed = NormalizeMessage(message);

        return new GrantUpgradeRequest
        {
            Id = Guid.CreateVersion7(),
            GrantId = grant.Id,
            RequesterUserId = requesterUserId,
            PatientId = grant.PatientId,
            Status = GrantUpgradeRequestStatus.Pending,
            Message = trimmed,
            RequestedAt = DateTimeOffset.UtcNow,
            ResolvedAt = null,
            ResolvedByUserId = null,
        };
    }

    // Owner approves. Caller must also upgrade the underlying grant in the
    // same DB transaction via PatientGrant.UpgradeToEditor — this entity
    // tracks intent, not the grant role itself.
    public void Approve(Guid resolvedByUserId)
    {
        Resolve(GrantUpgradeRequestStatus.Approved, resolvedByUserId);
    }

    public void Deny(Guid resolvedByUserId)
    {
        Resolve(GrantUpgradeRequestStatus.Denied, resolvedByUserId);
    }

    // Cancellation must come from the original requester. Application layer
    // gates this; the equality check here is the floor.
    public void Cancel(Guid resolvedByUserId)
    {
        if (resolvedByUserId != RequesterUserId)
            throw new DomainException("Only the requester may cancel.");
        Resolve(GrantUpgradeRequestStatus.Cancelled, resolvedByUserId);
    }

    private void Resolve(GrantUpgradeRequestStatus next, Guid resolvedByUserId)
    {
        if (resolvedByUserId == Guid.Empty)
            throw new DomainException("Resolver is required.");
        if (Status != GrantUpgradeRequestStatus.Pending)
            throw new DomainException("Request has already been resolved.");
        Status = next;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolvedByUserId = resolvedByUserId;
    }

    private static string? NormalizeMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > 500)
            throw new DomainException("Message is too long (max 500 characters).");
        return trimmed;
    }
}
