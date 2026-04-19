using Globalization = System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OctopusDashboard.Data;
using OctopusDashboard.Models;
using System.Net.Http.Headers;
using System.Text;

namespace OctopusDashboard.Services;

public class OctopusService(
    HttpClient httpClient,
    IOptions<OctopusSettings> options,
    IDbContextFactory<OctopusDbContext> dbFactory) : IOctopusService
{
    private readonly OctopusSettings _settings = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.ApiKey) &&
        !string.IsNullOrWhiteSpace(_settings.Mpan) &&
        !string.IsNullOrWhiteSpace(_settings.ElectricityMeterSerial) &&
        !string.IsNullOrWhiteSpace(_settings.Mprn) &&
        !string.IsNullOrWhiteSpace(_settings.GasMeterSerial);

    public async Task<ConsumptionSummary> GetElectricityConsumptionAsync(
        DateTimeOffset from, DateTimeOffset to, string groupBy, CancellationToken ct = default)
    {
        ConfigureAuth();
        var path = $"electricity-meter-points/{_settings.Mpan}/meters/{_settings.ElectricityMeterSerial}/consumption/";
        var raw = await GetCachedRawIntervalsAsync("electricity", path, from, to, ct);

        var tariffCode = !string.IsNullOrWhiteSpace(_settings.ElectricityTariffCode)
            ? _settings.ElectricityTariffCode
            : await GetTariffCodeAsync(_settings.Mpan, true, from, ct);

        var grouped = GroupIntervals(raw, groupBy);
        var total = grouped.Sum(i => i.Consumption);
        var (unitCost, avgRate, standingCharge) = await GetCostDataAsync(tariffCode, "electricity-tariffs", from, to, raw, ct);

        return new ConsumptionSummary(grouped, total, "kWh", unitCost, avgRate, standingCharge);
    }

    public async Task<ConsumptionSummary> GetGasConsumptionAsync(
        DateTimeOffset from, DateTimeOffset to, string groupBy, CancellationToken ct = default)
    {
        ConfigureAuth();
        var path = $"gas-meter-points/{_settings.Mprn}/meters/{_settings.GasMeterSerial}/consumption/";
        var raw = await GetCachedRawIntervalsAsync("gas", path, from, to, ct);

        var tariffCode = !string.IsNullOrWhiteSpace(_settings.GasTariffCode)
            ? _settings.GasTariffCode
            : await GetTariffCodeAsync(_settings.Mprn, false, from, ct);

        var grouped = GroupIntervals(raw, groupBy);
        var total = grouped.Sum(i => i.Consumption);
        var (unitCost, avgRate, standingCharge) = await GetCostDataAsync(tariffCode, "gas-tariffs", from, to, raw, ct);

        return new ConsumptionSummary(grouped, total, "m\u00B3", unitCost, avgRate, standingCharge);
    }

    private async Task<List<ConsumptionInterval>> GetCachedRawIntervalsAsync(
        string energyType, string apiPath, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = DateOnly.FromDateTime(from.UtcDateTime);
        var toDate = DateOnly.FromDateTime(to.UtcDateTime);
        var histToDate = toDate < todayUtc ? toDate : todayUtc;

        if (fromDate < histToDate)
        {
            var neededDates = Enumerable.Range(0, histToDate.DayNumber - fromDate.DayNumber)
                .Select(i => fromDate.AddDays(i))
                .ToList();

            var cachedDates = await db.CachedDays
                .Where(d => d.EnergyType == energyType && d.Date >= fromDate && d.Date < histToDate)
                .Select(d => d.Date)
                .ToListAsync(ct);

            var missingDates = neededDates.Except(cachedDates).OrderBy(d => d).ToList();

            foreach (var (rangeStart, rangeEnd) in GetContiguousRanges(missingDates))
            {
                var fetchFrom = new DateTimeOffset(rangeStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var fetchTo = new DateTimeOffset(rangeEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

                var fetched = await FetchAllPagesAsync<ConsumptionInterval>(
                    BuildUrl(apiPath, fetchFrom, fetchTo, "page_size=25000&order_by=period"), ct);

                db.CachedIntervals.AddRange(fetched.Select(i => new CachedInterval
                {
                    EnergyType = energyType,
                    IntervalStartUtc = i.IntervalStart.UtcDateTime,
                    IntervalEndUtc = i.IntervalEnd.UtcDateTime,
                    Consumption = i.Consumption
                }));

                var daysToMark = Enumerable.Range(0, rangeEnd.DayNumber - rangeStart.DayNumber + 1)
                    .Select(i => rangeStart.AddDays(i));
                db.CachedDays.AddRange(daysToMark.Select(d => new CachedDay
                {
                    EnergyType = energyType,
                    Date = d
                }));

                await db.SaveChangesAsync(ct);
            }
        }

        var histTo = new DateTimeOffset(histToDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var fromUtc = from.UtcDateTime;
        var histToUtc = histTo.UtcDateTime;

        var intervals = fromDate < histToDate
            ? await db.CachedIntervals
                .Where(i => i.EnergyType == energyType && i.IntervalStartUtc >= fromUtc && i.IntervalStartUtc < histToUtc)
                .Select(i => new ConsumptionInterval(i.Consumption,
                    new DateTimeOffset(i.IntervalStartUtc, TimeSpan.Zero),
                    new DateTimeOffset(i.IntervalEndUtc, TimeSpan.Zero)))
                .ToListAsync(ct)
            : [];

        if (to > histTo)
        {
            var liveFrom = from > histTo ? from : histTo;
            var live = await FetchAllPagesAsync<ConsumptionInterval>(
                BuildUrl(apiPath, liveFrom, to, "page_size=25000&order_by=period"), ct);
            intervals.AddRange(live);
        }

        return [.. intervals.OrderBy(i => i.IntervalStart)];
    }

    private static IEnumerable<(DateOnly Start, DateOnly End)> GetContiguousRanges(List<DateOnly> sortedDates)
    {
        if (sortedDates.Count == 0) yield break;

        var start = sortedDates[0];
        var end = sortedDates[0];

        for (int i = 1; i < sortedDates.Count; i++)
        {
            if (sortedDates[i] == end.AddDays(1))
                end = sortedDates[i];
            else
            {
                yield return (start, end);
                start = end = sortedDates[i];
            }
        }

        yield return (start, end);
    }

    private AccountResponse? _accountCache;

    private async Task<AccountResponse?> GetAccountAsync(CancellationToken ct)
    {
        if (_accountCache is not null) return _accountCache;
        if (string.IsNullOrWhiteSpace(_settings.AccountNumber)) return null;
        _accountCache = await httpClient.GetFromJsonAsync<AccountResponse>(
            $"accounts/{_settings.AccountNumber}/", ct);
        return _accountCache;
    }

    private async Task<string?> GetTariffCodeAsync(string mpanOrMprn, bool isElectricity, DateTimeOffset from, CancellationToken ct)
    {
        try
        {
            var account = await GetAccountAsync(ct);
            if (account is null) return null;

            var meterPoints = isElectricity
                ? account.Properties.SelectMany(p => p.ElectricityMeterPoints)
                : account.Properties.SelectMany(p => p.GasMeterPoints);

            var agreements = meterPoints
                .Where(mp => (isElectricity ? mp.Mpan : mp.Mprn) == mpanOrMprn)
                .SelectMany(mp => mp.Agreements)
                .ToList();

            return agreements
                .Where(a => (a.ValidTo is null || a.ValidTo > from))
                .OrderByDescending(a => a.ValidFrom ?? DateTimeOffset.MinValue)
                .FirstOrDefault()?.TariffCode;
        }
        catch { return null; }
    }

    private async Task<(decimal? unitCost, decimal? avgRate, decimal? standingCharge)> GetCostDataAsync(
        string? tariffCode, string tariffType, DateTimeOffset from, DateTimeOffset to,
        List<ConsumptionInterval> intervals, CancellationToken ct)
    {
        if (tariffCode is null || intervals.Count == 0) return (null, null, null);

        try
        {
            var productCode = ExtractProductCode(tariffCode);
            var ratesUrl = BuildUrl($"products/{productCode}/{tariffType}/{tariffCode}/standard-unit-rates/", from, to);
            var standingUrl = BuildUrl($"products/{productCode}/{tariffType}/{tariffCode}/standing-charges/", from, to);

            var ratesTask = FetchAllPagesAsync<TariffRate>(ratesUrl, ct);
            var standingTask = FetchAllPagesAsync<TariffRate>(standingUrl, ct);
            await Task.WhenAll(ratesTask, standingTask);

            var rates = ratesTask.Result;
            var standings = standingTask.Result;

            if (rates.Count == 0) return (null, null, null);

            decimal totalCost = intervals.Sum(interval =>
            {
                var rate = rates
                    .Where(r => (r.ValidFrom is null || r.ValidFrom <= interval.IntervalStart)
                             && (r.ValidTo is null || r.ValidTo > interval.IntervalStart))
                    .OrderByDescending(r => r.ValidFrom)
                    .FirstOrDefault();
                return rate is null ? 0m : interval.Consumption * rate.ValueIncVat;
            });

            decimal avgRate = intervals.Sum(i => i.Consumption) is > 0 and var tot
                ? totalCost / tot : rates.Average(r => r.ValueIncVat);

            decimal? dailyStanding = standings.Count > 0
                ? standings.Average(s => s.ValueIncVat)
                : null;

            return (totalCost, avgRate, dailyStanding);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CostData] Error: {ex.Message}");
            return (null, null, null);
        }
    }

    private static string ExtractProductCode(string tariffCode)
    {
        var parts = tariffCode.Split('-');
        return string.Join("-", parts[2..^1]);
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
