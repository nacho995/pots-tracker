namespace Pots.Shared.Contracts;

public sealed record GrantDto(Guid Id, string GranteeEmail, string Role, DateTimeOffset GrantedAt);

public sealed record InviteGrantDto(string GranteeEmail, string Role);

// Phase 7.1: owner-driven direct toggle between Viewer and Editor, separate
// from the upgrade-request flow.
public sealed record SetGrantRoleDto(string Role);
