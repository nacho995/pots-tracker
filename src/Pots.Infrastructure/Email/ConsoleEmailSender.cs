using Microsoft.Extensions.Logging;

namespace Pots.Infrastructure.Email;

// Dev-only sender: logs the email content. Magic-link tokens appear in the
// server log so a developer can click the link without a real mail relay.
// MUST NOT be used in non-Development environments — Program.cs guards this.
public sealed class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        // LogInformation rather than Debug so a developer running `dotnet run`
        // sees magic-link emails by default. The class is Dev-only — Program.cs
        // throws in non-Dev without an override — so prod log aggregators never
        // see this output.
        _logger.LogInformation(
            "[DEV EMAIL]\nTo: {To}\nSubject: {Subject}\n---\n{Body}\n---",
            to, subject, body);
        return Task.CompletedTask;
    }
}
