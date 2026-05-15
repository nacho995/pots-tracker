using Pots.Domain;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

public sealed class PatientTests
{
    [Fact]
    public void Create_RequiresOwner()
    {
        Assert.Throws<DomainException>(() => Patient.Create(Guid.Empty, "Ana"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsEmptyName(string? name)
    {
        Assert.Throws<DomainException>(() => Patient.Create(Guid.NewGuid(), name!));
    }

    [Fact]
    public void Create_RejectsOversizedName()
    {
        Assert.Throws<DomainException>(() => Patient.Create(Guid.NewGuid(), new string('a', 101)));
    }

    [Fact]
    public void Create_TrimsAndAssignsV4Id()
    {
        var owner = Guid.NewGuid();
        var p = Patient.Create(owner, "  Ana  ");
        Assert.Equal("Ana", p.Name);
        Assert.Equal(owner, p.OwnerUserId);
        Assert.Equal('4', p.Id.ToString()[14]); // UUIDv4 — no creation-time leak
        Assert.Null(p.DeletedAt);
    }

    [Fact]
    public void Rename_UpdatesUpdatedAt()
    {
        var p = Patient.Create(Guid.NewGuid(), "Ana");
        var before = p.UpdatedAt;
        Thread.Sleep(5);
        p.Rename("Anita");
        Assert.Equal("Anita", p.Name);
        Assert.True(p.UpdatedAt > before);
    }

    [Fact]
    public void SoftDelete_SetsDeletedAtAndIsIdempotent()
    {
        var p = Patient.Create(Guid.NewGuid(), "Ana");
        p.SoftDelete();
        var firstDeletedAt = p.DeletedAt;
        Assert.NotNull(firstDeletedAt);

        Thread.Sleep(5);
        p.SoftDelete(); // second call should be no-op
        Assert.Equal(firstDeletedAt, p.DeletedAt);
    }
}
