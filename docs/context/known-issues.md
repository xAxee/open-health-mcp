# Known issues

## Product/code

- Daily calendar dates and scheduled `today` use UTC rather than an explicit provider/user timezone.
- Daily timelines are deleted after 365 days, conflicting with unrestricted local history.
- Activity detail fetch is capped at 2,000 samples before persistence.
- Raw payload rows are overwritten and lack content hash/parser version.
- Daily HRV series, sleep stages, SpO2, respiration, body composition, profile, and blood pressure are not implemented.
- Trend range is limited to 366 days and has no automatic granularity.
- `get_activities` has no offset/cursor.
- There is no stable application/MCP error code model.
- There is no .NET unit or integration test project and no sanitized Garmin fixtures.

## Provider limitations pending confirmation

- The NuGet 0.10.0 source confirms methods for FIT, profile/settings, fitness age, configured HR zones, body composition, and blood pressure. Real-account payload compatibility and fixtures still require validation.
- HRV measured series and naps still lack a confirmed response shape in the currently used methods.

## Environment

- Live migration/synchronization requires configured PostgreSQL and Garmin credentials.
- PowerShell 5 reflection cannot directly load the net10 Garmin DLL because its host lacks `System.Runtime` 10; the application itself builds successfully under the .NET 10 SDK.
