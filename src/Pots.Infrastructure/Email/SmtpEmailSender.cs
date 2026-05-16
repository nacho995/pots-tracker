using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Pots.Infrastructure.Email;

public sealed class SmtpOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string FromEmail { get; init; } = "";
    public string FromName { get; init; } = "POTS";
}

// MailKit-backed SMTP sender. Use case: Gmail SMTP with an App Password
// (smtp.gmail.com:587 + STARTTLS). Gmail caps at ~500 messages/day on free
// accounts which is several orders of magnitude beyond what a personal POTS
// tracker needs. Microsoft's recommended SMTP client (System.Net.Mail's
// SmtpClient is officially obsolete for new code).
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger)
    {
        _options = options;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Host)) throw new InvalidOperationException("Smtp:Host is required.");
        if (string.IsNullOrWhiteSpace(_options.Username)) throw new InvalidOperationException("Smtp:Username is required.");
        if (string.IsNullOrWhiteSpace(_options.Password)) throw new InvalidOperationException("Smtp:Password is required (Gmail App Password, not the account password).");
        if (string.IsNullOrWhiteSpace(_options.FromEmail)) throw new InvalidOperationException("Smtp:FromEmail is required.");
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        try
        {
            // Port 587 → STARTTLS (Gmail's standard). Port 465 → implicit TLS.
            var socketOpt = _options.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_options.Host, _options.Port, socketOpt, cancellationToken);
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            // Do NOT log the message body — magic-link token would leak.
            _logger.LogError(ex, "SMTP send failed to {To} subject {Subject}", to, subject);
            // Wrap in HttpRequestException so AuthService's swallow filter catches it.
            throw new HttpRequestException("SMTP send failed", ex);
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(quit: true, cancellationToken);
        }
    }
}
