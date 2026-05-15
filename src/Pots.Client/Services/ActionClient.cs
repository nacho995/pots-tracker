using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class ActionClient
{
    private readonly HttpClient _http;
    public ActionClient(HttpClient http) => _http = http;

    public async Task<ActionLogDto> UpsertAsync(UpsertActionsDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync("/me/actions", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ActionLogDto>(cancellationToken: ct))!;
    }
}
