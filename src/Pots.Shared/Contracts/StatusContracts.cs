namespace Pots.Shared.Contracts;

public sealed record RecordStatusDto(
    string Status,
    string? Posture = null,
    string? Activity = null,
    string? LocationNote = null,
    string? Note = null,
    bool EpisodeOccurred = false);

public sealed record DailyStatusDto(
    Guid Id,
    string Status,
    string? Posture,
    string? Activity,
    string? LocationNote,
    string? Note,
    bool EpisodeOccurred,
    DateTimeOffset CreatedAt,
    // Phase 6: surface "who pressed the button" when ≠ patient owner.
    // Null when the patient self-recorded; non-null = display name (with
    // email fallback) of the Editor grantee who logged on her behalf.
    // UI renders "Registrado por X".
    string? RecorderName = null);

public sealed record UpdateStatusDetailDto(
    string? Posture = null,
    string? Activity = null,
    string? LocationNote = null,
    string? Note = null,
    bool EpisodeOccurred = false);
