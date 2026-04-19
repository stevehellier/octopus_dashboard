# Octopus Dashboard — Technical Documentation

## Overview

Octopus Dashboard is a self-hosted ASP.NET Core 10 Blazor Server application that fetches electricity and gas consumption data from the Octopus Energy REST API, caches historical readings in a local SQLite database, and renders interactive charts and cost summaries.

---

## Technology Stack

| Component | Technology |
|---|---|
| Framework | ASP.NET Core 10, Blazor Server |
| UI rendering | Razor Components (`@rendermode InteractiveServer`) |
| Charting | Chart.js (CDN) via JS interop |
| Database | SQLite via EF Core 10 |
| HTTP client | `HttpClient` with Basic auth |
| Configuration | .NET User Secrets / `appsettings.json` |
| Target runtime | .NET 10 |

---

## Project Structure

```
OctopusDashboard/
├── Components/
│   ├── App.razor                  # Root component; loads Chart.js CDN
│   ├── _Imports.razor             # Global Razor namespace imports
│   ├── Layout/
│   │   ├── MainLayout.razor       # Shell layout with sidebar nav
│   │   └── NavMenu.razor          # Navigation links
│   └── Pages/
│       ├── Home.razor             # 30-day summary for both fuels
│       ├── Electricity.razor      # Electricity detail page
│       └── Gas.razor              # Gas detail page
├── Data/
│   ├── CachedInterval.cs          # EF Core entity — half-hour readings
│   ├── CachedDay.cs               # EF Core entity — per-day cache index
│   └── OctopusDbContext.cs        # EF Core DbContext
├── Endpoints/
│   └── OctopusEndpoints.cs        # Minimal API REST endpoints
├── Models/
│   ├── ConsumptionModels.cs       # API response records + ConsumptionSummary
│   └── OctopusSettings.cs         # Configuration POCO
├── Services/
│   ├── IOctopusService.cs         # Service interface
│   ├── OctopusService.cs          # HTTP + cache implementation
│   └── AppState.cs                # Scoped UI state (ShowCosts toggle)
├── wwwroot/
│   └── js/charts.js               # Chart.js wrapper (window.octopusCharts)
├── Program.cs                     # DI registration, middleware, startup
└── OctopusDashboard.csproj
```

---

## Configuration

All credentials are stored outside source control using .NET User Secrets.

```bash
cd OctopusDashboard
dotnet user-secrets set "Octopus:ApiKey"                "sk_live_..."
dotnet user-secrets set "Octopus:Mpan"                  "2200014344475"
dotnet user-secrets set "Octopus:ElectricityMeterSerial" "22L4356557"
dotnet user-secrets set "Octopus:Mprn"                  "4256454305"
dotnet user-secrets set "Octopus:GasMeterSerial"        "G4W02491710101"
dotnet user-secrets set "Octopus:AccountNumber"         "A-XXXXXXXX"
dotnet user-secrets set "Octopus:ElectricityTariffCode" "E-1R-VAR-22-11-01-A"
dotnet user-secrets set "Octopus:GasTariffCode"         "G-1R-VAR-22-11-01-A"
```

The first five keys are required — `IsConfigured` returns `false` if any are missing and the UI shows a setup notice. `AccountNumber` and the tariff codes are optional but needed to display cost data.

**Settings class:** `Models/OctopusSettings.cs` — bound from the `Octopus` config section via `IOptions<OctopusSettings>`.

---

## Octopus Energy API

- **Base URL:** `https://api.octopus.energy/v1/`
- **Authentication:** HTTP Basic — API key as username, empty password
- **Pagination:** Responses include a `next` URL; `FetchAllPagesAsync<T>` follows all pages automatically

### Endpoints used

| Purpose | Path |
|---|---|
| Electricity consumption | `electricity-meter-points/{mpan}/meters/{serial}/consumption/` |
| Gas consumption | `gas-meter-points/{mprn}/meters/{serial}/consumption/` |
| Account + tariff agreements | `accounts/{account_number}/` |
| Unit rates | `products/{product}/electricity-tariffs/{tariff}/standard-unit-rates/` |
| Standing charges | `products/{product}/electricity-tariffs/{tariff}/standing-charges/` |

### Query parameters

| Parameter | Values |
|---|---|
| `period_from` | ISO 8601 UTC datetime |
| `period_to` | ISO 8601 UTC datetime |
| `group_by` | `half_hour`, `hour`, `day`, `week`, `month` |
| `page_size` | Integer (max 25000 used) |
| `order_by` | `period` (ascending) |

### Tariff code format

Tariff codes follow the pattern `E-1R-VAR-22-11-01-A`. The product code is extracted by splitting on `-` and dropping the first two and last one segments: `VAR-22-11-01`. This is used to build the rates API path.

---

## Data Flow

```
User navigates to page
        │
        ▼
Blazor page calls IOctopusService
        │
        ▼
OctopusService.GetCachedRawIntervalsAsync()
        │
        ├─► Check CachedDays in SQLite for the requested date range
        │
        ├─► For any missing historical days:
        │       └─► Fetch half-hour intervals from Octopus API
        │           └─► Write intervals + day markers to SQLite
        │
        ├─► Read historical intervals from SQLite
        │
        └─► Fetch today's data live from Octopus API (never cached)
                │
                ▼
        GroupIntervals() — aggregate half-hours into chosen period
                │
                ▼
        GetCostDataAsync() — fetch tariff rates, calculate unit cost
                │
                ▼
        ConsumptionSummary returned to Blazor page
                │
                ▼
        OnAfterRenderAsync → JS.InvokeVoidAsync("octopusCharts.render", ...)
```

---

## Caching Layer

**Database file:** `octopus_cache.db` (SQLite, created automatically in the working directory on first run)

### Schema

**`CachedIntervals`**

| Column | Type | Description |
|---|---|---|
| `Id` | INTEGER PK | Auto-increment |
| `EnergyType` | TEXT | `"electricity"` or `"gas"` |
| `IntervalStartUtc` | TEXT | UTC datetime of interval start |
| `IntervalEndUtc` | TEXT | UTC datetime of interval end |
| `Consumption` | TEXT | Reading value (kWh for electricity, m³ for gas) |

Index on `(EnergyType, IntervalStartUtc)`.

**`CachedDays`**

| Column | Type | Description |
|---|---|---|
| `Id` | INTEGER PK | Auto-increment |
| `EnergyType` | TEXT | `"electricity"` or `"gas"` |
| `Date` | TEXT | UTC date (ISO 8601) |

Unique index on `(EnergyType, Date)`.

### Cache strategy

1. On each request, determine the **historical range**: `[from, min(to, today UTC))`.
2. Query `CachedDays` to find which days in that range are already stored.
3. Group missing days into contiguous ranges to minimise API calls.
4. Fetch each contiguous missing range from the Octopus API and write the intervals + day markers to the DB.
5. Read all historical intervals for the full range from `CachedIntervals`.
6. For **today onwards**, always fetch live from the API (data still accumulating).
7. Merge and sort, then group into the requested period granularity.

Historical data is considered immutable — cached days are never invalidated or re-fetched.

---

## Service Layer

### `IOctopusService`

```csharp
bool IsConfigured { get; }
Task<ConsumptionSummary> GetElectricityConsumptionAsync(DateTimeOffset from, DateTimeOffset to, string groupBy, CancellationToken ct = default);
Task<ConsumptionSummary> GetGasConsumptionAsync(DateTimeOffset from, DateTimeOffset to, string groupBy, CancellationToken ct = default);
```

Registered as a typed `HttpClient` (transient lifetime) in `Program.cs`.

### `AppState`

Scoped service that holds the `ShowCosts` boolean. Exposes an `OnChange` event; pages subscribe via `OnInitialized` / `Dispose` to trigger `StateHasChanged` when the toggle changes. The `ShowCosts` setter fires `OnChange` automatically, making `@bind="AppState.ShowCosts"` work directly in Razor.

---

## REST API Endpoints

Exposed at `/api` for external consumers (e.g. scripting, Home Assistant).

| Method | Path | Description |
|---|---|---|
| GET | `/api/config/status` | Returns `{ "configured": true/false }` |
| GET | `/api/electricity/consumption` | Electricity summary for date range |
| GET | `/api/gas/consumption` | Gas summary for date range |

Query parameters for consumption endpoints: `from`, `to` (DateTimeOffset), `groupBy` (string).

---

## UI Pages

### Home (`/`)

Shows a 30-day summary card for each fuel. Displays total consumption and, if `ShowCosts` is on and tariff data is available, the estimated total bill. Loads electricity and gas in parallel.

### Electricity (`/electricity`) and Gas (`/gas`)

Full detail pages with:
- Date range pickers (`From` / `To`) — default to last 30 days
- Group by selector (`Half hour` / `Hour` / `Day` / `Week` / `Month`)
- **Show previous period** checkbox — overlays the preceding equivalent period as a dashed line on the chart
- **Show costs** checkbox — shows unit cost, standing charge, and total bill cards
- Summary stat cards (total consumption, unit cost, standing charge, total bill)
- Delta badges comparing current vs previous period (▲ red = worse, ▼ green = better)
- Chart.js bar chart with optional previous-period line overlay

### Chart rendering pattern

Pages set `_chartPending = true` after data loads. `OnAfterRenderAsync` detects this flag and calls `JS.InvokeVoidAsync("octopusCharts.render", ...)` once the `<canvas>` element is in the DOM. Chart.js is loaded from CDN before `blazor.web.js` in `App.razor` to ensure it is available when Blazor first renders.

---

## Gas Unit Conversion

The Octopus API returns gas consumption in **m³**. The UI converts to approximate kWh using a fixed factor of **11.1 kWh/m³** (standard calorific value). Actual conversion varies by gas calorific value and is shown as an approximation. Cost calculations in the service use the kWh-denominated tariff rates applied to the m³ readings converted by the same factor implicitly through the API rate structure.

---

## Running the App

```bash
# Development
dotnet run --project OctopusDashboard

# Watch mode (auto-restart on file changes)
dotnet watch --project OctopusDashboard

# Build only
dotnet build
```

The app runs on `http://localhost:5271` by default (configured in `Properties/launchSettings.json`). The SQLite cache database (`octopus_cache.db`) is created automatically on first startup in the working directory.

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.6 | SQLite ORM and cache persistence |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.6 | EF Core tooling support |
| Chart.js | CDN (latest) | Interactive bar/line charts |
| Bootstrap | Bundled with Blazor template | UI layout and components |
