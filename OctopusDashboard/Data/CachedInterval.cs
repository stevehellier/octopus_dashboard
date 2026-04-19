namespace OctopusDashboard.Data;

public class CachedInterval
{
    public int Id { get; set; }
    public required string EnergyType { get; set; }
    public DateTime IntervalStartUtc { get; set; }
    public DateTime IntervalEndUtc { get; set; }
    public decimal Consumption { get; set; }
}
