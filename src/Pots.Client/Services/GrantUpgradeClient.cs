using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

// Phase 3 client for the Viewer→Editor upgrade flow. Mirrors the API
// endpoints in GrantUpgradeRequestEndpoints.cs.
public sealed class GrantUpgradeClient
{
    private readonly HttpClient _http;

    public GrantUpgradeClient(HttpClient http)
    {
        _http = http;
    }

    public async Task RequestAsync(Guid patientId, string? message, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"/patients/{patientId}/grant-upgrade-requests",
            new CreateGrantUpgradeRequestDto(message),
            ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<GrantUpgradeRequestDto>> ListPendingForMyPatientAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/me/patient/grant-upgrade-requests", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<GrantUpgradeRequestDto>>(cancellationToken: ct)) ?? new();
    }

    public async Task ApproveAsync(Guid requestId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/me/patient/grant-upgrade-requests/{requestId}/approve", content: null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DenyAsync(Guid requestId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/me/patient/grant-upgrade-requests/{requestId}/deny", content: null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelAsync(Guid requestId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/me/grant-upgrade-requests/{requestId}", ct);
        response.EnsureSuccessStatusCode();
    }
}
