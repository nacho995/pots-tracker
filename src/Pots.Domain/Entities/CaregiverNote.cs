namespace Pots.Domain.Entities;

// A short text note attached to a patient, authored by anyone with access
// to that patient (the owner herself or any active grantee). The point is
// to let caregivers leave observations like "Te he visto pálida esta mañana"
// or "Recuerda hidratarte si vas a salir" without polluting the clinical
// data (symptoms, episodes, vitals).
//
// Visibility (enforced at RLS): everyone with has_patient_access can read;
// only the author can soft-delete their own note, and the patient owner can
// soft-delete any note (her data, her call). Hard delete is never exposed.
//
// Editability: NOT supported in v1. A note is a moment-in-time observation;
// editing one after the fact would muddy the audit trail and the reading
// experience. If the author wants to amend, they post a new note.
//
// Author identity is always shown in the UI — that's the whole point of the
// feature ("from {{caregiver name}}"). The application layer resolves the
// author's display name via a SECURITY DEFINER join, mirroring the pattern
// used for grant listings.
public sealed class CaregiverNote
{
    public Guid Id { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedByUserId { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    private CaregiverNote() { }

    public static CaregiverNote Create(Guid patientId, Guid authorUserId, string body)
    {
        if (patientId == Guid.Empty)
            throw new DomainException("Patient is required.");
        if (authorUserId == Guid.Empty)
            throw new DomainException("Author is required.");

        var trimmed = NormalizeBody(body);

        return new CaregiverNote
        {
            Id = Guid.CreateVersion7(),
            PatientId = patientId,
            AuthorUserId = authorUserId,
            Body = trimmed,
            CreatedAt = DateTimeOffset.UtcNow,
            DeletedAt = null,
            DeletedByUserId = null,
        };
    }

    // Soft-delete. The application layer is responsible for gating WHO can
    // call this (author OR patient owner). The domain only enforces that
    // the action is idempotent and stamps the actor.
    public void SoftDelete(Guid deletedByUserId)
    {
        if (deletedByUserId == Guid.Empty)
            throw new DomainException("Deleter is required.");
        if (DeletedAt is not null) return;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedByUserId = deletedByUserId;
    }

    private static string NormalizeBody(string value)
    {
        if (value is null)
            throw new DomainException("Body cannot be null.");
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Body cannot be empty.");
        if (trimmed.Length > 2000)
            throw new DomainException("Body is too long (max 2000 characters).");
        return trimmed;
    }
}
