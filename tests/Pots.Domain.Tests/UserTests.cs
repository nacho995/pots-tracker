using Pots.Domain;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

public sealed class UserTests
{
    [Fact]
    public void Create_NormalizesEmailAndAssignsV4Guid()
    {
        var user = User.Create("  Foo@Example.com  ");
        Assert.Equal("foo@example.com", user.Email);
        // UUIDv4 has version nibble = 4 in the upper 4 bits of the 7th byte.
        // (Verifying the entity does NOT use v7 — that would leak creation time.)
        Assert.Equal('4', user.Id.ToString()[14]);
    }

    [Fact]
    public void Create_RejectsInvalidEmail()
    {
        Assert.Throws<DomainException>(() => User.Create("not-an-email"));
    }

    [Fact]
    public void Create_AcceptsAndTrimsDisplayName()
    {
        var user = User.Create("foo@example.com", "  Ana  ");
        Assert.Equal("Ana", user.DisplayName);
    }

    [Fact]
    public void Create_NormalizesEmptyDisplayNameToNull()
    {
        var user = User.Create("foo@example.com", "   ");
        Assert.Null(user.DisplayName);
    }

    [Fact]
    public void Rename_RejectsOversizedDisplayName()
    {
        var user = User.Create("foo@example.com");
        Assert.Throws<DomainException>(() => user.Rename(new string('x', 101)));
    }
}
