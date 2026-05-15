namespace Pots.Domain.Entities;

// Per-patient personal goals and gates. ONE row per patient (unique index on
// patient_id).
//
// SaltTargetEnabled is the clinician-gate from CLAUDE.md §2: salt-loading
// targets only appear if the patient has explicitly attested that a clinician
// prescribed them. Default: FALSE. Enabling MUST be accompanied by a non-empty
// attestation note. Disabling is always allowed.
public sealed class PatientTargets
{
    public Guid Id { get; private set; }
    public Guid PatientId { get; private set; }

    public int HydrationTargetMl { get; private set; } = 2500;

    public bool SaltTargetEnabled { get; private set; }
    public int? SaltTargetMg { get; private set; }
    public string? SaltClinicianAttestation { get; private set; }

    public int? CompressionGoalHoursPerDay { get; private set; }
    public string? ExercisePlanNote { get; private set; }
    public decimal? SleepTargetHours { get; private set; }
    public string Language { get; private set; } = "es";

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PatientTargets() { }

    public static PatientTargets CreateDefaults(Guid patientId)
    {
        if (patientId == Guid.Empty)
            throw new DomainException("Patient is required.");

        var now = DateTimeOffset.UtcNow;
        return new PatientTargets
        {
            Id = Guid.CreateVersion7(),
            PatientId = patientId,
            HydrationTargetMl = 2500,
            SaltTargetEnabled = false,
            SaltTargetMg = null,
            SaltClinicianAttestation = null,
            CompressionGoalHoursPerDay = null,
            ExercisePlanNote = null,
            SleepTargetHours = null,
            Language = "es",
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(
        int hydrationTargetMl,
        int? compressionGoalHoursPerDay,
        string? exercisePlanNote,
        decimal? sleepTargetHours,
        string language)
    {
        if (hydrationTargetMl < 0 || hydrationTargetMl > 20_000)
            throw new DomainException("Hydration target out of plausible range.");
        if (compressionGoalHoursPerDay is { } c && (c < 0 || c > 24))
            throw new DomainException("Compression hours out of range.");
        if (sleepTargetHours is { } s && (s < 0 || s > 24))
            throw new DomainException("Sleep target hours out of range.");
        if (string.IsNullOrWhiteSpace(language)) language = "es";
        if (language.Length > 8)
            throw new DomainException("Language code is too long.");

        HydrationTargetMl = hydrationTargetMl;
        CompressionGoalHoursPerDay = compressionGoalHoursPerDay;
        ExercisePlanNote = TrimNullable(exercisePlanNote, 2000);
        SleepTargetHours = sleepTargetHours;
        Language = language;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // SAFETY-CRITICAL (CLAUDE.md §2): enabling the salt feature requires an
    // explicit attestation string from the patient. The application layer must
    // surface a confirmation modal before calling this method. The domain
    // refuses to enable without an attestation.
    public void EnableSaltTarget(int saltTargetMg, string attestation)
    {
        if (saltTargetMg < 0 || saltTargetMg > 50_000)
            throw new DomainException("Salt target out of plausible range.");
        if (string.IsNullOrWhiteSpace(attestation))
            throw new DomainException(
                "Enabling the salt target requires an explicit clinician-prescribed attestation. " +
                "Salt is contraindicated in hypertension, kidney disease, pregnancy, and some POTS subtypes.");
        var trimmed = attestation.Trim();
        if (trimmed.Length > 2000)
            throw new DomainException("Attestation note is too long.");

        SaltTargetEnabled = true;
        SaltTargetMg = saltTargetMg;
        SaltClinicianAttestation = trimmed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DisableSaltTarget()
    {
        SaltTargetEnabled = false;
        SaltTargetMg = null;
        SaltClinicianAttestation = null;
        UpdatedAt = DateTimeOffset.UtcNow;
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
