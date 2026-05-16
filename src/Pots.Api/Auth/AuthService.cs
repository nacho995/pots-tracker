using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.Email;

namespace Pots.Api.Auth;

// Magic-link auth.
//
// Deliberate trade-offs documented for review:
//   - HS256 JWT, 24h access token, no refresh token (v1). The lifetime is
//     deliberately long to reduce re-auth friction for symptomatic users
//     (CLAUDE.md "spoon-aware UX"). Refresh+revocation comes in v2 when the
//     session-management UX exists.
//   - Per-email rate limit not yet implemented; IP-based limit only. TODO
//     v1.5: add a second partition keyed on sha256(normalizedEmail) for
//     spam amplification defence.
//   - Email send is awaited inline (no outbox). v2 if email volume requires.
//   - ForwardedHeaders not configured here; required when deployed behind a
//     reverse proxy so the rate limiter doesn't share a single bucket.
public sealed class AuthService
{
    private readonly PotsDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly JwtOptions _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(PotsDbContext db, IEmailSender emailSender, JwtOptions jwt, ILogger<AuthService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task SendInvitationAsync(string granteeEmail, string inviterEmail, string role, CancellationToken ct)
    {
        var normalizedEmail = EmailValidator.Normalize(granteeEmail);
        var (token, raw) = MagicLinkToken.Create(normalizedEmail, TimeSpan.FromHours(1));
        _db.MagicLinkTokens.Add(token);
        await _db.SaveChangesAsync(ct);

        var link = $"{_jwt.MagicLinkBaseUrl.TrimEnd('/')}/login/verify#token={Uri.EscapeDataString(raw)}";
        var verb = role == "Editor" ? "ver y editar" : "ver";
        var subject = $"{inviterEmail} te ha invitado a entrar en POTS";
        var body =
            $"Hola,\n\n" +
            $"{inviterEmail} te ha invitado a {verb} sus datos en POTS.\n\n" +
            $"Pulsa este enlace para aceptar (válido durante 1 hora):\n{link}\n\n" +
            "Si no esperabas esta invitación, puedes ignorar este correo.";
        await _emailSender.SendAsync(normalizedEmail, subject, body, ct);
    }

    public async Task RequestMagicLinkAsync(string? rawEmail, CancellationToken ct)
    {
        string normalizedEmail;
        try
        {
            normalizedEmail = EmailValidator.Normalize(rawEmail);
        }
        catch (DomainException ex)
        {
            _logger.LogDebug(ex, "Magic-link requested for invalid email");
            return;
        }

        var (token, raw) = MagicLinkToken.Create(normalizedEmail, TimeSpan.FromMinutes(15));
        _db.MagicLinkTokens.Add(token);
        await _db.SaveChangesAsync(ct);

        // Token placed in the URL fragment (#token=...) — fragments are NOT sent
        // to the server on subsequent requests and do not appear in access logs
        // or Referer headers. The client-side verify page reads window.location.hash
        // and POSTs to /auth/verify with the JSON body.
        var link = $"{_jwt.MagicLinkBaseUrl.TrimEnd('/')}/login/verify#token={Uri.EscapeDataString(raw)}";
        try
        {
            await _emailSender.SendAsync(
                normalizedEmail,
                "Tu enlace de inicio de sesión",
                $"Pulsa este enlace para entrar. Expira en 15 minutos:\n{link}",
                ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or InvalidOperationException)
        {
            // Email transport failure (provider 4xx/5xx, network, timeout).
            // We deliberately swallow so the endpoint can keep its opaque
            // response: leaking "send failed for this email" gives an attacker
            // a partial existence oracle. The token row remains in the DB;
            // an operator who fixes the provider can re-send manually, and
            // the next user-initiated attempt invalidates this token's siblings.
            _logger.LogError(ex, "Failed to send magic link to {Email}", normalizedEmail);
        }
    }

    public async Task<string?> VerifyAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;

        string hash;
        try { hash = MagicLinkToken.HashRaw(rawToken); }
        catch (DomainException) { return null; }

        // Atomic consume + provision + sibling-invalidation. NpgsqlRetryingExecutionStrategy
        // requires user-initiated transactions to run inside CreateExecutionStrategy().ExecuteAsync
        // so retries replay the full unit, not partial state.
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<(Guid userId, string email)?>(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var entry = await _db.MagicLinkTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
            if (entry is null) return null;

            try { entry.Consume(DateTimeOffset.UtcNow); }
            catch (DomainException) { return null; }

            // Sibling outstanding tokens for the same email are invalidated so
            // a successful verify renders prior emailed links useless.
            var siblings = await _db.MagicLinkTokens
                .Where(t => t.Email == entry.Email
                            && t.Id != entry.Id
                            && t.ConsumedAt == null
                            && t.ExpiresAt > DateTimeOffset.UtcNow)
                .ToListAsync(ct);
            foreach (var sibling in siblings) sibling.Consume(DateTimeOffset.UtcNow);

            await _db.SaveChangesAsync(ct);

            var userIdResult = await _db.Database
                .SqlQuery<Guid>($"SELECT auth_provision_user({entry.Email}::citext) AS \"Value\"")
                .ToListAsync(ct);
            var uid = userIdResult.Single();

            await tx.CommitAsync(ct);
            return (uid, entry.Email);
        }) is { } pair
            ? IssueJwt(pair.userId, pair.email)
            : null;
    }

    private string IssueJwt(Guid userId, string email)
    {
        var keyBytes = Convert.FromBase64String(_jwt.SigningKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey decodes to fewer than 32 bytes — invalid for HS256.");
        var key = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: now,
            expires: now.AddMinutes(_jwt.AccessTokenLifetimeMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
