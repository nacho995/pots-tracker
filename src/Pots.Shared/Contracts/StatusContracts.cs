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
    DateTimeOffset CreatedAt);

public sealed record UpdateStatusDetailDto(
    string? Posture = null,
    string? Activity = null,
    string? LocationNote = null,
    string? Note = null,
    bool EpisodeOccurred = false);
