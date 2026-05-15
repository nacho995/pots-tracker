using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Pots.Infrastructure.Email;

public sealed class ResendOptions
{
    public string ApiKey { get; init; } = "";
    public string FromAddress { get; init; } = "";
}

// Minimal Resend (resend.com) HTTP client. Resend's REST API accepts a single
// POST /emails with bearer auth. We deliberately keep this dependency-free —
// no SDK, just HttpClient — to avoid pulling another NuGet for one endpoint.
public sealed class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(HttpClient http, ResendOptions options, ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Resend:ApiKey is required.");
        if (string.IsNullOrWhiteSpace(_options.FromAddress))
            throw new InvalidOperationException("Resend:FromAddress is required.");

        _http.BaseAddress = new Uri("https://api.resend.com/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            from = _options.FromAddress,
            to = new[] { to },
            subject,
            text = body
        };

        using var resp = await _http.PostAsJsonAsync("emails", payload, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(cancellationToken);
            // Do NOT log the full magic-link body — token would leak. Subject + recipient is enough.
            _logger.LogError(
                "Resend send failed: {Status} to {To} subject {Subject}. Detail: {Detail}",
                (int)resp.StatusCode, to, subject, detail);
            throw new HttpRequestException($"Resend send failed: {(int)resp.StatusCode}");
        }
    }
}
