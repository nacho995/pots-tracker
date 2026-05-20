namespace Pots.Domain.Entities;

// One row per patient per local day. The endpoint upserts on (patient_id, day).
// salt_target_reached is meaningful ONLY when PatientTargets.SaltTargetEnabled
// is true — the API layer must guard input. See CLAUDE.md §2 (salt clinician-gated).
public sealed class PreventiveActionLog
{
    public Guid Id { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateOnly Day { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Hydration
    public int? FluidMl { get; private set; }
    public bool ElectrolyteTaken { get; private set; }
    public bool MorningWaterBeforeStanding { get; private set; }
    public UrineColor? UrineColor { get; private set; }

    // Salt (gated)
    public bool? SaltTargetReached { get; private set; }

    // Nutrition
    public bool RegularMeals { get; private set; }
    public bool SkippedBreakfast { get; private set; }
    public bool SmallFrequentMeals { get; private set; }
    public bool AvoidedLargeHighCarbMeal { get; private set; }
    public bool AdequateProtein { get; private set; }
    public bool AlcoholAvoided { get; private set; }
    public CaffeineLevel CaffeineLevel { get; private set; } = CaffeineLevel.None;

    // Compression
    public bool CompressionSocks { get; private set; }
    public bool WaistHighCompression { get; private set; }
    public bool AbdominalCompression { get; private set; }
    public int? CompressionHoursWorn { get; private set; }

    // Exercise / reconditioning
    public bool RecumbentExercise { get; private set; }
    public bool Walking { get; private set; }
    public bool Strength { get; private set; }
    public bool Stretching { get; private set; }
    public bool PtExercises { get; private set; }
    public int? ExerciseDurationMinutes { get; private set; }
    public string? ExerciseIntensity { get; private set; }
    public string? PostExerciseSymptoms { get; private set; }

    // Pacing
    public bool PlannedRestBreaks { get; private set; }
    public bool AvoidedOverexertion { get; private set; }
    public bool UsedActivityPacing { get; private set; }
    public bool AvoidedLongStanding { get; private set; }
    public bool SatDuringShowerCooking { get; private set; }
    public string? MobilityAid { get; private set; }

    // Heat
    public bool AvoidedHeat { get; private set; }
    public bool UsedCoolingVestFan { get; private set; }
    public bool ColdShower { get; private set; }
    public bool AvoidedHotBathSauna { get; private set; }
    public bool StayedInShadeAc { get; private set; }

    // Sleep
    public bool SleptEnough { get; private set; }
    public int? SleepQuality { get; private set; }
    public bool ConsistentBedtimes { get; private set; }
    public bool NapTaken { get; private set; }
    public bool? WokeRefreshed { get; private set; }

    // Medication / treatment adherence
    public bool MedicationTakenAsPrescribed { get; private set; }
    public bool MissedDose { get; private set; }
    public string? SideEffects { get; private set; }
    public string? NewMedicationOrSupplement { get; private set; }
    public bool? RescueMedicationUsed { get; private set; }

    private PreventiveActionLog() { }

    public static PreventiveActionLog Create(Guid patientId, Guid recordedByUserId, DateOnly day, bool saltTargetAllowed, PreventiveActionData d)
    {
        if (patientId == Guid.Empty)
            throw new DomainException("Patient is required.");
        if (recordedByUserId == Guid.Empty)
            throw new DomainException("Recorder is required.");

        if (d.SaltTargetReached is not null && !saltTargetAllowed)
            throw new DomainException("Salt target field is only available when patient has a clinician-prescribed salt target enabled in settings.");

        Validate(d);

        var now = DateTimeOffset.UtcNow;
        var entry = new PreventiveActionLog
        {
            Id = Guid.CreateVersion7(),
            PatientId = patientId,
            RecordedByUserId = recordedByUserId,
            Day = day,
            CreatedAt = now,
            UpdatedAt = now,
        };
        Apply(entry, d);
        return entry;
    }

    public void Update(bool saltTargetAllowed, PreventiveActionData d)
    {
        if (d.SaltTargetReached is not null && !saltTargetAllowed)
            throw new DomainException("Salt target field is only available when patient has a clinician-prescribed salt target enabled in settings.");
        Validate(d);
        Apply(this, d);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Validate(PreventiveActionData d)
    {
        if (d.FluidMl is { } f && (f < 0 || f > 20_000))
            throw new DomainException("Fluid amount is out of plausible range.");
        if (d.CompressionHoursWorn is { } ch && (ch < 0 || ch > 24))
            throw new DomainException("Compression hours out of range.");
        if (d.ExerciseDurationMinutes is { } ed && (ed < 0 || ed > 24 * 60))
            throw new DomainException("Exercise duration out of range.");
        if (d.SleepQuality is { } sq && (sq < 0 || sq > 10))
            throw new DomainException("Sleep quality must be 0-10.");
        if (d.UrineColor is { } uc && !Enum.IsDefined(uc))
            throw new DomainException("Urine color is not valid.");
        if (!Enum.IsDefined(d.CaffeineLevel))
            throw new DomainException("Caffeine level is not valid.");
    }

    private static void Apply(PreventiveActionLog e, PreventiveActionData d)
    {
        e.FluidMl = d.FluidMl;
        e.ElectrolyteTaken = d.ElectrolyteTaken;
        e.MorningWaterBeforeStanding = d.MorningWaterBeforeStanding;
        e.UrineColor = d.UrineColor;
        e.SaltTargetReached = d.SaltTargetReached;
        e.RegularMeals = d.RegularMeals;
        e.SkippedBreakfast = d.SkippedBreakfast;
        e.SmallFrequentMeals = d.SmallFrequentMeals;
        e.AvoidedLargeHighCarbMeal = d.AvoidedLargeHighCarbMeal;
        e.AdequateProtein = d.AdequateProtein;
        e.AlcoholAvoided = d.AlcoholAvoided;
        e.CaffeineLevel = d.CaffeineLevel;
        e.CompressionSocks = d.CompressionSocks;
        e.WaistHighCompression = d.WaistHighCompression;
        e.AbdominalCompression = d.AbdominalCompression;
        e.CompressionHoursWorn = d.CompressionHoursWorn;
        e.RecumbentExercise = d.RecumbentExercise;
        e.Walking = d.Walking;
        e.Strength = d.Strength;
        e.Stretching = d.Stretching;
        e.PtExercises = d.PtExercises;
        e.ExerciseDurationMinutes = d.ExerciseDurationMinutes;
        e.ExerciseIntensity = TrimNullable(d.ExerciseIntensity, 50);
        e.PostExerciseSymptoms = TrimNullable(d.PostExerciseSymptoms, 500);
        e.PlannedRestBreaks = d.PlannedRestBreaks;
        e.AvoidedOverexertion = d.AvoidedOverexertion;
        e.UsedActivityPacing = d.UsedActivityPacing;
        e.AvoidedLongStanding = d.AvoidedLongStanding;
        e.SatDuringShowerCooking = d.SatDuringShowerCooking;
        e.MobilityAid = TrimNullable(d.MobilityAid, 100);
        e.AvoidedHeat = d.AvoidedHeat;
        e.UsedCoolingVestFan = d.UsedCoolingVestFan;
        e.ColdShower = d.ColdShower;
        e.AvoidedHotBathSauna = d.AvoidedHotBathSauna;
        e.StayedInShadeAc = d.StayedInShadeAc;
        e.SleptEnough = d.SleptEnough;
        e.SleepQuality = d.SleepQuality;
        e.ConsistentBedtimes = d.ConsistentBedtimes;
        e.NapTaken = d.NapTaken;
        e.WokeRefreshed = d.WokeRefreshed;
        e.MedicationTakenAsPrescribed = d.MedicationTakenAsPrescribed;
        e.MissedDose = d.MissedDose;
        e.SideEffects = TrimNullable(d.SideEffects, 500);
        e.NewMedicationOrSupplement = TrimNullable(d.NewMedicationOrSupplement, 200);
        e.RescueMedicationUsed = d.RescueMedicationUsed;
    }

    private static string? TrimNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"Field exceeds {maxLength} characters.");
        return trimmed;
    }
}

public sealed record PreventiveActionData
{
    public int? FluidMl { get; init; }
    public bool ElectrolyteTaken { get; init; }
    public bool MorningWaterBeforeStanding { get; init; }
    public UrineColor? UrineColor { get; init; }
    public bool? SaltTargetReached { get; init; }
    public bool RegularMeals { get; init; }
    public bool SkippedBreakfast { get; init; }
    public bool SmallFrequentMeals { get; init; }
    public bool AvoidedLargeHighCarbMeal { get; init; }
    public bool AdequateProtein { get; init; }
    public bool AlcoholAvoided { get; init; }
    public CaffeineLevel CaffeineLevel { get; init; } = CaffeineLevel.None;
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
