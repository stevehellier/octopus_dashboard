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

public record ConsumptionSummary(
    List<ConsumptionInterval> Intervals,
    decimal TotalConsumption,
    string Unit);
