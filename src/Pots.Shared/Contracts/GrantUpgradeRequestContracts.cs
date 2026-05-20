namespace Pots.Shared.Contracts;

public sealed record CreateGrantUpgradeRequestDto(string? Message);

public sealed record GrantUpgradeRequestDto(
    Guid RequestId,
    Guid GrantId,
    string RequesterEmail,
    string? Message,
    DateTimeOffset RequestedAt);

public sealed record MyGrantUpgradeRequestDto(
    Guid RequestId,
    Guid PatientId,
    string PatientName,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ResolvedAt);
