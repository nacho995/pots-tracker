using System.Net;
using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class PatientClient
{
    private readonly HttpClient _http;

    public PatientClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<PatientDto?> GetMyPatientAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/me/patient", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PatientDto>(cancellationToken: ct);
    }

    public async Task<PatientDto> CreateMyPatientAsync(string name, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/me/patient", new CreatePatientDto(name), ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PatientDto>(cancellationToken: ct))!;
    }

    public async Task<PatientDto> RenameMyPatientAsync(string name, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync("/me/patient", new UpdatePatientDto(name), ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PatientDto>(cancellationToken: ct))!;
    }
}
