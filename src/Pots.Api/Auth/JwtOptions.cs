namespace Pots.Api.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    // Base64-encoded symmetric key. At least 32 bytes after decode for HS256.
    public string SigningKey { get; set; } = null!;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
    public string MagicLinkBaseUrl { get; set; } = null!;
}
