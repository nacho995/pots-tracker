namespace Pots.Shared.Contracts;

public sealed record UpsertActionsDto
{
    public DateOnly Day { get; init; }

    public int? FluidMl { get; init; }
    public bool ElectrolyteTaken { get; init; }
    public bool MorningWaterBeforeStanding { get; init; }
    public string? UrineColor { get; init; }

    public bool? SaltTargetReached { get; init; }

    public bool RegularMeals { get; init; }
    public bool SkippedBreakfast { get; init; }
    public bool SmallFrequentMeals { get; init; }
    public bool AvoidedLargeHighCarbMeal { get; init; }
    public bool AdequateProtein { get; init; }
    public bool AlcoholAvoided { get; init; }
    public string CaffeineLevel { get; init; } = "None";

    public bool CompressionSocks { get; init; }
    public bool WaistHighCompression { get; init; }
    public bool AbdominalCompression { get; init; }
    public int? CompressionHoursWorn { get; init; }

    public bool RecumbentExercise { get; init; }
    public bool Walking { get; init; }
    public bool Strength { get; init; }
    public bool Stretching { get; init; }
    public bool PtExercises { get; init; }
    public int? ExerciseDurationMinutes { get; init; }
    public string? ExerciseIntensity { get; init; }
    public string? PostExerciseSymptoms { get; init; }

    public bool PlannedRestBreaks { get; init; }
    public bool AvoidedOverexertion { get; init; }
    public bool UsedActivityPacing { get; init; }
    public bool AvoidedLongStanding { get; init; }
    public bool SatDuringShowerCooking { get; init; }
    public string? MobilityAid { get; init; }

    public bool AvoidedHeat { get; init; }
    public bool UsedCoolingVestFan { get; init; }
    public bool ColdShower { get; init; }
    public bool AvoidedHotBathSauna { get; init; }
    public bool StayedInShadeAc { get; init; }

    public bool SleptEnough { get; init; }
    public int? SleepQuality { get; init; }
    public bool ConsistentBedtimes { get; init; }
    public bool NapTaken { get; init; }
    public bool? WokeRefreshed { get; init; }

    public bool MedicationTakenAsPrescribed { get; init; }
    public bool MissedDose { get; init; }
    public string? SideEffects { get; init; }
    public string? NewMedicationOrSupplement { get; init; }
    public bool? RescueMedicationUsed { get; init; }
}

public sealed record ActionLogDto(Guid Id, DateOnly Day);
