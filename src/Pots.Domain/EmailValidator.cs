using System.Net.Mail;

namespace Pots.Domain;

public static class EmailValidator
{
    public const int MaxLength = 320;

    public static string Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email cannot be empty.");

        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length > MaxLength ||
            !MailAddress.TryCreate(normalized, out var parsed) ||
            parsed!.Address != normalized)
        {
            throw new DomainException("Email is not valid.");
        }
        return normalized;
    }
}
