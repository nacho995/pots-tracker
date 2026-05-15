namespace Pots.Domain.Entities;

public sealed class User
{
    // External-facing identity: UUIDv4 to avoid leaking account creation timestamp.
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string? DisplayName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { }

    public static User Create(string email, string? displayName = null)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = EmailValidator.Normalize(email),
            DisplayName = NormalizeDisplayName(displayName),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Rename(string? displayName)
    {
        DisplayName = NormalizeDisplayName(displayName);
    }

    private static string? NormalizeDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > 100)
            throw new DomainException("Display name is too long.");
        return trimmed;
    }
}
