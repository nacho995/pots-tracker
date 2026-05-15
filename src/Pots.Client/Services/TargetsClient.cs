using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class TargetsClient
{
    private readonly HttpClient _http;

    public TargetsClient(HttpClient http) => _http = http;

    public async Task<PatientTargetsDto> GetAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/me/targets", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PatientTargetsDto>(cancellationToken: ct))!;
    }

    public async Task<PatientTargetsDto> UpdateAsync(UpdateTargetsDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync("/me/targets", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PatientTargetsDto>(cancellationToken: ct))!;
    }

    public async Task<PatientTargetsDto> EnableSaltAsync(EnableSaltTargetDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/me/targets/salt/enable", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PatientTargetsDto>(cancellationToken: ct))!;
    }

    public async Task DisableSaltAsync(CancellationToken ct = default)
    {
        var response = await _http.PostAsync("/me/targets/salt/disable", null, ct);
        response.EnsureSuccessStatusCode();
    }
}
