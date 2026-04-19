using Globalization = System.Globalization;
using Microsoft.Extensions.Options;
using OctopusDashboard.Models;
using System.Net.Http.Headers;
using System.Text;

namespace OctopusDashboard.Services;

public class OctopusService(HttpClient httpClient, IOptions<OctopusSettings> options) : IOctopusService
{
    private readonly OctopusSettings _settings = options.Value;
    private const decimal GasM3ToKwh = 11.1m;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.ApiKey) &&
        !string.IsNullOrWhiteSpace(_settings.Mpan) &&
        !string.IsNullOrWhiteSpace(_settings.ElectricityMeterSerial) &&
        !string.IsNullOrWhiteSpace(_settings.Mprn) &&
        !string.IsNullOrWhiteSpace(_settings.GasMeterSerial);

    public Task<ConsumptionSummary> GetElectricityConsumptionAsync(
        DateTimeOffset from, DateTimeOffset to, string groupBy, CancellationToken ct = default)
    {
        var path = $"electricity-meter-points/{_settings.Mpan}/meters/{_settings.ElectricityMeterSerial}/consumption/";
        return FetchSummaryAsync(path, from, to, groupBy, "kWh", ct);
    }

    public Task<ConsumptionSummary> GetGasConsumptionAsync(
        DateTimeOffset from, DateTimeOffset to, string groupBy, CancellationToken ct = default)
    {
        var path = $"gas-meter-points/{_settings.Mprn}/meters/{_settings.GasMeterSerial}/consumption/";
        return FetchSummaryAsync(path, from, to, groupBy, "m\u00B3", ct);
    }

    private async Task<ConsumptionSummary> FetchSummaryAsync(
        string path, DateTimeOffset from, DateTimeOffset to, string groupBy, string unit, CancellationToken ct)
    {
        ConfigureAuth();

        var url = BuildUrl(path, from, to, "page_size=25000&order_by=period");
        var raw = await FetchAllPagesAsync<ConsumptionInterval>(url, ct);
        var grouped = GroupIntervals(raw, groupBy);
        var total = grouped.Sum(i => i.Consumption);

        return new ConsumptionSummary(grouped, total, unit);
    }

    private static List<ConsumptionInterval> GroupIntervals(List<ConsumptionInterval> intervals, string groupBy)
    {
        if (groupBy == "half_hour") return intervals;

        var tz = TimeZoneInfo.Local;
        DateTime Local(ConsumptionInterval i) => TimeZoneInfo.ConvertTimeFromUtc(i.IntervalStart.UtcDateTime, tz);

        Func<ConsumptionInterval, object> key = groupBy switch
        {
            "hour" => i => { var l = Local(i); return (object)(l.Date, l.Hour); },
            "week" => i => { var l = Local(i); return (object)(l.Year * 100 + Globalization.ISOWeek.GetWeekOfYear(l)); },
            "month" => i => { var l = Local(i); return (object)(l.Year * 100 + l.Month); },
            _ => i => (object)Local(i).Date
        };

        return [.. intervals
            .GroupBy(key)
            .Select(g => new ConsumptionInterval(
                g.Sum(i => i.Consumption),
                g.Min(i => i.IntervalStart),
                g.Max(i => i.IntervalEnd)))
            .OrderBy(i => i.IntervalStart)];
    }

    private async Task<List<T>> FetchAllPagesAsync<T>(string url, CancellationToken ct)
    {
        var all = new List<T>();
        string? currentUrl = url;

        while (currentUrl is not null)
        {
            var response = await httpClient.GetFromJsonAsync<PagedResponse<T>>(currentUrl, ct);
            if (response is null) break;
            all.AddRange(response.Results);
            currentUrl = response.Next;
        }

        return all;
    }

    private static string BuildUrl(string path, DateTimeOffset from, DateTimeOffset to, string? extra = null)
    {
        var url = $"{path}?period_from={from.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}&period_to={to.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}";
        return extra is null ? url : $"{url}&{extra}";
    }

    private void ConfigureAuth()
    {
        if (!string.IsNullOrEmpty(_settings.ApiKey) &&
            httpClient.DefaultRequestHeaders.Authorization is null)
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_settings.ApiKey}:"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }
}
