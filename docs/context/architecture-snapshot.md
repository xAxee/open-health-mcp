# Architecture snapshot

```text
Garmin Connect
  -> GarminClientSession + GarminRawCaptureHandler
  -> GarminProvider parsers/normalizers
  -> EF Core AppDbContext
  -> PostgreSQL normalized tables + JSONB raw/series
  -> static read-only HealthTools MCP methods
```

- One ASP.NET Core application and one PostgreSQL database.
- `HealthSyncService` is both the scheduled `BackgroundService` and the manual range-sync coordinator.
- Provider boundary: `IHealthDataProvider.SyncAsync(from, to)`.
- Existing uniqueness keys make daily, activity, enrichment, and raw upserts idempotent.
- Current series are one JSONB document per activity or source/date/metric.
- Authentication supports a static MCP token and local OAuth authorization-code flow.
- Provider types are isolated from persistence and MCP DTOs; retain this boundary.
