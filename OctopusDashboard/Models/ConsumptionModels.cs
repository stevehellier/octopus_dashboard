using System.Text.Json.Serialization;

namespace OctopusDashboard.Models;

public record PagedResponse<T>(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("next")] string? Next,
    [property: JsonPropertyName("previous")] string? Previous,
    [property: JsonPropertyName("results")] List<T> Results);

public record ConsumptionInterval(
    [property: JsonPropertyName("consumption")] decimal Consumption,
    [property: JsonPropertyName("interval_start")] DateTimeOffset IntervalStart,
    [property: JsonPropertyName("interval_end")] DateTimeOffset IntervalEnd);

public record MeterPointAgreement(
    [property: JsonPropertyName("tariff_code")] string TariffCode,
    [property: JsonPropertyName("valid_from")] DateTimeOffset? ValidFrom,
    [property: JsonPropertyName("valid_to")] DateTimeOffset? ValidTo);

public record MeterPointResponse(
    [property: JsonPropertyName("agreements")] List<MeterPointAgreement> Agreements);

public record AccountMeterPoint(
    [property: JsonPropertyName("mpan")] string? Mpan,
    [property: JsonPropertyName("mprn")] string? Mprn,
    [property: JsonPropertyName("agreements")] List<MeterPointAgreement> Agreements);

public record AccountProperty(
    [property: JsonPropertyName("electricity_meter_points")] List<AccountMeterPoint> ElectricityMeterPoints,
    [property: JsonPropertyName("gas_meter_points")] List<AccountMeterPoint> GasMeterPoints);

public record AccountResponse(
    [property: JsonPropertyName("properties")] List<AccountProperty> Properties);

public record TariffRate(
    [property: JsonPropertyName("value_inc_vat")] decimal ValueIncVat,
    [property: JsonPropertyName("valid_from")] DateTimeOffset? ValidFrom,
    [property: JsonPropertyName("valid_to")] DateTimeOffset? ValidTo);

public record ConsumptionSummary(
    List<ConsumptionInterval> Intervals,
    decimal TotalConsumption,
    string Unit,
    decimal? TotalUnitCostPence = null,
    decimal? AvgUnitRatePence = null,
    decimal? DailyStandingChargePence = null)
{
    public int Days => Intervals.Count == 0
        ? 0
        : (int)Math.Ceiling((Intervals.Max(i => i.IntervalEnd) - Intervals.Min(i => i.IntervalStart)).TotalDays);
};
