namespace Pots.Shared.Contracts;

public sealed record PatientDto(Guid Id, string Name);

// Returned by GET /patients/{id} (caregiver-facing). Tells the client what
// role the caller has on this specific patient so the UI can decide whether
// to show "request editor access", editor-only write controls (Phase 5),
// or just read-only chrome.
//
// CallerRole values: "Owner" | "Viewer" | "Editor".
// PendingUpgradeRequestId is non-null only when CallerRole == "Viewer" AND
// the caller has an open request awaiting the owner's decision.
public sealed record SharedPatientContextDto(
    Guid Id,
    string Name,
    string CallerRole,
    Guid? PendingUpgradeRequestId);

public sealed record CreatePatientDto(string Name);
public sealed record UpdatePatientDto(string Name);
public sealed record RequestLinkDto(string Email);
public sealed record VerifyDto(string Token);
public sealed record VerifyResponse(string AccessToken);
