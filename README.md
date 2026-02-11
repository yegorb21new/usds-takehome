# Yegor Biryukov USDS Take Home Test
Date: 2/5/2025

**1. Summary**

This submission implements an ASP.NET Core (.NET 8) Razor Pages application that downloads Federal Regulations data, stores it server-side in SQLite, computes meaningful metrics per agency, exposes APIs to retrieve stored results, and provides a UI for analysis and review.


**2. What the application does**

Ingestion
* Current eCFR snapshot: downloads GovInfo bulk XML for titles (1–50) and computes per-agency metrics.
* Annual CFR snapshots: downloads GovInfo CFR Annual bulk XML for a specified year and computes per-agency metrics for historical comparisons over time.
* Metrics (per agency, per snapshot)
* Word Count: approximate size/volume of regulatory text.
* SHA-256 checksum: stable fingerprint of normalized text to detect change.
* Custom metric - Obligation Intensity (per 10k words): density of prescriptive language (such as: “shall”, “must”, “may not”, “prohibited”), intended as a proxy for regulatory “mandatoriness.”

Insights
* Top changes endpoint ranks agencies by largest increases/decreases in Word Count and Obligation Intensity between the latest two snapshots.

UI
* Dashboard: sortable list (largest word counts first), search filter, and Top Changes panel.
* Agency detail page: per agency time series table across snapshots.


**3. How to run locally**

Prereqs:
* .NET SDK 8.x
* Visual Studio 2022 or dotnet CLI

Commands:
In the solution (.sln file) directory, run the following PowerShell commands:

```powershell
dotnet tool install --global dotnet-ef

dotnet restore
dotnet build

dotnet ef database update --project USDSTakeHomeTest --startup-project USDSTakeHomeTest
dotnet run --project USDSTakeHomeTest
```

PowerShell should say its now listening on some `<port>`. In a browser, open: 
`http://localhost:<port>/Dashboard`


**4. API endpoints**

```GET /api/agencies — agencies with latest snapshot metrics (sorted by word count desc)
GET /api/agencies/{agencyId}/metrics — time series metrics across snapshots
POST /api/ingest/current?fromTitle=1&toTitle=50 — ingest current eCFR
POST /api/ingest/annual?year=YYYY&fromTitle=1&toTitle=50 — ingest CFR Annual for year
GET /api/insights/top-changes?top=10 — top movers between latest two snapshots

Ingestion endpoints:
Current: POST /api/ingest/current?fromTitle=1&toTitle=50
Annual: POST /api/ingest/annual?year=2024&fromTitle=1&toTitle=50
```


**5. Link to frontend Local Razor Pages UI:**
`http://localhost:<port>/Dashboard`

