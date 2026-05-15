using Pots.Domain;
using Xunit;

namespace Pots.Domain.Tests;

public sealed class EmailValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Normalize_RejectsNullOrEmpty(string? input)
    {
        Assert.Throws<DomainException>(() => EmailValidator.Normalize(input));
    }

    [Theory]
    [InlineData("no-at-sign")]
    [InlineData("@nope.com")]
    [InlineData("\"Foo\" <foo@bar.com>")]  // round-trip rejects display-name wrap
    public void Normalize_RejectsInvalidShapes(string input)
    {
        Assert.Throws<DomainException>(() => EmailValidator.Normalize(input));
    }

    [Fact]
    public void Normalize_RejectsOversizedEmail()
    {
        var oversized = new string('a', 320) + "@b.com";
        Assert.Throws<DomainException>(() => EmailValidator.Normalize(oversized));
    }

    [Theory]
    [InlineData("  Foo@Example.COM  ", "foo@example.com")]
    [InlineData("user+tag@gmail.com", "user+tag@gmail.com")]
    public void Normalize_LowercasesAndTrims(string input, string expected)
    {
        Assert.Equal(expected, EmailValidator.Normalize(input));
    }
}
