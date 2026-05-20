namespace Pots.Shared.Contracts;

// User-account-level data, separate from the POTS patient profile.
// Phase 6 split: every user has a name+email (this), but only patients
// have a POTS profile (PatientDto). Caregivers have an AccountDto and
// no PatientDto; the previous design conflated the two and trapped
// caregivers into creating a Patient they didn't want.
public sealed record AccountDto(
    string Email,
    string? DisplayName,
    bool IsPatient,
    bool HasSharedAccess);

public sealed record UpdateAccountDto(string? DisplayName);
