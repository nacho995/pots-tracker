using Pots.Domain;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

public sealed class EpisodeTests
{
    private static readonly Guid Patient = Guid.NewGuid();
    private static readonly Guid Recorder = Guid.NewGuid();

    [Fact]
    public void Create_AcceptsMinimalEpisode()
    {
        var e = Episode.Create(Patient, Recorder, new EpisodeData());
        Assert.NotEqual(Guid.Empty, e.Id);
        Assert.Equal(Patient, e.PatientId);
        Assert.Equal(Recorder, e.RecordedByUserId);
        Assert.Equal(EpisodeTrigger.Unknown, e.TriggerSuspected);
    }

    [Fact]
    public void Create_RejectsImplausibleHr()
    {
        Assert.Throws<DomainException>(() =>
            Episode.Create(Patient, Recorder, new EpisodeData { HrDuringBpm = 500 }));
    }

    [Fact]
    public void Create_RejectsImplausibleBp()
    {
        Assert.Throws<DomainException>(() =>
            Episode.Create(Patient, Recorder, new EpisodeData { BpDuringSystolic = 500 }));
        Assert.Throws<DomainException>(() =>
            Episode.Create(Patient, Recorder, new EpisodeData { BpDuringDiastolic = 500 }));
    }

    [Fact]
    public void Create_RejectsUndefinedTrigger()
    {
        Assert.Throws<DomainException>(() =>
            Episode.Create(Patient, Recorder, new EpisodeData { TriggerSuspected = (EpisodeTrigger)99 }));
    }

    [Fact]
    public void Create_RejectsEmptyRecorder()
    {
        Assert.Throws<DomainException>(() =>
            Episode.Create(Patient, Guid.Empty, new EpisodeData()));
    }
}
