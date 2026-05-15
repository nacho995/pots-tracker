using Pots.Domain;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

public sealed class MagicLinkTokenTests
{
    [Fact]
    public void Create_GeneratesUniqueRawTokensPerCall()
    {
        var (_, r1) = MagicLinkToken.Create("foo@example.com", TimeSpan.FromMinutes(15));
        var (_, r2) = MagicLinkToken.Create("foo@example.com", TimeSpan.FromMinutes(15));
        Assert.NotEqual(r1, r2);
        Assert.True(r1.Length > 30); // 32 random bytes → ~43 chars base64url
    }

    [Fact]
    public void Create_HashesRawTokenWithSha256LowercaseHex()
    {
        var (entity, raw) = MagicLinkToken.Create("foo@example.com", TimeSpan.FromMinutes(15));
        Assert.Equal(64, entity.TokenHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", entity.TokenHash);
        Assert.Equal(entity.TokenHash, MagicLinkToken.HashRaw(raw));
    }

    [Fact]
    public void Create_NormalizesEmail()
    {
        var (entity, _) = MagicLinkToken.Create("  Foo@Example.COM ", TimeSpan.FromMinutes(15));
        Assert.Equal("foo@example.com", entity.Email);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]  // > 1 hour cap
    public void Create_RejectsInvalidTtl(int minutes)
    {
        Assert.Throws<DomainException>(() =>
            MagicLinkToken.Create("foo@example.com", TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Consume_RejectsDoubleConsumption()
    {
        var (entity, _) = MagicLinkToken.Create("foo@example.com", TimeSpan.FromMinutes(15));
        entity.Consume(DateTimeOffset.UtcNow);
        var ex = Assert.Throws<DomainException>(() => entity.Consume(DateTimeOffset.UtcNow));
        Assert.Contains("consumed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consume_RejectsAfterExpiry()
    {
        var (entity, _) = MagicLinkToken.Create("foo@example.com", TimeSpan.FromMinutes(15));
        var past = DateTimeOffset.UtcNow.AddHours(2);
        var ex = Assert.Throws<DomainException>(() => entity.Consume(past));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HashRaw_RejectsEmpty()
    {
        Assert.Throws<DomainException>(() => MagicLinkToken.HashRaw(""));
    }
}
