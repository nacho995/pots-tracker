namespace Pots.Domain.Entities;

public sealed class Episode
{
    public Guid Id { get; private set; }
    public Guid PatientId { get; private set; }
    // See DailyStatusEntry.RecordedByUserId — same denormalised attribution
    // pattern. An Editor grantee may register an episode on the owner's
    // behalf when the owner is unable to write during the event itself.
    public Guid RecordedByUserId { get; private set; }
    public DateTimeOffset StartTime { get; private set; }
    public int? DurationMinutes { get; private set; }
    public string? MainSymptom { get; private set; }
    public PostureKind? PostureBefore { get; private set; }
    public EpisodeTrigger TriggerSuspected { get; private set; } = EpisodeTrigger.Unknown;
    public int? HrDuringBpm { get; private set; }
    public int? BpDuringSystolic { get; private set; }
    public int? BpDuringDiastolic { get; private set; }
    public string? ActionTaken { get; private set; }
    public int? RecoveryTimeMinutes { get; private set; }
    public bool? PreventedFainting { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Episode() { }

    public static Episode Create(Guid patientId, Guid recordedByUserId, EpisodeData d)
    {
        if (patientId == Guid.Empty)
            throw new DomainException("Patient is required.");
        if (recordedByUserId == Guid.Empty)
            throw new DomainException("Recorder is required.");
        if (d.PostureBefore is { } pb && !Enum.IsDefined(pb))
            throw new DomainException("Posture is not valid.");
        if (!Enum.IsDefined(d.TriggerSuspected))
            throw new DomainException("Trigger value is not valid.");
        if (d.DurationMinutes is { } dur && (dur < 0 || dur > 24 * 60))
            throw new DomainException("Duration out of plausible range.");
        if (d.HrDuringBpm is { } hr && (hr < 20 || hr > 250))
            throw new DomainException("Heart rate out of plausible range.");
        if (d.BpDuringSystolic is { } s && (s < 50 || s > 250))
            throw new DomainException("Systolic BP out of plausible range.");
        if (d.BpDuringDiastolic is { } dia && (dia < 30 || dia > 180))
            throw new DomainException("Diastolic BP out of plausible range.");
        if (d.RecoveryTimeMinutes is { } rt && (rt < 0 || rt > 24 * 60))
            throw new DomainException("Recovery time out of plausible range.");

        return new Episode
        {
            Id = Guid.CreateVersion7(),
            PatientId = patientId,
            RecordedByUserId = recordedByUserId,
            StartTime = d.StartTime ?? DateTimeOffset.UtcNow,
            DurationMinutes = d.DurationMinutes,
            MainSymptom = TrimNullable(d.MainSymptom, 200),
            PostureBefore = d.PostureBefore,
            TriggerSuspected = d.TriggerSuspected,
            HrDuringBpm = d.HrDuringBpm,
            BpDuringSystolic = d.BpDuringSystolic,
            BpDuringDiastolic = d.BpDuringDiastolic,
            ActionTaken = TrimNullable(d.ActionTaken, 500),
            RecoveryTimeMinutes = d.RecoveryTimeMinutes,
            PreventedFainting = d.PreventedFainting,
            Note = TrimNullable(d.Note, 1000),
            CreatedAt = DateTimeOffset.UtcNow,
        };
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

public sealed record EpisodeData
{
    public DateTimeOffset? StartTime { get; init; }
    public int? DurationMinutes { get; init; }
    public string? MainSymptom { get; init; }
    public PostureKind? PostureBefore { get; init; }
    public EpisodeTrigger TriggerSuspected { get; init; } = EpisodeTrigger.Unknown;
    public int? HrDuringBpm { get; init; }
    public int? BpDuringSystolic { get; init; }
    public int? BpDuringDiastolic { get; init; }
    public string? ActionTaken { get; init; }
    public int? RecoveryTimeMinutes { get; init; }
    public bool? PreventedFainting { get; init; }
    public string? Note { get; init; }
}
