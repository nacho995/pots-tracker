namespace Pots.Domain.Entities;

public sealed class VitalSignLog
{
    public Guid Id { get; private set; }
    public Guid PatientId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public int? RestingHrBpm { get; private set; }
    public int? StandingHrBpm2Min { get; private set; }
    public int? StandingHrBpm5Min { get; private set; }
    public int? StandingHrBpm10Min { get; private set; }

    public int? BpLyingSystolic { get; private set; }
    public int? BpLyingDiastolic { get; private set; }
    public int? BpSittingSystolic { get; private set; }
    public int? BpSittingDiastolic { get; private set; }
    public int? BpStandingSystolic { get; private set; }
    public int? BpStandingDiastolic { get; private set; }

    public int? Spo2Percent { get; private set; }
    public decimal? WeightKg { get; private set; }
    public int? MenstrualCycleDay { get; private set; }

    public int? SleepDurationMinutes { get; private set; }
    public int? SleepQuality { get; private set; }
    public int? Steps { get; private set; }
    public int? ExerciseMinutes { get; private set; }
    public int? TimeUprightMinutes { get; private set; }
    public int? TimeLyingMinutes { get; private set; }
    public decimal? AmbientTempC { get; private set; }

    private VitalSignLog() { }

    public static VitalSignLog Create(Guid patientId, VitalSignData d)
    {
        if (patientId == Guid.Empty)
            throw new DomainException("Patient is required.");

        BpmCheck(d.RestingHrBpm);
        BpmCheck(d.StandingHrBpm2Min);
        BpmCheck(d.StandingHrBpm5Min);
        BpmCheck(d.StandingHrBpm10Min);
        BpCheck(d.BpLyingSystolic, d.BpLyingDiastolic);
        BpCheck(d.BpSittingSystolic, d.BpSittingDiastolic);
        BpCheck(d.BpStandingSystolic, d.BpStandingDiastolic);
        Spo2Check(d.Spo2Percent);
        if (d.WeightKg is { } w && (w <= 0 || w > 500))
            throw new DomainException("Weight is out of plausible range.");
        if (d.MenstrualCycleDay is { } mc && (mc < 1 || mc > 60))
            throw new DomainException("Menstrual cycle day is out of range.");
        if (d.SleepDurationMinutes is { } sd && (sd < 0 || sd > 24 * 60))
            throw new DomainException("Sleep duration is out of range.");
        ScaleCheck(d.SleepQuality);
        if (d.Steps is { } st && st < 0) throw new DomainException("Steps cannot be negative.");
        if (d.ExerciseMinutes is { } em && (em < 0 || em > 24 * 60))
            throw new DomainException("Exercise minutes out of range.");
        if (d.TimeUprightMinutes is { } tu && (tu < 0 || tu > 24 * 60))
            throw new DomainException("Time upright out of range.");
        if (d.TimeLyingMinutes is { } tl && (tl < 0 || tl > 24 * 60))
            throw new DomainException("Time lying out of range.");
        if (d.AmbientTempC is { } at && (at < -50 || at > 60))
            throw new DomainException("Ambient temperature out of plausible range.");

        var now = DateTimeOffset.UtcNow;
        return new VitalSignLog
        {
            Id = Guid.CreateVersion7(),
            PatientId = patientId,
            RecordedAt = d.RecordedAt ?? now,
            CreatedAt = now,
            RestingHrBpm = d.RestingHrBpm,
            StandingHrBpm2Min = d.StandingHrBpm2Min,
            StandingHrBpm5Min = d.StandingHrBpm5Min,
            StandingHrBpm10Min = d.StandingHrBpm10Min,
            BpLyingSystolic = d.BpLyingSystolic,
            BpLyingDiastolic = d.BpLyingDiastolic,
            BpSittingSystolic = d.BpSittingSystolic,
            BpSittingDiastolic = d.BpSittingDiastolic,
            BpStandingSystolic = d.BpStandingSystolic,
            BpStandingDiastolic = d.BpStandingDiastolic,
            Spo2Percent = d.Spo2Percent,
            WeightKg = d.WeightKg,
            MenstrualCycleDay = d.MenstrualCycleDay,
            SleepDurationMinutes = d.SleepDurationMinutes,
            SleepQuality = d.SleepQuality,
            Steps = d.Steps,
            ExerciseMinutes = d.ExerciseMinutes,
            TimeUprightMinutes = d.TimeUprightMinutes,
            TimeLyingMinutes = d.TimeLyingMinutes,
            AmbientTempC = d.AmbientTempC,
        };
    }

    private static void BpmCheck(int? v)
    {
        if (v is null) return;
        if (v < 20 || v > 250) throw new DomainException("Heart rate out of plausible range.");
    }

    private static void BpCheck(int? sys, int? dia)
    {
        if (sys is null && dia is null) return;
        if (sys is { } s && (s < 50 || s > 250)) throw new DomainException("Systolic BP out of plausible range.");
        if (dia is { } d && (d < 30 || d > 180)) throw new DomainException("Diastolic BP out of plausible range.");
    }

    private static void Spo2Check(int? v)
    {
        if (v is null) return;
        if (v < 50 || v > 100) throw new DomainException("SpO₂ out of range.");
    }

    private static void ScaleCheck(int? v)
    {
        if (v is null) return;
        if (v < 0 || v > 10) throw new DomainException("Scale value must be between 0 and 10.");
    }
}

public sealed record VitalSignData
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
