using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Pots.Infrastructure.Email;

public sealed class SendGridOptions
{
    public string ApiKey { get; init; } = "";
    public string SenderEmail { get; init; } = "";
    public string SenderName { get; init; } = "POTS";
}

// SendGrid v3 mail/send. Free tier: 100 emails/day, Single Sender Verification
// works without owning a domain (verify the From address via a confirmation
// email). Outbound traffic is HTTPS to api.sendgrid.com which Render allows,
// unlike SMTP on 25/465/587 which Render free silently times out.
public sealed class SendGridEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly SendGridOptions _options;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(HttpClient http, SendGridOptions options, ILogger<SendGridEmailSender> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey)) throw new InvalidOperationException("SendGrid:ApiKey is required.");
        if (string.IsNullOrWhiteSpace(_options.SenderEmail)) throw new InvalidOperationException("SendGrid:SenderEmail is required (the verified Single Sender address).");

        _http.BaseAddress = new Uri("https://api.sendgrid.com/v3/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            personalizations = new[]
            {
                new { to = new[] { new { email = to } } }
            },
            from = new { email = _options.SenderEmail, name = _options.SenderName },
            subject,
            content = new[]
            {
                new { type = "text/plain", value = body }
            }
        };

        using var resp = await _http.PostAsJsonAsync("mail/send", payload, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(cancellationToken);
            // Do NOT log the body — magic-link token would leak.
            _logger.LogError(
                "SendGrid send failed: {Status} to {To} subject {Subject}. Detail: {Detail}",
                (int)resp.StatusCode, to, subject, detail);
            throw new HttpRequestException($"SendGrid send failed: {(int)resp.StatusCode}");
        }
    }
}
