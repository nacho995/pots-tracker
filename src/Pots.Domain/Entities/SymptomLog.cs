namespace Pots.Domain.Entities;

// Wide entity: one row per logging event with every symptom from the spec as
// a nullable 0-10 scale (or boolean for fainting). Null means "not recorded
// this time" (NOT "zero"). The endpoint sends only the fields the patient
// filled in.
public sealed class SymptomLog
{
    public Guid Id { get; private set; }
    public Guid PatientId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Cardiovascular / orthostatic
    public int? Dizziness { get; private set; }
    public int? Palpitations { get; private set; }
    public int? TachycardiaSensation { get; private set; }
    public int? ChestDiscomfort { get; private set; }
    public int? ShortnessOfBreath { get; private set; }
    public int? NearFainting { get; private set; }
    public bool FaintingEpisode { get; private set; }
    public int? BloodPooling { get; private set; }

    // Neurological / cognitive
    public int? BrainFog { get; private set; }
    public int? Headache { get; private set; }
    public int? VisualDisturbance { get; private set; }
    public int? Tremor { get; private set; }
    public int? Weakness { get; private set; }
    public int? Fatigue { get; private set; }
    public int? Sleepiness { get; private set; }

    // Gastrointestinal
    public int? Nausea { get; private set; }
    public int? AbdominalPain { get; private set; }
    public int? Bloating { get; private set; }
    public GiBowelState? Bowel { get; private set; }
    public int? AppetiteLevel { get; private set; }

    // Temperature / autonomic
    public int? HeatIntolerance { get; private set; }
    public int? Sweating { get; private set; }
    public int? Chills { get; private set; }
    public int? Flushing { get; private set; }
    public int? ColdExtremities { get; private set; }

    // Emotional / functional
    public int? Anxiety { get; private set; }
    public int? Mood { get; private set; }
    public int? AbilityToWork { get; private set; }
    public int? AbilityToWalk { get; private set; }
    public int? SocialTolerance { get; private set; }

    private SymptomLog() { }

    public static SymptomLog Create(Guid patientId, SymptomData d)
    {
        if (patientId == Guid.Empty)
            throw new DomainException("Patient is required.");

        ScaleCheck(d.Dizziness);
        ScaleCheck(d.Palpitations);
        ScaleCheck(d.TachycardiaSensation);
        ScaleCheck(d.ChestDiscomfort);
        ScaleCheck(d.ShortnessOfBreath);
        ScaleCheck(d.NearFainting);
        ScaleCheck(d.BloodPooling);
        ScaleCheck(d.BrainFog);
        ScaleCheck(d.Headache);
        ScaleCheck(d.VisualDisturbance);
        ScaleCheck(d.Tremor);
        ScaleCheck(d.Weakness);
        ScaleCheck(d.Fatigue);
        ScaleCheck(d.Sleepiness);
        ScaleCheck(d.Nausea);
        ScaleCheck(d.AbdominalPain);
        ScaleCheck(d.Bloating);
        ScaleCheck(d.AppetiteLevel);
        ScaleCheck(d.HeatIntolerance);
        ScaleCheck(d.Sweating);
        ScaleCheck(d.Chills);
        ScaleCheck(d.Flushing);
        ScaleCheck(d.ColdExtremities);
        ScaleCheck(d.Anxiety);
        ScaleCheck(d.Mood);
        ScaleCheck(d.AbilityToWork);
        ScaleCheck(d.AbilityToWalk);
        ScaleCheck(d.SocialTolerance);
        if (d.Bowel is { } bowel && !Enum.IsDefined(bowel))
            throw new DomainException("Bowel state is not valid.");

        var now = DateTimeOffset.UtcNow;
        return new SymptomLog
        {
            Id = Guid.CreateVersion7(),
            PatientId = patientId,
            RecordedAt = d.RecordedAt ?? now,
            CreatedAt = now,
            Dizziness = d.Dizziness,
            Palpitations = d.Palpitations,
            TachycardiaSensation = d.TachycardiaSensation,
            ChestDiscomfort = d.ChestDiscomfort,
            ShortnessOfBreath = d.ShortnessOfBreath,
            NearFainting = d.NearFainting,
            FaintingEpisode = d.FaintingEpisode,
            BloodPooling = d.BloodPooling,
            BrainFog = d.BrainFog,
            Headache = d.Headache,
            VisualDisturbance = d.VisualDisturbance,
            Tremor = d.Tremor,
            Weakness = d.Weakness,
            Fatigue = d.Fatigue,
            Sleepiness = d.Sleepiness,
            Nausea = d.Nausea,
            AbdominalPain = d.AbdominalPain,
            Bloating = d.Bloating,
            Bowel = d.Bowel,
            AppetiteLevel = d.AppetiteLevel,
            HeatIntolerance = d.HeatIntolerance,
            Sweating = d.Sweating,
            Chills = d.Chills,
            Flushing = d.Flushing,
            ColdExtremities = d.ColdExtremities,
            Anxiety = d.Anxiety,
            Mood = d.Mood,
            AbilityToWork = d.AbilityToWork,
            AbilityToWalk = d.AbilityToWalk,
            SocialTolerance = d.SocialTolerance,
        };
    }

    private static void ScaleCheck(int? v)
    {
        if (v is null) return;
        if (v < 0 || v > 10)
            throw new DomainException("Symptom scale must be between 0 and 10.");
    }
}

public sealed record SymptomData
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
    public GiBowelState? Bowel { get; init; }
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
