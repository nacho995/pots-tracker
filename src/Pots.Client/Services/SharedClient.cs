using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class SharedClient
{
    private readonly HttpClient _http;
    public SharedClient(HttpClient http) => _http = http;

    public async Task<List<SharedPatientDto>> ListAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/me/shared", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<SharedPatientDto>>(cancellationToken: ct)) ?? new();
    }
}
