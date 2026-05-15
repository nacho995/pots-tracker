using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class TrendsClient
{
    private readonly HttpClient _http;
    public TrendsClient(HttpClient http) => _http = http;

    public async Task<TrendsDto> GetAsync(int days = 30, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/me/trends?days={days}", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TrendsDto>(cancellationToken: ct))!;
    }
}
