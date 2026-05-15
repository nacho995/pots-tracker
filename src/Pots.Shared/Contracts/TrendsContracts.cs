namespace Pots.Shared.Contracts;

public sealed record TrendsDto(
    int RangeDays,
    int GreenCount,
    int OrangeCount,
    int RedCount,
    int EpisodeCount,
    List<EpisodesPerWeekRow> EpisodesPerWeek,
    List<TriggerCountRow> TopTriggers,
    double? AvgFatigue,
    double? AvgBrainFog,
    double? AvgDizziness,
    List<DailyStatusBucket> DailyStatusBuckets,
    // ↓ correlations ("asociado con", nunca "causa")
    BestWorstDays? BestWorst,
    List<AssociationRow> SleepQualityVsSymptoms,
    List<AssociationRow> HydrationVsSymptoms,
    AssociationRow? CompressionVsSymptoms,
    AssociationRow? NoCompressionVsSymptoms,
    AssociationRow? SkipBreakfastVsSymptoms,
    AssociationRow? RegularBreakfastVsSymptoms,
    List<AssociationRow> AmbientTempVsSymptoms,
    List<AssociationRow> MenstrualPhaseVsSymptoms,
    ExerciseToleranceRow? ExerciseTolerance,
    List<ActionVsRedRow> ActionsAssociatedWithFewerReds
);

public sealed record EpisodesPerWeekRow(string WeekStart, int Count);
public sealed record TriggerCountRow(string Trigger, int Count);
public sealed record DailyStatusBucket(string Day, int Green, int Orange, int Red);

public sealed record BestWorstDays(
    string? BestDay,
    int BestGreen,
    int BestRed,
    string? WorstDay,
    int WorstRed,
    int WorstGreen
);

public sealed record AssociationRow(
    string Bucket,
    int DayCount,
    double? AvgSymptomBurden
);

public sealed record ExerciseToleranceRow(
    int TotalSessions,
    int SessionsWithPostSymptoms,
    double? SymptomBurdenWhenExercised,
    double? SymptomBurdenWhenNotExercised
);

public sealed record ActionVsRedRow(
    string Action,
    int DaysWhen,
    int DaysWhenNot,
    double? RedRateWhenPct,
    double? RedRateWhenNotPct,
    double? DeltaPct
);
