using System.Net.Http.Json;
using System.Text.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class AuthClient
{
    private const string JwtKey = "pots.jwt";
    private readonly HttpClient _http;
    private readonly LocalStorage _storage;

    public event Action? AuthStateChanged;
    public string? CachedJwt { get; private set; }

    public AuthClient(HttpClient http, LocalStorage storage)
    {
        _http = http;
        _storage = storage;
    }

    public async Task InitializeAsync()
    {
        CachedJwt = await _storage.GetAsync(JwtKey);
    }

    public async Task RequestMagicLinkAsync(string email, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/auth/request-link", new RequestLinkDto(email), ct);
        // Endpoint always returns 200 regardless of email validity (no enumeration).
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> VerifyAsync(string rawToken, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/auth/verify", new VerifyDto(rawToken), ct);
        if (!response.IsSuccessStatusCode) return false;
        var body = await response.Content.ReadFromJsonAsync<VerifyResponse>(cancellationToken: ct);
        if (body is null || string.IsNullOrEmpty(body.AccessToken)) return false;
        await _storage.SetAsync(JwtKey, body.AccessToken);
        CachedJwt = body.AccessToken;
        AuthStateChanged?.Invoke();
        return true;
    }

    public async Task SignOutAsync()
    {
        await _storage.RemoveAsync(JwtKey);
        CachedJwt = null;
        AuthStateChanged?.Invoke();
    }

    public string? GetEmailFromJwt()
    {
        if (string.IsNullOrEmpty(CachedJwt)) return null;
        var parts = CachedJwt.Split('.');
        if (parts.Length < 2) return null;
        try
        {
            var payload = Base64UrlDecode(parts[1]);
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
        }
        catch { return null; }
    }

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
