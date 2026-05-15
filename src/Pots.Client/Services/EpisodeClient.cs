using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class EpisodeClient
{
    private readonly HttpClient _http;

    public EpisodeClient(HttpClient http) => _http = http;

    public async Task<EpisodeDto> CreateAsync(CreateEpisodeDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/me/episodes", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EpisodeDto>(cancellationToken: ct))!;
    }

    public async Task<List<EpisodeDto>> ListAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/me/episodes", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<EpisodeDto>>(cancellationToken: ct)) ?? new();
    }
}
