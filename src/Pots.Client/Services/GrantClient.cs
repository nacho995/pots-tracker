using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class GrantClient
{
    private readonly HttpClient _http;

    public GrantClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<GrantDto>> ListAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/me/patient/grants", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<GrantDto>>(cancellationToken: ct))
               ?? new List<GrantDto>();
    }

    public async Task<(bool ok, string? errorCode)> InviteAsync(string email, string role, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/me/patient/grants",
            new InviteGrantDto(email, role), ct);
        if (response.IsSuccessStatusCode) return (true, null);

        string? code = null;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(cancellationToken: ct);
            code = problem?.Code;
        }
        catch { /* ignore */ }
        return (false, code ?? response.StatusCode.ToString());
    }

    public async Task RevokeAsync(Guid grantId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/me/patient/grants/{grantId}", ct);
        response.EnsureSuccessStatusCode();
    }

    private sealed record ProblemBody(string? Code);
}
