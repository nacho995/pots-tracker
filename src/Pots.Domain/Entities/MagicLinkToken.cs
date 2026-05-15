using System.Security.Cryptography;
using System.Text;

namespace Pots.Domain.Entities;

// Single-use, time-limited token persisted as its SHA-256 hash. The raw token
// is returned only once at creation, embedded in the email link, and never
// stored. RLS is intentionally NOT enabled for magic_link_tokens — sign-in
// must work for anonymous callers — but the token hash itself protects: even
// if the table is leaked, the raw tokens needed to claim a session are not
// recoverable.
public sealed class MagicLinkToken
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private MagicLinkToken() { }

    public static (MagicLinkToken Token, string RawToken) Create(string email, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero || ttl > TimeSpan.FromHours(1))
            throw new DomainException("Magic-link TTL must be (0, 1h].");

        var normalizedEmail = EmailValidator.Normalize(email);

        // 32 random bytes → 43-char URL-safe base64 (no padding). Plenty of
        // entropy against brute force.
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = ToBase64Url(rawBytes);

        var now = DateTimeOffset.UtcNow;
        var entity = new MagicLinkToken
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            TokenHash = HashRaw(rawToken),
            ExpiresAt = now + ttl,
            ConsumedAt = null,
            CreatedAt = now,
        };
        return (entity, rawToken);
    }

    public void Consume(DateTimeOffset now)
    {
        if (ConsumedAt is not null)
            throw new DomainException("Token already consumed.");
        if (now > ExpiresAt)
            throw new DomainException("Token expired.");
        ConsumedAt = now;
    }

    public static string HashRaw(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new DomainException("Token cannot be empty.");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        // Lowercase hex: matches the DB CHECK constraint (^[0-9a-f]{64}$) on
        // magic_link_tokens.token_hash. Any future ORM mapping or admin tool
        // that lowercases hashes will still hit the unique index.
        return Convert.ToHexStringLower(bytes);
    }

    private static string ToBase64Url(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
