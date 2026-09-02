# Current state

Active branch: `feature/fitness-ai-connector-parity`.

OpenHealthMCP is a single-user ASP.NET Core 10 MCP server backed by PostgreSQL and EF Core. Garmin Connect is integrated through `Unofficial.Garmin.Connect` 0.10.0. Existing synchronization covers daily summary, HR, stress/Body Battery timelines, sleep aggregates, HRV average, activities, laps, Garmin HR zones, and bounded activity streams.

The Fitness AI parity audit is complete and recorded in `docs/fitness-ai-gap-analysis.md`. Implementation has not yet changed runtime behavior or database schema.

Baseline validation: `dotnet build OpenHealthMCP.csproj --no-restore` passes. There is no .NET test project yet; only `tests/oauth-flow.sh` exists.
