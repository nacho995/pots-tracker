using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Pots.Infrastructure.Email;

public sealed class BrevoOptions
{
    public string ApiKey { get; init; } = "";
    public string SenderEmail { get; init; } = "";
    public string SenderName { get; init; } = "POTS";
}

// Minimal Brevo (brevo.com, ex-Sendinblue) transactional email client.
// POST https://api.brevo.com/v3/smtp/email with header `api-key`.
// Free tier: 300 emails/day, single-sender verification (no domain needed).
// We keep the dependency to System.Net.Http only — no SDK.
public sealed class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly BrevoOptions _options;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(HttpClient http, BrevoOptions options, ILogger<BrevoEmailSender> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Brevo:ApiKey is required.");
        if (string.IsNullOrWhiteSpace(_options.SenderEmail))
            throw new InvalidOperationException("Brevo:SenderEmail is required (the verified sender address).");

        _http.BaseAddress = new Uri("https://api.brevo.com/v3/");
        _http.DefaultRequestHeaders.Add("api-key", _options.ApiKey);
        _http.DefaultRequestHeaders.Add("accept", "application/json");
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            sender = new { name = _options.SenderName, email = _options.SenderEmail },
            to = new[] { new { email = to } },
            subject,
            textContent = body
        };

        using var resp = await _http.PostAsJsonAsync("smtp/email", payload, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(cancellationToken);
            // Do NOT log the body — the magic-link token would leak. Subject + recipient is enough.
            _logger.LogError(
                "Brevo send failed: {Status} to {To} subject {Subject}. Detail: {Detail}",
                (int)resp.StatusCode, to, subject, detail);
            throw new HttpRequestException($"Brevo send failed: {(int)resp.StatusCode}");
        }
    }
}
