using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Pots.Infrastructure.Email;

public sealed class MailjetOptions
{
    // Mailjet auth is HTTP Basic with two keys (not one bearer token).
    public string ApiKey { get; init; } = "";       // "public" key
    public string ApiSecret { get; init; } = "";    // "private" key
    public string SenderEmail { get; init; } = "";  // verified single sender
    public string SenderName { get; init; } = "POTS";
}

// Mailjet Send API v3.1. Auth is HTTP Basic (api_key:api_secret).
// Free tier: 200 messages/day with Single Sender Verification — verify the
// "From" address by clicking a link Mailjet emails to it; no domain required.
// Transport is HTTPS to api.mailjet.com (port 443) so Render's SMTP outbound
// blocking does not apply.
public sealed class MailjetEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly MailjetOptions _options;
    private readonly ILogger<MailjetEmailSender> _logger;

    public MailjetEmailSender(HttpClient http, MailjetOptions options, ILogger<MailjetEmailSender> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey)) throw new InvalidOperationException("Mailjet:ApiKey is required.");
        if (string.IsNullOrWhiteSpace(_options.ApiSecret)) throw new InvalidOperationException("Mailjet:ApiSecret is required.");
        if (string.IsNullOrWhiteSpace(_options.SenderEmail)) throw new InvalidOperationException("Mailjet:SenderEmail is required (verified Single Sender).");

        _http.BaseAddress = new Uri("https://api.mailjet.com/v3.1/");

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiKey}:{_options.ApiSecret}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            Messages = new[]
            {
                new
                {
                    From = new { Email = _options.SenderEmail, Name = _options.SenderName },
                    To = new[] { new { Email = to } },
                    Subject = subject,
                    TextPart = body
                }
            }
        };

        using var resp = await _http.PostAsJsonAsync("send", payload, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(cancellationToken);
            // Do NOT log the body — magic-link token would leak.
            _logger.LogError(
                "Mailjet send failed: {Status} to {To} subject {Subject}. Detail: {Detail}",
                (int)resp.StatusCode, to, subject, detail);
            throw new HttpRequestException($"Mailjet send failed: {(int)resp.StatusCode}");
        }
    }
}
