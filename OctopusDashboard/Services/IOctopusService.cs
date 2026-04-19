using OctopusDashboard.Models;

namespace OctopusDashboard.Services;

public interface IOctopusService
{
    bool IsConfigured { get; }
    Task<ConsumptionSummary> GetElectricityConsumptionAsync(DateTimeOffset from, DateTimeOffset to, string groupBy, CancellationToken cancellationToken = default);
    Task<ConsumptionSummary> GetGasConsumptionAsync(DateTimeOffset from, DateTimeOffset to, string groupBy, CancellationToken cancellationToken = default);
}
