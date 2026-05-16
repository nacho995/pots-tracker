using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Pots.Infrastructure.Email;

public sealed class AppsScriptOptions
{
    // URL of the deployed Google Apps Script web app
    // (https://script.google.com/macros/s/AKfycb.../exec).
    public string Url { get; init; } = "";

    // Shared secret. The Apps Script handler rejects any POST whose
    // X-Auth-Secret header doesn't match. Without this, anyone discovering
    // the URL could send mail through the user's Gmail until they rotate.
    public string Secret { get; init; } = "";
}

// Sends magic-link emails through a Google Apps Script web app deployed
// under the user's own Google account. The script invokes MailApp.sendEmail,
// which delegates to the user's Gmail and bypasses every third-party email
// provider's gating (no paid plans, no domain ownership, no card to verify).
//
// Quota is the standard Google Apps Script free limit for consumer Gmail:
// 100 recipients/day, which is two orders of magnitude beyond what a single
// POTS patient will ever need.
//
// Transport is HTTPS to script.google.com (port 443), so Render free tier's
// SMTP outbound blocking doesn't apply.
public sealed class AppsScriptEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly AppsScriptOptions _options;
    private readonly ILogger<AppsScriptEmailSender> _logger;

    public AppsScriptEmailSender(HttpClient http, AppsScriptOptions options, ILogger<AppsScriptEmailSender> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Url)) throw new InvalidOperationException("AppsScript:Url is required.");
        if (string.IsNullOrWhiteSpace(_options.Secret)) throw new InvalidOperationException("AppsScript:Secret is required.");
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var payload = new { to, subject, body };

        using var req = new HttpRequestMessage(HttpMethod.Post, _options.Url)
        {
            Content = JsonContent.Create(payload)
        };
        // Header carries the shared secret; the Apps Script doPost handler
        // rejects any request whose header doesn't match.
        req.Headers.Add("X-Auth-Secret", _options.Secret);

        using var resp = await _http.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(cancellationToken);
            // Do NOT log the body — magic-link token would leak.
            _logger.LogError(
                "Apps Script send failed: {Status} to {To} subject {Subject}. Detail: {Detail}",
                (int)resp.StatusCode, to, subject, detail);
            throw new HttpRequestException($"Apps Script send failed: {(int)resp.StatusCode}");
        }
    }
}
