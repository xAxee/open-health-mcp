# Known issues

## Product/code

- Scheduled `today` still uses UTC; synced daily records now preserve provider calendar date, local wall-clock timestamps, and a deterministic UTC offset where Garmin supplies both time bases.
- Activity detail fetch is still capped at 2,000 samples by the current provider call, although canonical storage itself is no longer capped.
- Raw payload revisions now use SHA-256 and parser versions; legacy rows intentionally retain nullable hashes and `garmin-v0`.
- HRV and SpO2 measured series remain unavailable from the currently confirmed library responses. Sleep-stage provider codes are stored transparently but not text-labelled without a confirmed mapping.
- Body composition, profile, and blood pressure are not yet synchronized or exposed.
- Trend range is limited to 366 days and has no automatic granularity.
- `get_activities` has no offset/cursor.
- There is no stable application/MCP error code model.
- Parser/model unit tests and sanitized fixtures exist; broader sync/MCP integration tests are still needed.

## Provider limitations pending confirmation

- The NuGet 0.10.0 source confirms methods for FIT, profile/settings, fitness age, configured HR zones, body composition, and blood pressure. Real-account payload compatibility and fixtures still require validation.
- HRV measured series and naps still lack a confirmed response shape in the currently used methods.

## Environment

- Live migration/synchronization requires configured PostgreSQL and Garmin credentials.
- PowerShell 5 reflection cannot directly load the net10 Garmin DLL because its host lacks `System.Runtime` 10; the application itself builds successfully under the .NET 10 SDK.
