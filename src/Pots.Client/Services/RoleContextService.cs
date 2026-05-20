using Microsoft.Extensions.DependencyInjection;

namespace Pots.Client.Services;

// Caches the post-login answer to: "is the signed-in user a POTS patient,
// a caregiver invited to view someone's data, or neither?"
//
// Why a service: routing (Home), nav chrome (BottomNav) and several pages
// all branch on this. A single fetch on first read, then event-driven
// invalidation on auth state change, keeps the UI consistent and avoids
// hammering /me/patient + /me/shared from multiple components.
//
// Singleton lifetime (matches AuthClient): WASM is single-threaded with one
// circuit; the cached state is per-user and survives navigation. Auth state
// changes (sign-in, sign-out) clear it so the next reader refreshes.
//
// Transient typed clients (PatientClient, SharedClient) are resolved per
// call via IServiceProvider — capturing a transient in a singleton freezes
// its internal HttpMessageHandler chain.
//
// CONTRACT — callers MUST gate on Auth.CachedJwt before reading flags:
//
//   if (string.IsNullOrEmpty(Auth.CachedJwt)) { redirect to /login; return; }
//   await Roles.EnsureLoadedAsync();
//   if (Roles.IsPatient) { ... } else if (Roles.HasSharedAccess) { ... }
//
// When no JWT is present, EnsureLoadedAsync is a no-op and the boolean
// flags remain false. Reading IsPatient/IsCaregiverOnly/HasNoRole without
// the auth gate would conflate "unauthenticated" with "loaded as no-role" —
// future contributors should not skip the auth check.
public sealed class RoleContextService
{
    private readonly IServiceProvider _services;
    private readonly AuthClient _auth;
    private Task? _loadTask;

    public bool HasOwnPatient { get; private set; }
    public bool HasSharedAccess { get; private set; }
    public bool IsLoaded { get; private set; }

    public bool IsPatient => HasOwnPatient;
    public bool IsCaregiverOnly => IsLoaded && !HasOwnPatient && HasSharedAccess;
    public bool HasNoRole => IsLoaded && !HasOwnPatient && !HasSharedAccess;

    public event Action? RoleStateChanged;

    public RoleContextService(IServiceProvider services, AuthClient auth)
    {
        _services = services;
        _auth = auth;
        _auth.AuthStateChanged += OnAuthChanged;
    }

    private void OnAuthChanged()
    {
        IsLoaded = false;
        HasOwnPatient = false;
        HasSharedAccess = false;
        _loadTask = null;
        RoleStateChanged?.Invoke();
    }

    public Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (IsLoaded) return Task.CompletedTask;
        if (string.IsNullOrEmpty(_auth.CachedJwt)) return Task.CompletedTask;
        return _loadTask ??= LoadAsync(ct);
    }

    // Call after operations that change the role (creating a patient, grant
    // revoked, etc.) so the next consumer sees fresh values.
    public void Invalidate()
    {
        IsLoaded = false;
        _loadTask = null;
        RoleStateChanged?.Invoke();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            var patients = _services.GetRequiredService<PatientClient>();
            var shared = _services.GetRequiredService<SharedClient>();
            var patientTask = patients.GetMyPatientAsync(ct);
            var sharedTask = shared.ListAsync(ct);
            await Task.WhenAll(patientTask, sharedTask);
            HasOwnPatient = patientTask.Result is not null;
            HasSharedAccess = sharedTask.Result.Count > 0;
            IsLoaded = true;
            RoleStateChanged?.Invoke();
        }
        catch
        {
            // Reset the in-flight task so a retry can be started. We do NOT
            // mark IsLoaded=true with stale defaults: a transient network
            // failure must not silently re-classify a patient as no-role.
            _loadTask = null;
            throw;
        }
    }
}
