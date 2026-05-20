namespace Pots.Shared.Contracts;

public sealed record CreateCaregiverNoteDto(string Body);

public sealed record CaregiverNoteDto(
    Guid Id,
    Guid AuthorUserId,
    string AuthorEmail,
    string Body,
    DateTimeOffset CreatedAt,
    bool CanDelete);
