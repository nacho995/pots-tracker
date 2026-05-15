using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class StatusClient
{
    private readonly HttpClient _http;
    public StatusClient(HttpClient http) => _http = http;

    public async Task<DailyStatusDto> RecordAsync(RecordStatusDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/me/status", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DailyStatusDto>(cancellationToken: ct))!;
    }

    public async Task<List<DailyStatusDto>> GetTodayAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/me/status/today", ct);
        if (!response.IsSuccessStatusCode) return new();
        return (await response.Content.ReadFromJsonAsync<List<DailyStatusDto>>(cancellationToken: ct)) ?? new();
    }

    public async Task<DailyStatusDto> UpdateDetailAsync(Guid statusId, UpdateStatusDetailDto dto, CancellationToken ct = default)
    {
        var response = await _http.PatchAsJsonAsync($"/me/status/{statusId}", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DailyStatusDto>(cancellationToken: ct))!;
    }

    public async Task DeleteAsync(Guid statusId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/me/status/{statusId}", ct);
        response.EnsureSuccessStatusCode();
    }
}
