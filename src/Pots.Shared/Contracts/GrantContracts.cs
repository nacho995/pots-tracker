namespace Pots.Shared.Contracts;

public sealed record GrantDto(Guid Id, string GranteeEmail, string Role, DateTimeOffset GrantedAt);

public sealed record InviteGrantDto(string GranteeEmail, string Role);
