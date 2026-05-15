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
