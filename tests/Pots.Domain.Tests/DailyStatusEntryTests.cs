using Pots.Domain;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

public sealed class DailyStatusEntryTests
{
    private static readonly Guid Patient = Guid.NewGuid();
    private static readonly Guid Recorder = Guid.NewGuid();

    [Fact]
    public void Create_StampsPatientAndRecorder()
    {
        var entry = DailyStatusEntry.Create(Patient, Recorder, DailyStatusKind.Green);
        Assert.Equal(Patient, entry.PatientId);
        Assert.Equal(Recorder, entry.RecordedByUserId);
        Assert.Equal(DailyStatusKind.Green, entry.Status);
    }

    [Fact]
    public void Create_RejectsEmptyPatient()
    {
        Assert.Throws<DomainException>(() =>
            DailyStatusEntry.Create(Guid.Empty, Recorder, DailyStatusKind.Green));
    }

    [Fact]
    public void Create_RejectsEmptyRecorder()
    {
        Assert.Throws<DomainException>(() =>
            DailyStatusEntry.Create(Patient, Guid.Empty, DailyStatusKind.Green));
    }

    [Fact]
    public void Create_RejectsUndefinedStatus()
    {
        Assert.Throws<DomainException>(() =>
            DailyStatusEntry.Create(Patient, Recorder, (DailyStatusKind)99));
    }

    [Fact]
    public void Create_AcceptsAllOptionalDetailFields()
    {
        var entry = DailyStatusEntry.Create(
            Patient, Recorder, DailyStatusKind.Orange,
            posture: PostureKind.Sitting,
            activity: "trabajando",
            locationNote: "casa",
            note: "ligero mareo",
            episodeOccurred: false);

        Assert.Equal(PostureKind.Sitting, entry.Posture);
        Assert.Equal("trabajando", entry.Activity);
        Assert.Equal("casa", entry.LocationNote);
        Assert.Equal("ligero mareo", entry.Note);
        Assert.False(entry.EpisodeOccurred);
    }
}
