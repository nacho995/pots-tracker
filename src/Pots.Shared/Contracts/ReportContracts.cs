namespace Pots.Shared.Contracts;

public sealed record DoctorReportDto(
    DateTimeOffset From,
    DateTimeOffset To,
    string PatientName,
    int GreenCount,
    int OrangeCount,
    int RedCount,
    int EpisodeCount,
    double? AvgRestingHrBpm,
    double? AvgStandingHrBpm,
    double? AvgBpLyingSys,
    double? AvgBpLyingDia,
    double? AvgBpSittingSys,
    double? AvgBpSittingDia,
    double? AvgBpStandingSys,
    double? AvgBpStandingDia,
    int? MaxHrInEpisodeBpm,
    List<SymptomAverage> TopSymptoms,
    List<TriggerCountRow> TopTriggers,
    double? AvgDailyFluidMl,
    double? AvgSaltMgPerDay,
    double? CompressionAdherencePct,
    double? ExerciseAdherencePct,
    double? MedicationAdherencePct,
    int EpisodesPreventedFainting,
    int ExerciseSessions,
    int ExerciseSessionsWithPostSymptoms,
    List<string> PatientNotes
);

public sealed record SymptomAverage(string Name, double Average);
