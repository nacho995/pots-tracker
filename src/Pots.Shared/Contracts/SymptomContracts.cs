namespace Pots.Shared.Contracts;

public sealed record RecordSymptomsDto
{
    public DateTimeOffset? RecordedAt { get; init; }
    public int? Dizziness { get; init; }
    public int? Palpitations { get; init; }
    public int? TachycardiaSensation { get; init; }
    public int? ChestDiscomfort { get; init; }
    public int? ShortnessOfBreath { get; init; }
    public int? NearFainting { get; init; }
    public bool FaintingEpisode { get; init; }
    public int? BloodPooling { get; init; }
    public int? BrainFog { get; init; }
    public int? Headache { get; init; }
    public int? VisualDisturbance { get; init; }
    public int? Tremor { get; init; }
    public int? Weakness { get; init; }
    public int? Fatigue { get; init; }
    public int? Sleepiness { get; init; }
    public int? Nausea { get; init; }
    public int? AbdominalPain { get; init; }
    public int? Bloating { get; init; }
    public string? Bowel { get; init; }
    public int? AppetiteLevel { get; init; }
    public int? HeatIntolerance { get; init; }
    public int? Sweating { get; init; }
    public int? Chills { get; init; }
    public int? Flushing { get; init; }
    public int? ColdExtremities { get; init; }
    public int? Anxiety { get; init; }
    public int? Mood { get; init; }
    public int? AbilityToWork { get; init; }
    public int? AbilityToWalk { get; init; }
    public int? SocialTolerance { get; init; }
}

public sealed record SymptomLogDto(Guid Id, DateTimeOffset RecordedAt);

// Phase 7.2.b — full DTO surfaced to caregivers when they read a shared
// patient's symptom history. Includes recorder name for attribution.
public sealed record SymptomLogFullDto(
    Guid Id, DateTimeOffset RecordedAt, string? RecorderName,
    int? Dizziness, int? Palpitations, int? TachycardiaSensation,
    int? ChestDiscomfort, int? ShortnessOfBreath, int? NearFainting,
    bool FaintingEpisode, int? BloodPooling,
    int? BrainFog, int? Headache, int? VisualDisturbance,
    int? Tremor, int? Weakness, int? Fatigue, int? Sleepiness,
    int? Nausea, int? AbdominalPain, int? Bloating,
    string? Bowel, int? AppetiteLevel,
    int? HeatIntolerance, int? Sweating, int? Chills, int? Flushing, int? ColdExtremities,
    int? Anxiety, int? Mood,
    int? AbilityToWork, int? AbilityToWalk, int? SocialTolerance);
