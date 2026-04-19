# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run the app
dotnet run --project OctopusDashboard

# Build
dotnet build

# Watch mode (auto-restart on change)
dotnet watch --project OctopusDashboard
```

No test project exists yet; add one with `dotnet new xunit -n OctopusDashboard.Tests`.

## Configuration

The app requires Octopus Energy API credentials. Set them via user secrets (preferred for development):

```bash
cd OctopusDashboard
dotnet user-secrets set "Octopus:ApiKey" "sk_live_..."
dotnet user-secrets set "Octopus:Mpan" "1234567890123"
dotnet user-secrets set "Octopus:ElectricityMeterSerial" "AB1234567"
dotnet user-secrets set "Octopus:Mprn" "1234567890"
dotnet user-secrets set "Octopus:GasMeterSerial" "G4P12345678901"
```

Alternatively, populate `OctopusDashboard/appsettings.json` directly (never commit secrets there). All five values must be non-empty for the app to consider itself configured (`IOctopusService.IsConfigured`).

## Architecture

Single ASP.NET Core + Blazor Server project targeting .NET 10.

**Request flow:**
- Blazor pages (`Components/Pages/`) run server-side with `@rendermode InteractiveServer`. They inject `IOctopusService` directly — no HTTP round-trip to self.
- REST API endpoints (`/api/electricity/consumption`, `/api/gas/consumption`, `/api/config/status`) are mapped in `Endpoints/OctopusEndpoints.cs` and expose the same service for external consumers.

**Key layers:**

| Layer | Location | Role |
|---|---|---|
| Settings | `Models/OctopusSettings.cs` | Bound from `Octopus` config section via `IOptions<T>` |
| API models | `Models/ConsumptionModels.cs` | JSON records matching Octopus REST response shape |
| Service | `Services/OctopusService.cs` | HTTP Basic auth, pagination, aggregation |
| API endpoints | `Endpoints/OctopusEndpoints.cs` | Minimal API group at `/api` |
| Pages | `Components/Pages/` | Home (30-day summary), Electricity, Gas |
| Chart interop | `wwwroot/js/charts.js` | `window.octopusCharts.render()` wraps Chart.js (CDN) |

**Octopus API basics:**
- Base URL: `https://api.octopus.energy/v1/`
- Auth: HTTP Basic with API key as username, empty password
- Electricity: `electricity-meter-points/{mpan}/meters/{serial}/consumption/`
- Gas: `gas-meter-points/{mprn}/meters/{serial}/consumption/`
- Query params: `period_from`, `period_to`, `group_by` (`half_hour`|`hour`|`day`|`week`|`month`), `page_size`, `order_by`
- Gas readings are in **m³**; the UI shows an approximate kWh conversion (×11.1).

**Chart rendering pattern:** Pages set `_chartPending = true` after data loads, then `OnAfterRenderAsync` calls `JS.InvokeVoidAsync("octopusCharts.render", ...)` once the canvas is in the DOM. Chart.js is loaded from CDN in `Components/App.razor`.
