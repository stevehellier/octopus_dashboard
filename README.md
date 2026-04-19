# Octopus Dashboard

A self-hosted web dashboard for visualising your Octopus Energy electricity and gas usage. Browse consumption by day, week, or month, compare periods, and optionally display cost breakdowns.

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An Octopus Energy account with API access

---

## Setup

### 1. Get your API credentials

Log in to your [Octopus Energy developer dashboard](https://octopus.energy/dashboard/developer/) and note the following:

- **API key** — shown at the top of the developer page (starts with `sk_live_`)
- **MPAN** — your electricity meter point reference number (13 digits)
- **Electricity meter serial** — shown under your electricity meter point
- **MPRN** — your gas meter point reference number
- **Gas meter serial** — shown under your gas meter point
- **Account number** — shown at the top of your account page (format `A-XXXXXXXX`) — needed for cost data
- **Tariff codes** — found under **Agreements** on the developer page (optional, needed for cost data)

### 2. Clone the repository

```bash
git clone https://github.com/stevehellier/octopus_dashboard.git
cd octopus_dashboard
```

### 3. Configure credentials

Use .NET User Secrets to store your credentials securely (they are never committed to source control):

```bash
cd OctopusDashboard
dotnet user-secrets set "Octopus:ApiKey"                 "sk_live_..."
dotnet user-secrets set "Octopus:Mpan"                   "1234567890123"
dotnet user-secrets set "Octopus:ElectricityMeterSerial" "AB1234567"
dotnet user-secrets set "Octopus:Mprn"                   "1234567890"
dotnet user-secrets set "Octopus:GasMeterSerial"         "G4P12345678901"
```

The five values above are required. To also display costs, add:

```bash
dotnet user-secrets set "Octopus:AccountNumber"          "A-XXXXXXXX"
dotnet user-secrets set "Octopus:ElectricityTariffCode"  "E-1R-VAR-22-11-01-A"
dotnet user-secrets set "Octopus:GasTariffCode"          "G-1R-VAR-22-11-01-A"
```

### 4. Run the app

```bash
dotnet run --project OctopusDashboard
```

Open your browser at **http://localhost:5271**.

---

## Using the Dashboard

### Home page

Shows a 30-day summary for electricity and gas — total consumption and, if costs are configured, an estimated bill. Click **View detail** on either card to drill into the full data for that fuel.

### Electricity and Gas pages

Both detail pages share the same controls:

| Control | Description |
|---|---|
| **From / To** | Set the date range. Defaults to the last 30 days. |
| **Group by** | Aggregate readings into Half hour, Hour, Day, Week, or Month. |
| **Show previous period** | Overlays the equivalent preceding period as a dashed line on the chart, making it easy to spot trends. |
| **Show costs** | Displays unit cost, standing charge, and total bill cards (requires tariff codes to be configured). |

#### Summary cards

- **Total consumption** — kWh (electricity) or m³ with approximate kWh equivalent (gas)
- **Unit cost** — total spend on units, with average rate per kWh
- **Standing charge** — daily rate × number of days in the selected period
- **Total bill** — unit cost + standing charge combined

Each card shows a delta badge comparing the current period to the previous one (green = improvement, red = worse).

#### Chart

Bars show consumption for each period in the selected grouping. If **Show previous period** is checked, the prior period is overlaid as a grey dashed line using the same x-axis positions, so you can see at a glance whether usage is higher or lower than before.

---

## Caching

Historical data is cached in a local SQLite database (`OctopusDashboard/octopus_cache.db`) on first load. Subsequent requests for the same date ranges are served instantly from the database without hitting the Octopus API. Only today's data is always fetched live.

The cache file is created automatically — no setup required.

---

## REST API

The app also exposes a small REST API for scripting or integration with other tools:

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/config/status` | Returns `{ "configured": true }` if credentials are set |
| GET | `/api/electricity/consumption` | Electricity data for a date range |
| GET | `/api/gas/consumption` | Gas data for a date range |

**Example:**

```
GET /api/electricity/consumption?from=2026-03-01T00:00:00Z&to=2026-04-01T00:00:00Z&groupBy=day
```

---

## Development

```bash
# Auto-restart on file changes
dotnet watch --project OctopusDashboard

# Build only
dotnet build
```

For full technical details see [docs/technical.md](docs/technical.md).
