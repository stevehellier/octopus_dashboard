using OctopusDashboard.Services;

namespace OctopusDashboard.Endpoints;

public static class OctopusEndpoints
{
    public static void MapOctopusEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/config/status", (IOctopusService svc) =>
            Results.Ok(new { configured = svc.IsConfigured }));

        api.MapGet("/electricity/consumption", async (
            DateTimeOffset from, DateTimeOffset to, string groupBy,
            IOctopusService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetElectricityConsumptionAsync(from, to, groupBy, ct);
            return Results.Ok(summary);
        });

        api.MapGet("/gas/consumption", async (
            DateTimeOffset from, DateTimeOffset to, string groupBy,
            IOctopusService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetGasConsumptionAsync(from, to, groupBy, ct);
            return Results.Ok(summary);
        });
    }
}
