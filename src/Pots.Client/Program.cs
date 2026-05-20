using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Pots.Client;
using Pots.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not configured (wwwroot/appsettings.json).");

// Singletons: IHttpClientFactory creates message handlers in its own internal
// scope, separate from the component scope. If AuthClient were Scoped, the
// PotsAuthMessageHandler would receive a different AuthClient instance than
// the one components mutate via VerifyAsync — the freshly issued JWT would
// never reach the Authorization header. WASM is single-threaded with one
// circuit, so Singleton is the correct lifetime for these auth services.
builder.Services.AddSingleton<LocalStorage>();
builder.Services.AddSingleton<LanguageService>();
builder.Services.AddTransient<PotsAuthMessageHandler>();

// No-auth HttpClient for the /auth endpoints (request-link, verify).
builder.Services.AddHttpClient("auth", c => c.BaseAddress = new Uri(apiBaseUrl));

builder.Services.AddSingleton<AuthClient>(sp => new AuthClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("auth"),
    sp.GetRequiredService<LocalStorage>()));

// Authed HttpClient typed-clients for protected endpoints.
builder.Services.AddHttpClient<PatientClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<GrantClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<StatusClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<EpisodeClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<TargetsClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<SymptomClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<VitalClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<ActionClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<TrendsClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<ReportClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<SharedClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<SharedPatientClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<GrantUpgradeClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();
builder.Services.AddHttpClient<CaregiverNoteClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<PotsAuthMessageHandler>();

// Singleton: the cached "is the user a patient, a caregiver, or neither?"
// answer is per-circuit and survives navigation. RoleContextService resolves
// the transient PatientClient/SharedClient per call to avoid freezing their
// HttpMessageHandler chain.
builder.Services.AddSingleton<RoleContextService>();

var app = builder.Build();

// Restore JWT from localStorage on the singleton AuthClient before first render
// so navigation guards see persisted auth state across page reloads.
var auth = app.Services.GetRequiredService<AuthClient>();
await auth.InitializeAsync();
var lang = app.Services.GetRequiredService<LanguageService>();
await lang.InitializeAsync();

// If the user is already authenticated (reload, return visit), pre-fetch
// their role so BottomNav and Home don't flash a patient-nav before settling
// into a caregiver-only layout. Swallowed: a transient network error here
// must not block the app from booting; the next consumer will retry.
if (!string.IsNullOrEmpty(auth.CachedJwt))
{
    var roles = app.Services.GetRequiredService<RoleContextService>();
    try { await roles.EnsureLoadedAsync(); }
    catch { /* boot anyway; pages will surface their own errors */ }
}

await app.RunAsync();
