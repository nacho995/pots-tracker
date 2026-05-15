using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class SymptomClient
{
    private readonly HttpClient _http;
    public SymptomClient(HttpClient http) => _http = http;

    public async Task<SymptomLogDto> RecordAsync(RecordSymptomsDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/me/symptoms", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SymptomLogDto>(cancellationToken: ct))!;
    }
}
