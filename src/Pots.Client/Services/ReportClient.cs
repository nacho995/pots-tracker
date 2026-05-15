using System.Net.Http.Json;
using Pots.Shared.Contracts;

namespace Pots.Client.Services;

public sealed class ReportClient
{
    private readonly HttpClient _http;
    public ReportClient(HttpClient http) => _http = http;

    public async Task<DoctorReportDto> GetAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var url = $"/me/report?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DoctorReportDto>(cancellationToken: ct))!;
    }

    public string CsvUrl(DateTimeOffset from, DateTimeOffset to) =>
        $"/me/report/csv?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
}
