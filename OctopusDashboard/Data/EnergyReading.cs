namespace OctopusDashboard.Data;

public class EnergyReading
{
    public int Id { get; set; }
    public required string EnergyType { get; set; }  // "electricity" | "gas"
    public DateTimeOffset IntervalStart { get; set; }
    public DateTimeOffset IntervalEnd { get; set; }
    public decimal Consumption { get; set; }
}
