using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Pots.Api.Auth;
using Pots.Api.Patients;
using Pots.Infrastructure;
using Pots.Infrastructure.Email;
using Pots.Infrastructure.RowLevelSecurity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Pots.Api connects as pots_app (RLS-bound). Migrations run separately as pots_dev.
var connectionString = builder.Configuration.GetConnectionString("Pots")
    ?? throw new InvalidOperationException("ConnectionStrings:Pots is not configured.");
builder.Services.AddPotsInfrastructure(connectionString);

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt section is not configured.");
if (string.IsNullOrWhiteSpace(jwt.Issuer) ||
    string.IsNullOrWhiteSpace(jwt.Audience) ||
    string.IsNullOrWhiteSpace(jwt.SigningKey) ||
    string.IsNullOrWhiteSpace(jwt.MagicLinkBaseUrl))
{
    throw new InvalidOperationException("Jwt:Issuer/Audience/SigningKey/MagicLinkBaseUrl are all required.");
}

// Refuse to boot if the known dev signing key is still in place outside
// Development. Same for the localhost magic-link URL — a real deploy
// emailing localhost links to users would silently fail the sign-in flow.
const string KnownDevSigningKey = "ZGV2LW9ubHktc2lnbmluZy1rZXktY2hhbmdlLW1lLWluLXByb2QtcGxlYXNlLXJlYWxseQ==";
if (!builder.Environment.IsDevelopment())
{
    if (jwt.SigningKey == KnownDevSigningKey)
        throw new InvalidOperationException(
            "Refusing to start: known-dev Jwt:SigningKey detected in non-Development environment. " +
            "Override Jwt__SigningKey via environment variable or secret store.");
    if (jwt.MagicLinkBaseUrl.Contains("localhost"))
        throw new InvalidOperationException(
            "Refusing to start: Jwt:MagicLinkBaseUrl contains 'localhost' in non-Development. " +
            "Set the public URL via Jwt__MagicLinkBaseUrl.");
}

builder.Services.AddSingleton(jwt);

builder.Services.AddHttpContextAccessor();
// Override the Infrastructure fallback with a request-scoped, JWT-claim-backed
// implementation. Without this, every request would be anonymous.
builder.Services.AddScoped<IUserContext, HttpContextUserContext>();
builder.Services.AddScoped<AuthService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
}
else
{
    throw new InvalidOperationException(
        "No IEmailSender configured for non-Development. Register a real provider " +
        "(SendGrid, Postmark, SES, etc.) before deploying.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        var keyBytes = Convert.FromBase64String(jwt.SigningKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey decodes to fewer than 32 bytes.");
        // Read JWT claims by their canonical names ("sub", "email"), not the
        // ASP.NET legacy mapping to ClaimTypes.NameIdentifier.
        opts.MapInboundClaims = false;
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p
        .WithOrigins("http://localhost:5185", "https://localhost:7211")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddRateLimiter(o =>
{
    // 5 requests / IP / 5 minutes. TODO v1.5: add a second per-email partition
    // keyed on sha256(normalizedEmail) to defend against spam amplification.
    // TODO production: configure ForwardedHeaders middleware so RemoteIpAddress
    // reflects the real client, not the reverse proxy.
    o.AddPolicy("auth-request-link", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    o.AddPolicy("auth-verify", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapPatientEndpoints();
app.MapGrantEndpoints();
app.MapStatusEndpoints();
app.MapEpisodeEndpoints();
app.MapTargetsEndpoints();
app.MapSymptomEndpoints();
app.MapVitalEndpoints();
app.MapPreventiveActionEndpoints();
app.MapTrendsEndpoints();
app.MapReportEndpoints();
app.MapSharedEndpoints();

app.Run();
