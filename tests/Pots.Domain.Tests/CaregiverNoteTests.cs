using Pots.Domain;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

public sealed class CaregiverNoteTests
{
    private static readonly Guid Patient = Guid.NewGuid();
    private static readonly Guid Author = Guid.NewGuid();

    [Fact]
    public void Create_ValidNote_StartsActive()
    {
        var note = CaregiverNote.Create(Patient, Author, "Hoy te he visto pálida.");
        Assert.Equal(Patient, note.PatientId);
        Assert.Equal(Author, note.AuthorUserId);
        Assert.Equal("Hoy te he visto pálida.", note.Body);
        Assert.False(note.IsDeleted);
        Assert.Null(note.DeletedAt);
        Assert.Null(note.DeletedByUserId);
    }

    [Fact]
    public void Create_TrimsBody()
    {
        var note = CaregiverNote.Create(Patient, Author, "  hola  ");
        Assert.Equal("hola", note.Body);
    }

    [Fact]
    public void Create_RejectsEmptyOrWhitespaceBody()
    {
        Assert.Throws<DomainException>(() => CaregiverNote.Create(Patient, Author, ""));
        Assert.Throws<DomainException>(() => CaregiverNote.Create(Patient, Author, "   "));
    }

    [Fact]
    public void Create_RejectsBodyOver2000Chars()
    {
        var tooLong = new string('x', 2001);
        Assert.Throws<DomainException>(() => CaregiverNote.Create(Patient, Author, tooLong));
    }

    [Fact]
    public void Create_AcceptsBodyExactly2000Chars()
    {
        var maxBody = new string('x', 2000);
        var note = CaregiverNote.Create(Patient, Author, maxBody);
        Assert.Equal(2000, note.Body.Length);
    }

    [Fact]
    public void Create_RejectsEmptyPatientOrAuthor()
    {
        Assert.Throws<DomainException>(() => CaregiverNote.Create(Guid.Empty, Author, "x"));
        Assert.Throws<DomainException>(() => CaregiverNote.Create(Patient, Guid.Empty, "x"));
    }

    [Fact]
    public void SoftDelete_MarksDeletedAndStampsActor()
    {
        var note = CaregiverNote.Create(Patient, Author, "x");
        note.SoftDelete(Author);
        Assert.True(note.IsDeleted);
        Assert.NotNull(note.DeletedAt);
        Assert.Equal(Author, note.DeletedByUserId);
    }

    [Fact]
    public void SoftDelete_IsIdempotent()
    {
        var note = CaregiverNote.Create(Patient, Author, "x");
        note.SoftDelete(Author);
        var firstTime = note.DeletedAt;

        var someoneElse = Guid.NewGuid();
        note.SoftDelete(someoneElse);

        // Original deleter and timestamp preserved
        Assert.Equal(firstTime, note.DeletedAt);
        Assert.Equal(Author, note.DeletedByUserId);
    }

    [Fact]
    public void SoftDelete_RequiresDeleter()
    {
        var note = CaregiverNote.Create(Patient, Author, "x");
        Assert.Throws<DomainException>(() => note.SoftDelete(Guid.Empty));
    }
}
