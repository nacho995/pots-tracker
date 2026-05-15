namespace Pots.Shared.Contracts;

public sealed record PatientTargetsDto(
    int HydrationTargetMl,
    bool SaltTargetEnabled,
    int? SaltTargetMg,
    string? SaltClinicianAttestation,
    int? CompressionGoalHoursPerDay,
    string? ExercisePlanNote,
    decimal? SleepTargetHours,
    string Language
);

public sealed record UpdateTargetsDto(
    int HydrationTargetMl,
    int? CompressionGoalHoursPerDay,
    string? ExercisePlanNote,
    decimal? SleepTargetHours,
    string Language
);

public sealed record EnableSaltTargetDto(
    int SaltTargetMg,
    string Attestation
);
