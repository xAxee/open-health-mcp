# Fitness AI parity gap analysis

Date: 2026-09-02  
Audited revision: `58df0d5`  
Provider: Garmin Connect through `Unofficial.Garmin.Connect` 0.10.0

## Status legend

- `AVAILABLE` — normalized, persisted, and exposed by MCP.
- `PARTIAL` — some fields or semantics are missing.
- `MISSING` — not implemented although the architecture can support it.
- `PROVIDER_NOT_AVAILABLE` — no provider source has been confirmed; the value must not be fabricated.
- `DERIVED` — computed deterministically by OpenHealth, never presented as provider data.
- `TODO` — provider availability or payload shape must still be confirmed with captured data.

## Audited architecture

OpenHealthMCP is one ASP.NET Core 10 application using EF Core and PostgreSQL. Garmin integration is isolated under `Providers/Garmin`; the normalized entities do not depend on Garmin model types. The current flow is:

```text
Garmin Connect response
  -> response-body capture
  -> raw JSON parser
  -> normalized EF Core entity / JSONB timeline
  -> PostgreSQL
  -> read-only MCP tool
```

This is the correct direction and must be retained. The current implementation is single-user, identifies normalized records by `Source` plus provider ID/date, applies migrations at startup, and runs a scheduled synchronization hosted service.

### Confirmed Garmin sources currently used

| Data | Client method | Captured request path | Raw type |
| --- | --- | --- | --- |
| Daily summary | `GetUserSummary` | `/usersummary-service/usersummary/daily/` | `daily` |
| Daily heart rate | `GetWellnessHeartRates` | `/wellness-service/wellness/dailyHeartRate/` | `heart_rate` |
| Stress and Body Battery | `GetAllDayStress` | `/wellness-service/wellness/dailyStress/` | `daily_timeline` |
| Sleep | `GetWellnessSleepData` | `/wellness-service/wellness/dailySleepData/` | `sleep` |
| HRV summary | `GetReportHrvStatus` | `/hrv-service/hrv/daily/` | `hrv` |
| Activity list and summary | `GetActivitiesByDate` | `/activitylist-service/activities/search/activities` | `activity` |
| Activity stream | `GetActivityDetails` | `/activity-service/activity/{id}/details` | `activity_details` |
| Laps | `GetActivitySplits` | `/activity-service/activity/{id}/splits` | `activity_splits` |
| HR zones | `GetActivityHrInTimezones` | `/activity-service/activity/{id}/hrTimeInZones` | activity HR-zone raw record |

Source inspection at the exact NuGet 0.10.0 commit (`e8e811c`) confirms client methods for FIT download, body composition/weight, blood pressure, fitness age, configured HR zones, user settings, SpO2, respiration, and a dedicated Body Battery report. Their implementation status remains `MISSING` until OpenHealth captures a real response, stores it raw, and normalizes it with sanitized fixture coverage.

## Daily wellness

| Feature | Status | Current source and semantics | Gap / action |
| --- | --- | --- | --- |
| Provider calendar date | `PARTIAL` | Request date is persisted as `DateOnly` | Store provider local date plus timezone/offset; do not equate it with UTC date. |
| Steps | `AVAILABLE` | Garmin daily `totalSteps` | Preserve. |
| Distance | `MISSING` | Not normalized | Confirm daily payload key and add meters. |
| Active time | `MISSING` | Not normalized | Confirm provider payload and add seconds. |
| Resting HR | `AVAILABLE` | Garmin daily/heart-rate response | Preserve provider value. |
| Average HR | `DERIVED` | Computed from measured daily HR samples | Mark as derived-from-measured-series. |
| Min/max HR | `AVAILABLE` | Garmin daily heart-rate response | Preserve. |
| Total calories | `AVAILABLE` | Garmin `totalKilocalories` | Preserve as provider value. |
| Active calories | `AVAILABLE` | Garmin `activeKilocalories` | Preserve as provider value. |
| BMR calories | `MISSING` | Not normalized | Confirm payload; deterministic total derivation must carry source metadata. |
| Floors climbed/goal | `MISSING` | Not normalized | Confirm daily response keys. |
| Moderate/vigorous intensity | `AVAILABLE` | Garmin daily values | Preserve. |
| Total intensity minutes | `MISSING` | Not exposed | Derive only with explicit Garmin vigorous double-count rule metadata. |
| Steps/intensity goals | `MISSING` | Not normalized | Confirm provider fields. |

## Stress and Body Battery

| Feature | Status | Current source and semantics | Gap / action |
| --- | --- | --- | --- |
| Stress average | `AVAILABLE` | Garmin `averageStressLevel` | Preserve. |
| Stress maximum | `AVAILABLE` | Garmin daily `maxStressLevel` | Provider value. |
| Stress qualifier | `AVAILABLE` | Garmin daily `stressQualifier` | Provider category; no local replacement. |
| Rest/low/medium/high/activity durations | `AVAILABLE` | Garmin daily duration fields | Provider values in seconds. |
| Stress percentages | `AVAILABLE` | Garmin daily percentage fields | Provider values; OpenHealth does not recalculate them. |
| Stress series | `AVAILABLE` | Garmin daily-stress descriptor arrays | Persisted without an artificial local retention cutoff. |
| Body Battery min/max | `AVAILABLE` | Garmin daily summary | Preserve. |
| Body Battery charged/drained/high/low | `PARTIAL` | Charged, drained, high, and low are normalized from Garmin daily values | Provider high/low remain compatible min/max fields. |
| Body Battery series | `AVAILABLE` | Garmin daily-stress descriptor arrays | Persisted as JSONB and indexed canonical samples without retention deletion. |

## HRV

| Feature | Status | Current source and semantics | Gap / action |
| --- | --- | --- | --- |
| Last-night average | `AVAILABLE` | Garmin HRV `lastNightAvg`, compatible field `Hrv` plus explicit details | Provider value. |
| Five-minute high | `AVAILABLE` | Garmin HRV `lastNight5MinHigh` | Provider value. |
| Minimum/maximum | `MISSING` | No measured HRV series persisted | Derive only from actual samples and label accordingly. |
| Reading count/duration | `MISSING` | Not stored | Add sample count and measurement duration. |
| Sleep/start timestamp | `MISSING` | Not stored | Parse UTC/local basis from confirmed payload. |
| HRV series | `MISSING` | Only raw summary response exists | Add measured points without interpolation. |

## Sleep

| Feature | Status | Current source and semantics | Gap / action |
| --- | --- | --- | --- |
| Start/end UTC and local | `AVAILABLE` | Garmin sleep GMT/local timestamps | UTC, local wall clock, and derived offset are kept distinct. |
| Duration | `AVAILABLE` | Garmin `dailySleepDTO.sleepTimeSeconds` | Preserve. |
| Deep/light/REM/awake | `AVAILABLE` | Garmin `dailySleepDTO` | Preserve. |
| Unmeasurable stage duration | `AVAILABLE` | Garmin sleep payload | Provider value. |
| Sleep score | `AVAILABLE` | Garmin `sleepScores.overall.value` | Preserve. |
| Overall qualifier | `MISSING` | Not normalized | Store provider qualifier. |
| Extensible subscores | `MISSING` | Raw sleep response retained | Add normalized common fields plus JSONB subscore payload. |
| Sleep-stage series | `PARTIAL` | Exact Garmin intervals and numeric provider stage codes are stored | Text labels await a confirmed mapping. |
| Naps | `AVAILABLE` | Garmin `napTimeSeconds` | Aggregate duration is normalized; separate nap intervals are not confirmed. |

## SpO2 and respiration

| Feature | Status | Current source and semantics | Gap / action |
| --- | --- | --- | --- |
| Average SpO2 | `AVAILABLE` | Garmin daily `averageSpo2` | Preserve. |
| SpO2 min/max/count/window | `PARTIAL` | Minimum and measurement window are normalized | Confirmed response has no maximum/count or sample array. |
| SpO2 series | `MISSING` | Not persisted | Preserve raw samples without interpolation. |
| Average waking respiration | `AVAILABLE` | Garmin `avgWakingRespirationValue` | Preserve. |
| Sleep respiration/min/max | `AVAILABLE` | Garmin sleep and respiration responses | Provider values. |
| Respiration series | `AVAILABLE` | Garmin daily respiration and sleep epoch arrays | Measured points only; no interpolation. |

## Activities

| Feature | Status | Current source and semantics | Gap / action |
| --- | --- | --- | --- |
| Activity list | `AVAILABLE` | Garmin activity search | Preserve broad activity type strings. |
| Pagination | `PARTIAL` | `limit` 1..200 only | Add bounded offset/cursor; history remains unrestricted by plans. |
| Duration/elapsed/moving | `AVAILABLE` | Garmin summary | Preserve seconds. |
| Distance | `AVAILABLE` | Garmin summary | Preserve meters. |
| Elevation gain/loss | `AVAILABLE` | Garmin summary | Preserve meters. |
| Min/max elevation | `PARTIAL` | Available for laps only | Add activity-level confirmed values. |
| Average/max speed | `AVAILABLE` | Garmin summary | Preserve m/s. |
| Average pace | `DERIVED` | Deterministically derived from provider average speed | Add source/algorithm metadata; preserve compatibility field. |
| Best pace | `MISSING` | Not normalized | Add only if provider supplies it. |
| Average/max HR | `AVAILABLE` | Garmin summary | Preserve bpm. |
| Steps | `AVAILABLE` | Garmin summary | Preserve. |
| Cadence | `AVAILABLE` | Garmin running/cycling/swimming fields | Preserve explicit unit; add discipline-specific fields where present. |
| Average/max/normalized power | `AVAILABLE` | Garmin summary | Preserve watts. |
| Temperature | `PARTIAL` | Min/max only at activity level | Add average; identify as device sensor temperature, not weather. |
| Respiration | `AVAILABLE` | Garmin summary | Preserve average/min/max. |
| Training Effect | `AVAILABLE` | Garmin aerobic/anaerobic values | Provider values; preserve. |
| Training Load | `AVAILABLE` | Garmin `activityTrainingLoad` | Provider value; preserve. |
| VO2max | `AVAILABLE` | Garmin activity `vO2MaxValue` | Provider value; preserve. |
| Multisport parent/children | `MISSING` | No relationship columns | Confirm Garmin representation before implementing. |

## Activity series, laps, and HR zones

| Feature | Status | Current source and semantics | Gap / action |
| --- | --- | --- | --- |
| Activity samples | `PARTIAL` | `/details`, one JSONB document | Initial provider request is capped at 2,000 samples, so full resolution may be lost before storage. |
| HR/speed/distance/elevation/cadence/power/temperature/respiration | `AVAILABLE` | Confirmed detail descriptor keys | Preserve measured points; never interpolate. |
| Latitude/longitude/pace | `MISSING` | Not mapped | Add only confirmed descriptor keys; pace may be derived from measured speed with metadata. |
| Series source/resolution metadata | `MISSING` | Not stored | Add source, stored/effective resolution, original/returned counts, and downsampling metadata. |
| Laps | `AVAILABLE` | Garmin `/splits` | Preserve as independent provider source. |
| Lap power | `MISSING` | Entity has no power columns | Add confirmed values. |
| HR-zone number/time/percentage/low boundary | `AVAILABLE` | Garmin `/hrTimeInZones` | Keep Garmin values authoritative. Percentage is derived from Garmin durations and must be marked derived. |
| HR-zone high boundary | `MISSING` | Not inferred | May derive from next Garmin low boundary only with explicit `derived` metadata. Never use age formulas. |
| HR drift | `MISSING` | Not implemented | Add deterministic `hr-drift-v1` only for suitable activities with sufficient measured streams. |
| FIT | `MISSING` | Confirmed library path `/download-service/files/activity/{id}` | Implement binary capture/storage and parser only after validating a real download. |

## Profile and sparse measurements

| Feature | Status | Current source and semantics | Gap / action |
| --- | --- | --- | --- |
| Provider connection status | `PARTIAL` | Configuration and one provider-level sync state exist | Expose safe capabilities and status through MCP. |
| Timezone | `MISSING` | Not persisted | Required before correct daily/date semantics. |
| Garmin profile identifier | `MISSING` | Confirmed `/userprofile-service/socialProfile` | Store only a safe identifier, not names/email. |
| Running/cycling VO2max | `MISSING` | Confirmed user-settings fields | Persist provider values; never infer. |
| Fitness age/max HR/configured zones | `MISSING` | Confirmed fitness-age and biometric HR-zone methods | Preserve Garmin values and sport/training method. |
| Body composition | `MISSING` | Confirmed body-composition and weight-range methods | Persist sparse measurements without interpolation. |
| Blood pressure | `MISSING` | Confirmed daily/range methods | Persist sparse measurements without fabricated daily averages. |

## Trends, comparison, synchronization, and quality

| Feature | Status | Current source and semantics | Gap / action |
| --- | --- | --- | --- |
| Health trends | `PARTIAL` | Daily values, maximum 366-day range | Add required metrics and automatic daily/weekly/monthly buckets. |
| Activity trends | `PARTIAL` | Several `activity_*` daily aggregates | Extend without changing existing metric semantics. |
| Sparse measurements | `PARTIAL` | Null daily values are omitted | Add bucket measurement counts; never synthesize missing days. |
| Compare periods | `PARTIAL` | Average, difference, percentage, counts | Return full period statistics for all meaningful trend metrics. |
| Historical backfill | `AVAILABLE` | Authenticated `/admin/sync`, 31-day chunks | Add MCP-controlled bounded refresh and category selection. |
| Single activity refresh | `MISSING` | No public operation | Add idempotent details/laps/zones/streams/FIT refresh. |
| Sync status | `PARTIAL` | One `SyncState` row per provider | Add operation/category status and data-quality coverage. |
| Provider retry | `PARTIAL` | Per-unit failure isolation; no explicit retry/rate-limit policy | Add bounded retry with observable rate-limit/provider failures. |
| Structured logging | `PARTIAL` | Range and unit-level logs | Add endpoint, result counts, parsing/enrichment outcomes; never auth data. |
| Unified errors | `MISSING` | Raw exceptions/empty result conventions | Add stable codes such as `INVALID_RANGE`, `PROVIDER_UNAVAILABLE`, and `PARTIAL_DATA`. |

## Storage, retention, security, and tests

| Feature | Status | Current source and semantics | Gap / action |
| --- | --- | --- | --- |
| Raw response body retention | `PARTIAL` | `raw_provider_data.Payload` JSONB | Current logical row is overwritten; add hash and parser version and retain reprocessable revisions without duplicate copies. |
| Raw deduplication | `PARTIAL` | Unique source/type/external ID | Add content hash and a clear latest/revision policy. |
| Secret/header safety | `PARTIAL` | Only successful response bodies are captured; headers are not persisted | Add explicit sanitizer and tests before broadening capture metadata. |
| Daily series storage | `PARTIAL` | JSONB document unique by source/date/metric | Existing design cannot index individual timestamps; current sync deletes series older than 365 days. |
| Activity series storage | `PARTIAL` | JSONB document unique by activity | Efficient bounded read but no timestamp index and provider fetch is truncated. |
| Idempotency | `AVAILABLE` | Unique constraints and upserts | Preserve and test repeated synchronization. |
| Timezone correctness | `MISSING` | Scheduled `today` and group dates use UTC | Store provider calendar date and timezone; add UTC+ and UTC- tests. |
| Unit normalization | `PARTIAL` | Most entity names encode canonical units | Add source/unit metadata for generic samples. |
| Cache | `MISSING` | DB is the source of truth | Add only after measurement; refresh must invalidate it. |
| Automated tests | `MISSING` | Only an OAuth shell flow exists | Add unit/integration test project and sanitized fixtures. |
| Query-plan verification | `MISSING` | Not documented | Verify representative long trends and series queries after schema changes. |

## MCP contract gaps

Current tools are `get_day`, `get_activities`, `get_activity`, `get_activity_laps`, `get_activity_hr_zones`, `get_activity_streams`, `get_daily_timeline`, `get_activity_summary`, `get_trend`, and `compare_periods`.

Backward-compatible additions planned:

- `get_day_series` — multi-metric daily series with range, interval, `maxPoints`, and explicit downsampling/time-basis metadata.
- `get_activity_series` — preferred canonical name; keep `get_activity_streams` as a compatibility alias.
- `get_user_profile` — safe provider connection, timezone, confirmed fitness profile values, capabilities, and last successful sync.
- `get_body_composition` — sparse measurements.
- `get_blood_pressure` — only after provider support is confirmed.
- `refresh_data` — bounded, idempotent category-based synchronization.
- `refresh_activity` — idempotent activity enrichment retry.
- `get_sync_status` — provider/category operation status and data-quality coverage.

`get_day`, `get_activity`, trends, and period comparison will be extended without removing or reinterpreting existing fields.

## Implementation order

1. Add fixtures and a test project so parser/schema work is verifiable.
2. Version and hash raw payloads; add explicit source metadata and sanitization boundaries.
3. Add timezone-aware daily records and indexed canonical daily samples without deleting existing JSONB documents.
4. Expand daily, sleep, HRV, stress, Body Battery, SpO2, and respiration parsing from confirmed fixtures.
5. Store full-resolution activity samples and enrich activity/lap summaries while preserving Garmin HR zones and Training Effect/Load.
6. Add backward-compatible series tools with deterministic response-only downsampling.
7. Extend trends/comparison, then profile/body composition/refresh/status.
8. Evaluate FIT and remaining P2 features only after provider requests are confirmed.

## Non-negotiable source rules

- Garmin fields are reported as `garmin_api`/`garmin_connect` only when present in a confirmed response.
- FIT fields are reported as `garmin_fit` only when parsed from a validated and preserved FIT download.
- Percentages, inferred adjacent-zone boundaries, pace, HRV extrema from samples, and HR drift are `derived_by_openhealth` with an algorithm identifier.
- Missing samples are never interpolated into measurements.
- Garmin HR-zone boundaries always take precedence over any derived representation; age-based zones are prohibited.
