namespace Pots.Shared.Contracts;

public sealed record SharedPatientDto(
    Guid PatientId,
    string PatientName,
    string OwnerEmail,
    string Role,
    DateTimeOffset GrantedAt
);
