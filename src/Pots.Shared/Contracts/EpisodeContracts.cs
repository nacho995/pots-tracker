namespace Pots.Shared.Contracts;

public sealed record CreateEpisodeDto(
    DateTimeOffset? StartTime = null,
    int? DurationMinutes = null,
    string? MainSymptom = null,
    string? PostureBefore = null,
    string TriggerSuspected = "Unknown",
    int? HrDuringBpm = null,
    int? BpDuringSystolic = null,
    int? BpDuringDiastolic = null,
    string? ActionTaken = null,
    int? RecoveryTimeMinutes = null,
    bool? PreventedFainting = null,
    string? Note = null
);

public sealed record EpisodeDto(
    Guid Id,
    DateTimeOffset StartTime,
    int? DurationMinutes,
    string? MainSymptom,
    string? PostureBefore,
    string TriggerSuspected,
    int? HrDuringBpm,
    int? BpDuringSystolic,
    int? BpDuringDiastolic,
    string? ActionTaken,
    int? RecoveryTimeMinutes,
    bool? PreventedFainting,
    string? Note,
    DateTimeOffset CreatedAt
);
