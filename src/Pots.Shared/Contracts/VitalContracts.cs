namespace Pots.Shared.Contracts;

public sealed record RecordVitalsDto
{
    public DateTimeOffset? RecordedAt { get; init; }
    public int? RestingHrBpm { get; init; }
    public int? StandingHrBpm2Min { get; init; }
    public int? StandingHrBpm5Min { get; init; }
    public int? StandingHrBpm10Min { get; init; }
    public int? BpLyingSystolic { get; init; }
    public int? BpLyingDiastolic { get; init; }
    public int? BpSittingSystolic { get; init; }
    public int? BpSittingDiastolic { get; init; }
    public int? BpStandingSystolic { get; init; }
    public int? BpStandingDiastolic { get; init; }
    public int? Spo2Percent { get; init; }
    public decimal? WeightKg { get; init; }
    public int? MenstrualCycleDay { get; init; }
    public int? SleepDurationMinutes { get; init; }
    public int? SleepQuality { get; init; }
    public int? Steps { get; init; }
    public int? ExerciseMinutes { get; init; }
    public int? TimeUprightMinutes { get; init; }
    public int? TimeLyingMinutes { get; init; }
    public decimal? AmbientTempC { get; init; }
}

public sealed record VitalLogDto(Guid Id, DateTimeOffset RecordedAt);

// Phase 7.2.b — full vitals DTO for caregiver display.
public sealed record VitalLogFullDto(
    Guid Id, DateTimeOffset RecordedAt, string? RecorderName,
    int? RestingHrBpm, int? StandingHrBpm2Min, int? StandingHrBpm5Min, int? StandingHrBpm10Min,
    int? BpLyingSystolic, int? BpLyingDiastolic,
    int? BpSittingSystolic, int? BpSittingDiastolic,
    int? BpStandingSystolic, int? BpStandingDiastolic,
    int? Spo2Percent, decimal? WeightKg, int? MenstrualCycleDay,
    int? SleepDurationMinutes, int? SleepQuality,
    int? Steps, int? ExerciseMinutes,
    int? TimeUprightMinutes, int? TimeLyingMinutes,
    decimal? AmbientTempC);
