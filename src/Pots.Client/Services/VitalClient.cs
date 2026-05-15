using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class VitalClient
{
    private readonly HttpClient _http;
    public VitalClient(HttpClient http) => _http = http;

    public async Task<VitalLogDto> RecordAsync(RecordVitalsDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/me/vitals", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VitalLogDto>(cancellationToken: ct))!;
    }
}
