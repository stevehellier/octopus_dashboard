namespace OctopusDashboard.Data;

public class CachedDay
{
    public int Id { get; set; }
    public required string EnergyType { get; set; }
    public DateOnly Date { get; set; }
}
