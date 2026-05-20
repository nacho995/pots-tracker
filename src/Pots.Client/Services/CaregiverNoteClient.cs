using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class CaregiverNoteClient
{
    private readonly HttpClient _http;

    public CaregiverNoteClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<CaregiverNoteDto>> ListAsync(Guid patientId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/patients/{patientId}/caregiver-notes", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<CaregiverNoteDto>>(cancellationToken: ct)) ?? new();
    }

    public async Task CreateAsync(Guid patientId, string body, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"/patients/{patientId}/caregiver-notes",
            new CreateCaregiverNoteDto(body),
            ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid patientId, Guid noteId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/patients/{patientId}/caregiver-notes/{noteId}", ct);
        response.EnsureSuccessStatusCode();
    }
}
