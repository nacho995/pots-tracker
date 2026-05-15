namespace Pots.Shared.Contracts;

public sealed record PatientDto(Guid Id, string Name);
public sealed record CreatePatientDto(string Name);
public sealed record UpdatePatientDto(string Name);
public sealed record RequestLinkDto(string Email);
public sealed record VerifyDto(string Token);
public sealed record VerifyResponse(string AccessToken);
