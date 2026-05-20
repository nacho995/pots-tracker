namespace Pots.Domain.Entities;

// One row per Green/Orange/Red press. Designed for sub-60-second entry on
// brain-fog days: required fields are kept to status + patient + timestamp;
// everything else (posture, activity, location, free-text note) is optional
// and only surfaced after Orange/Red or "add detail".
public sealed class DailyStatusEntry
{
    public Guid Id { get; private set; }
    public Guid PatientId { get; private set; }
    // Denormalised "who pressed the button". Identical to the patient's
    // owner_user_id for the vast majority of rows; differs only when an
    // Editor grantee logged on the patient's behalf (Amelia mid-crisis,
    // María logs Red for her — Phase 5). Kept on the row so the Doctor
    // Report can render "Registrado por María, no por Amelia" without
    // joining audit_log every time.
    public Guid RecordedByUserId { get; private set; }
    public DailyStatusKind Status { get; private set; }
    public PostureKind? Posture { get; private set; }
    public string? Activity { get; private set; }
    public string? LocationNote { get; private set; }
    public string? Note { get; private set; }
    public bool EpisodeOccurred { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private DailyStatusEntry() { }

    public static DailyStatusEntry Create(
        Guid patientId,
        Guid recordedByUserId,
        DailyStatusKind status,
        PostureKind? posture = null,
        string? activity = null,
        string? locationNote = null,
        string? note = null,
        bool episodeOccurred = false)
    {
        if (patientId == Guid.Empty)
            throw new DomainException("Patient is required.");
        if (recordedByUserId == Guid.Empty)
            throw new DomainException("Recorder is required.");
        if (!Enum.IsDefined(status))
            throw new DomainException("Status is not valid.");
        if (posture is not null && !Enum.IsDefined(posture.Value))
            throw new DomainException("Posture is not valid.");

        return new DailyStatusEntry
        {
            Id = Guid.CreateVersion7(),
            PatientId = patientId,
            RecordedByUserId = recordedByUserId,
            Status = status,
            Posture = posture,
            Activity = TrimToMax(activity, 100),
            LocationNote = TrimToMax(locationNote, 200),
            Note = TrimToMax(note, 1000),
            EpisodeOccurred = episodeOccurred,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    // Updates the optional detail fields after the initial press. Supports the
    // "Añadir detalle" flow: the user presses Green/Orange/Red in <60s and may
    // come back to add posture/activity/location/note when they have a moment.
    // Status itself is immutable post-creation (the audit trail of how you
    // FELT in the moment must not be retconned).
    public void UpdateDetail(
        PostureKind? posture,
        string? activity,
        string? locationNote,
        string? note,
        bool episodeOccurred)
    {
        if (posture is not null && !Enum.IsDefined(posture.Value))
            throw new DomainException("Posture is not valid.");

        Posture = posture;
        Activity = TrimToMax(activity, 100);
        LocationNote = TrimToMax(locationNote, 200);
        Note = TrimToMax(note, 1000);
        EpisodeOccurred = episodeOccurred;
    }

    private static string? TrimToMax(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"Field exceeds {maxLength} characters.");
        return trimmed;
    }
}
