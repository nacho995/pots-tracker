namespace Pots.Client.Services;

// Persisted via localStorage so the choice survives reloads. Singleton so
// every component sees the same value. Components subscribe to LanguageChanged
// and StateHasChanged on the event to re-render.
public sealed class LanguageService
{
    private const string Key = "pots.language";
    public const string Spanish = "es";
    public const string French = "fr";
    public const string English = "en";

    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        Spanish, French, English
    };

    private readonly LocalStorage _storage;

    public string Current { get; private set; } = Spanish;

    public event Action? LanguageChanged;

    public LanguageService(LocalStorage storage)
    {
        _storage = storage;
    }

    public async Task InitializeAsync()
    {
        var stored = await _storage.GetAsync(Key);
        if (!string.IsNullOrEmpty(stored) && Supported.Contains(stored))
        {
            Current = stored.ToLowerInvariant();
        }
    }

    public async Task SetAsync(string lang)
    {
        if (!Supported.Contains(lang)) return;
        var normalized = lang.ToLowerInvariant();
        if (normalized == Current) return;
        Current = normalized;
        await _storage.SetAsync(Key, normalized);
        LanguageChanged?.Invoke();
    }

    public string T(string key) => Strings.Get(Current, key);
}
