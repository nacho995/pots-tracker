using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

// Phase 6 — client for user-account-level data (separate from Patient).
public sealed class AccountClient
{
    private readonly HttpClient _http;

    public AccountClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<AccountDto?> GetAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/me/account", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountDto>(cancellationToken: ct);
    }

    public async Task<AccountDto> UpdateDisplayNameAsync(string? displayName, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync("/me/account", new UpdateAccountDto(displayName), ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountDto>(cancellationToken: ct))!;
    }
}
