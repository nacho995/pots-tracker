using System.Net;
using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

// Cross-patient read client. Used by the caregiver-side `/shared/{id}*`
// pages to read another patient's data via `/patients/{id}/...`. The
// caller's access is enforced server-side by RLS — there is no need (and no
// way) for the client to pre-check.
//
// Kept separate from PatientClient (which is owner-only via `/me/...`) so
// the role boundary at the call site is explicit: a page using
// SharedPatientClient is, by construction, working on someone else's data.
//
// CONTRACT — 404 handling on the list endpoints:
//
// GetPatientAsync returns null on 404, signalling either "patient doesn't
// exist" or "RLS hides it from you" (indistinguishable, by design).
//
// GetTodayStatusAsync and GetEpisodesAsync return an empty list on 404 OR
// on a legitimate empty 200 — they do NOT distinguish. Callers MUST gate on
// GetPatientAsync first to disambiguate "no access" from "no data." Both
// SharedDashboard.razor and SharedEpisodes.razor follow this pattern. Any
// new caller that skips the patient lookup will silently mis-classify a
// revoked grant as "no episodes yet" — don't do that.
public sealed class SharedPatientClient
{
    private readonly HttpClient _http;

    public SharedPatientClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<SharedPatientContextDto?> GetPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/patients/{patientId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SharedPatientContextDto>(cancellationToken: ct);
    }

    public async Task<List<DailyStatusDto>> GetTodayStatusAsync(Guid patientId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/patients/{patientId}/status/today", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return new();
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<DailyStatusDto>>(cancellationToken: ct)) ?? new();
    }

    public async Task<List<EpisodeDto>> GetEpisodesAsync(Guid patientId, int take = 50, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/patients/{patientId}/episodes?take={take}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return new();
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<EpisodeDto>>(cancellationToken: ct)) ?? new();
    }

    // Phase 5: Editor (or owner) records a status entry on behalf of the
    // patient. Returns the created entry.
    public async Task<DailyStatusDto?> RecordStatusAsync(Guid patientId, RecordStatusDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/patients/{patientId}/status", dto, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DailyStatusDto>(cancellationToken: ct);
    }
}
