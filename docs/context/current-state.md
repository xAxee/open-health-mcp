# Current state

Active branch: `feature/fitness-ai-connector-parity`.

OpenHealthMCP is a single-user ASP.NET Core 10 MCP server backed by PostgreSQL and EF Core. Garmin Connect is integrated through `Unofficial.Garmin.Connect` 0.10.0. Existing synchronization covers daily summary, HR, stress/Body Battery timelines, sleep aggregates, HRV average, activities, laps, Garmin HR zones, and bounded activity streams.

The Fitness AI parity audit is recorded in `docs/fitness-ai-gap-analysis.md`. Canonical indexed daily/activity sample tables and revisioned raw payload storage are implemented. Daily synchronization now includes confirmed Garmin distance, active time, calories, goals, stress distribution, Body Battery charge/drain, extended HRV, sleep timestamps/subscores/stages/respiration, and dedicated SpO2/respiration responses. `get_day` retains all previous fields and adds structured sections with source metadata.

Canonical `get_day_series` and `get_activity_series` MCP tools are implemented alongside compatibility tools. They query indexed canonical tables, support range/field selection, interval aggregation, deterministic response-only downsampling, and explicit source/resolution metadata.

Current validation: production build passes; 24 standard xUnit tests pass. The active series integration test also passes on PostgreSQL 17. Both new migrations were applied to PostgreSQL 17, including an upgrade test that retained historical daily and raw records.
