# OpenHealthMCP

OpenHealthMCP is a self-hosted personal health data layer for MCP-compatible AI assistants. It synchronizes health and activity data into your own PostgreSQL database and exposes normalized, read-only data through a remote MCP server. Garmin Connect is the first supported provider.

OpenHealthMCP does not contain or call an LLM. It is designed for exactly one self-hosted user per installation.

> [!WARNING]
> OpenHealthMCP is an unofficial project and is not affiliated with or endorsed by Garmin. Garmin Connect integration depends on undocumented behavior and may break when Garmin changes its services. Use it only for personal automation and at your own risk.

## Features

- Garmin daily health synchronization: steps, heart rate, HRV, stress, Body Battery, sleep and its phases, calories, intensity minutes, respiration, and SpO2 when supplied by Garmin.
- Garmin activity synchronization with normalized timing, speed/pace, elevation, cadence, power, temperature, respiration, training effect/load, laps, heart-rate zones, and bounded time-series streams when supplied by Garmin.
- PostgreSQL 17 history with idempotent updates and uniqueness constraints.
- Original Garmin JSON response records preserved as PostgreSQL `jsonb`.
- Automatic refresh of recent days with a configurable interval and lookback.
- Authenticated historical backfill processed in bounded chunks.
- Remote Streamable HTTP MCP server with ten provider-neutral, read-only tools.
- Constant-time Bearer token authentication.
- Automatic EF Core migrations with startup retry.
- Docker Compose deployment with private PostgreSQL networking and persistent volumes.

## Architecture

```text
Garmin Connect
    ↓
GarminProvider (unofficial client isolated under Providers/Garmin)
    ↓
HealthSyncService
    ↓
PostgreSQL
    ↓
read-only MCP tools
    ↓
MCP-compatible AI client
```

The runtime is deliberately small: one ASP.NET Core process and one PostgreSQL container. The shared schema uses `Source` and `ExternalId`, so another provider can be introduced without changing MCP tools or core persistence. No additional provider is implemented in v0.1.

## Quick start

Requirements:

- Docker with Docker Compose v2.
- A Garmin Connect account for real synchronization.

Copy the example configuration:

```bash
cp .env.example .env
```

Set at least:

```env
POSTGRES_PASSWORD=choose-a-strong-database-password
MCP_AUTH_TOKEN=generate-a-long-random-token-at-least-32-characters
GARMIN_EMAIL=your-garmin-email
GARMIN_PASSWORD=your-garmin-password
```

Start the service:

```bash
docker compose up -d --build
```

Check the public health endpoint:

```bash
curl http://localhost:8080/health
```

Expected response:

```json
{"status":"healthy"}
```

Inspect startup and synchronization logs without exposing configuration values:

```bash
docker compose logs -f openhealthmcp
```

Pending EF Core migrations are applied automatically. PostgreSQL is reachable only on the internal Compose network and is not published to the host.

## Production deployment

The repository includes a GitHub Actions pipeline and production Docker Compose configuration for deployment through an existing Caddy reverse proxy:

- `.github/workflows/ci-cd.yml` validates the application, publishes immutable images to GHCR, and deploys `main` through SSH;
- `deploy/compose.production.yml` exposes the application only to Docker networks and keeps PostgreSQL private;
- `deploy/deploy.sh` deploys an immutable `sha-<commit>` image, checks `/health`, and attempts rollback when a previous image is locally available;
- `deploy/Caddyfile.health` contains the `health.hubertiwan.pl` reverse-proxy block.

The production host is expected to provide an external Docker network named `proxy`. The OpenHealthMCP application joins this network under the DNS name `openhealthmcp`; PostgreSQL remains attached only to the private project network.

### One-time VPS preparation

Create the deployment directory as the SSH deployment user:

```bash
install -d -m 700 /home/nexus-deploy/openhealthmcp
touch /home/nexus-deploy/openhealthmcp/.env
chmod 600 /home/nexus-deploy/openhealthmcp/.env
```

Generate three independent hexadecimal secrets so they are safe in dotenv and PostgreSQL connection-string syntax:

```bash
openssl rand -hex 32
openssl rand -hex 32
openssl rand -hex 32
```

Use the values for `POSTGRES_PASSWORD`, `MCP_AUTH_TOKEN`, and `OAUTH_OWNER_PASSWORD`, respectively. Do not reuse one value for multiple purposes. Set `OAUTH_BASE_URL=https://health.hubertiwan.pl`. Add the Garmin credentials without committing or copying this file into CI. Keep its permissions at `600`.

If the GHCR package is private, log in once as the deployment user with a fine-grained token that has read-only package access:

```bash
read -rsp 'GHCR token: ' GHCR_TOKEN
echo
printf '%s' "${GHCR_TOKEN}" | docker login ghcr.io --username xAxee --password-stdin
unset GHCR_TOKEN
```

Do not put that token in `.env`. The token value is read without terminal echo and does not appear in the command history. Alternatively, make the GHCR package public after its first publication.

### Existing Caddy integration

Append `deploy/Caddyfile.health` to the existing host Caddyfile only once. Validate the complete configuration before reloading Caddy:

```bash
docker exec portfolio-caddy \
  caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
docker exec portfolio-caddy \
  caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile
```

Caddy must share the external `proxy` network with OpenHealthMCP. Do not publish application port `8080` or PostgreSQL port `5432` on the host.

### GitHub Actions configuration

Configure these repository or `production` environment secrets:

| Secret | Value |
|---|---|
| `VPS_HOST` | VPS hostname or IP address. |
| `VPS_SSH_PORT` | SSH port, normally `22`. |
| `VPS_USER` | `nexus-deploy`. |
| `VPS_SSH_PRIVATE_KEY` | Dedicated unencrypted Ed25519 private key used only by GitHub Actions. |
| `VPS_SSH_KNOWN_HOSTS` | Verified `known_hosts` entry for the VPS; do not disable host-key checking. |

Install the matching public key in `/home/nexus-deploy/.ssh/authorized_keys`. The deployment user needs Docker access but does not need permission to modify Caddy or other applications.

Every pull request to `main` runs validation. A successful push to `main` additionally publishes:

```text
ghcr.io/xaxee/open-health-mcp:sha-<full-commit-sha>
ghcr.io/xaxee/open-health-mcp:latest
```

Only the immutable SHA tag is deployed. The workflow copies the production Compose file and deployment script, preserves the VPS `.env`, updates only the `openhealthmcp` Compose project, and verifies `https://health.hubertiwan.pl/health`.

### Operations and rollback

Inspect the isolated project without affecting other VPS applications:

```bash
cd /home/nexus-deploy/openhealthmcp
docker compose --project-name openhealthmcp --file compose.production.yml ps
docker compose --project-name openhealthmcp --file compose.production.yml logs --tail 100 openhealthmcp
```

For a manual rollback, replace `OPENHEALTHMCP_IMAGE` in `.env` with a previously published immutable SHA tag and run:

```bash
./deploy.sh ghcr.io/xaxee/open-health-mcp:sha-PREVIOUS_FULL_COMMIT_SHA
```

Never run a host-wide `docker compose down`, `docker system prune --volumes`, or volume removal command on the shared VPS.

## Configuration

| Variable | Required | Default | Purpose |
|---|---:|---:|---|
| `POSTGRES_PASSWORD` | Compose | none | Password used by the private PostgreSQL container. |
| `ConnectionStrings__Postgres` | App | none | PostgreSQL connection string; Compose supplies it automatically. |
| `MCP_AUTH_TOKEN` | yes | none | Static Bearer token for manual MCP clients and the admin synchronization endpoint; minimum 32 characters. |
| `OAUTH_BASE_URL` | yes | none | Public HTTPS origin of the OAuth and MCP server, without a trailing slash or path. |
| `OAUTH_OWNER_PASSWORD` | yes | none | Separate owner password used only on the OAuth consent page; minimum 32 characters. |
| `GARMIN_EMAIL` | sync | empty | Garmin Connect account email. |
| `GARMIN_PASSWORD` | sync | empty | Garmin Connect account password. |
| `GARMIN_MFA_CODE` | conditional | empty | Current one-time code if Garmin requires MFA during authentication. Remove or replace it after use. |
| `GARMIN_SESSION_PATH` | no | `garmin-session/token.json` | Persistent OAuth2 token cache path. Compose uses a dedicated volume. |
| `GARMIN_ACTIVITY_ENRICHMENT_LIMIT` | no | `10` | Maximum activities enriched with optional laps and HR-zone requests per 31-day sync chunk; 0 disables enrichment, maximum 100. |
| `GARMIN_ACTIVITY_ENRICHMENT_DELAY_MS` | no | `250` | Delay in milliseconds between optional Garmin activity enrichment requests; 0–10000. |
| `SYNC_INTERVAL_HOURS` | no | `3` | Scheduled synchronization interval; greater than 0 and at most 168. |
| `SYNC_LOOKBACK_DAYS` | no | `3` | Recent period refreshed by every scheduled run, including today; 1–31. |

Never commit `.env`. It is ignored by Git.

## Authentication

`GET /health` and OAuth discovery/authorization endpoints are public. The MCP endpoint accepts either an OAuth access token or the static installation token. Manual clients can use:

```http
Authorization: Bearer <MCP_AUTH_TOKEN>
```

`POST /admin/sync` accepts only `MCP_AUTH_TOKEN`; an OAuth token issued to ChatGPT cannot invoke it. Missing or invalid credentials return `401 Unauthorized` or `403 Forbidden` as appropriate. Configured passwords, tokens, authorization codes, Garmin credentials, session tokens, and raw health payloads are not written to normal application logs. OAuth authorization codes and tokens are stored in PostgreSQL only as SHA-256 hashes.

### OAuth for ChatGPT

OpenHealthMCP implements OAuth 2.1 authorization code flow for public clients with:

- OAuth Protected Resource Metadata (RFC 9728);
- Authorization Server Metadata (RFC 8414);
- Dynamic Client Registration (RFC 7591);
- mandatory PKCE using `S256`;
- exact redirect URI validation and MCP resource binding;
- the single read-only scope `health.read`;
- one-time authorization codes valid for 5 minutes;
- access tokens valid for 1 hour;
- rotating refresh tokens valid for 30 days.

Metadata endpoints:

```text
https://health.hubertiwan.pl/.well-known/oauth-protected-resource/mcp
https://health.hubertiwan.pl/.well-known/oauth-authorization-server
```

To connect from ChatGPT on an eligible web account:

1. Open **Settings → Security and login** and enable **Developer mode**.
2. Open `https://chatgpt.com/plugins`.
3. Select the plus button to create a developer-mode app.
4. Enter `OpenHealthMCP` as the name and `https://health.hubertiwan.pl/mcp` as the MCP server URL.
5. Select OAuth authentication. Do not enter `MCP_AUTH_TOKEN` into ChatGPT.
6. When redirected to OpenHealthMCP, verify the displayed client and callback URL, then enter `OAUTH_OWNER_PASSWORD` and approve access.
7. After the app appears under **Drafts**, enable it from the conversation's **Developer mode** tool menu.

Only authorize a client when the callback displayed by OpenHealthMCP belongs to ChatGPT. Revoke all existing ChatGPT grants by deleting rows from the OAuth token/client tables only when you intentionally want to disconnect every registered OAuth client.

## Garmin synchronization

### Automatic refresh

The background service runs once after startup and then every `SYNC_INTERVAL_HOURS`. By default, each run refreshes today and the previous two calendar days (`SYNC_LOOKBACK_DAYS=3`) because Garmin values can change after a device sync.

Daily summary, heart rate, daily timeline, sleep, HRV, and activity units are persisted independently. A failure in one unit does not roll back previously committed data. A failed scheduled run is recorded in `SyncState` and does not stop the application host.

Repeated synchronization is idempotent:

- daily metrics are unique by `(Source, Date)`;
- activities are unique by `(Source, ExternalId)`;
- activity laps are unique by `(ActivityId, LapIndex)`;
- activity heart-rate zones are unique by `(ActivityId, ZoneNumber)`;
- activity streams are unique by `ActivityId`;
- daily timelines are unique by `(Source, Date, Metric)`;
- raw payloads are unique by `(Source, DataType, ExternalId)`.

Activity summary fields are read from Garmin's paginated activity list response. This includes duration, elapsed and moving time, distance, elevation gain/loss, speed, steps, sport-specific cadence, power, temperature range, respiration, SWOLF/lengths, aerobic and anaerobic Training Effect, activity training load, Training Stress Score, Intensity Factor, and activity VO2 max when the individual fields are present. Average pace in seconds per kilometer is a unit conversion from Garmin's average speed; it is not a training interpretation.

Laps, activity heart-rate-zone time, and streams require optional Garmin requests to the confirmed `/splits`, `/hrTimeInZones`, and `/details` activity endpoints. They are fetched sequentially only for activities that have not yet been enriched, up to `GARMIN_ACTIVITY_ENRICHMENT_LIMIT` per 31-day chunk, with a configurable delay between requests. Re-run the same historical range to enrich any activities left beyond the limit. Recent activities can be refreshed at most once per 24 hours for seven days, allowing Garmin's delayed processing to complete. Activity streams are requested with `maxChartSize=2000`, omit the polyline, and are backfilled only for activities from the last 365 days. A failed optional request remains pending and can be retried by a later synchronization. Raw list, split, HR-zone, and details responses are retained as `jsonb`.

Daily heart-rate samples are normalized from the existing daily heart-rate response without another Garmin request. Stress and Body Battery timelines use one `/wellness-service/wellness/dailyStress/` request per synchronized date. Normalized daily timelines use a rolling 365-day retention window: older historical sync dates keep their scalar daily metrics and existing raw payloads but do not trigger `dailyStress` requests or retain dense normalized samples. Each retained series is stored as one compact `jsonb` document rather than one database row per sample. This keeps idempotent updates and reads inexpensive while preserving the original provider response separately.

### Historical synchronization

Use the authenticated admin endpoint:

```bash
curl --request POST http://localhost:8080/admin/sync \
  --header "Authorization: Bearer replace-with-your-configured-token" \
  --header "Content-Type: application/json" \
  --data '{"from":"2025-01-01","to":"2026-08-28"}'
```

Large ranges are processed synchronously in 31-day chunks and reuse the same idempotent synchronization logic. Only dates from `2000-01-01` through the current period are accepted. A provider failure is returned clearly; it is never reported as a successful sync.

### Session and MFA behavior

The unofficial Garmin client stores a short-lived OAuth2 token in the persistent `garmin_session` Docker volume, so restarts do not always require a full login. Passwords and session contents are not stored in PostgreSQL or exposed through MCP.

If Garmin requests MFA, set `GARMIN_MFA_CODE` to the current code and restart or invoke historical synchronization. This non-interactive v0.1 flow depends on the code still being valid when authentication occurs.

## MCP

Remote Streamable HTTP endpoint:

```text
http://localhost:8080/mcp
```

For a public deployment, use an HTTPS reverse proxy and configure an MCP-compatible client with:

```text
URL: https://your-domain.example/mcp
Authorization: Bearer <MCP_AUTH_TOKEN>
```

Available tools:

| Tool | Purpose |
|---|---|
| `get_day` | Return normalized metrics for one date. |
| `get_activities` | Return lightweight activity summaries in a date range, with optional type and bounded limit. |
| `get_activity` | Return one normalized activity with all available scalar activity and training-effect metrics. |
| `get_activity_laps` | Return normalized provider laps/splits for one stored activity. |
| `get_activity_hr_zones` | Return provider time in heart-rate zones for one stored activity. |
| `get_activity_streams` | Return selected, bounded activity stream metrics with elapsed time. |
| `get_daily_timeline` | Return bounded heart-rate, stress, or Body Battery samples for one day. |
| `get_activity_summary` | Return activity totals with optional type filter and daily, weekly, or monthly groups. |
| `get_trend` | Return deterministic statistics and daily samples for a supported metric. |
| `compare_periods` | Compare averages, absolute difference, percentage change, and sample counts. |

Supported trend metrics:

```text
steps
resting_heart_rate
average_heart_rate
min_heart_rate
max_heart_rate
hrv
stress
body_battery_min
body_battery_max
sleep_score
calories
active_calories
moderate_intensity_minutes
vigorous_intensity_minutes
intensity_minutes
sleep_duration_seconds
deep_sleep_seconds
light_sleep_seconds
rem_sleep_seconds
awake_sleep_seconds
average_respiration_rate
average_spo2
activity_count
activity_duration_seconds
activity_moving_duration_seconds
activity_distance_meters
activity_elevation_gain_meters
activity_elevation_loss_meters
activity_calories
activity_steps
activity_average_heart_rate
activity_training_load
activity_aerobic_training_effect
activity_anaerobic_training_effect
activity_vo2_max
```

MCP tools query PostgreSQL only. They do not contact Garmin, modify health data, expose raw provider payloads, or provide medical diagnoses.

`get_activity_laps` and `get_activity_hr_zones` return `found`, `source`, `activityId`, `synchronized`, and an ordered collection. `synchronized=false` means the optional Garmin enrichment has not completed. `synchronized=true` with an empty collection means no entries were returned or expected. HR-zone percentages are calculated only from Garmin's `secsInZone` values, using their sum as the denominator; zone boundaries are returned only when Garmin provides `zoneLowBoundary` and are never inferred.

`get_activity_streams` accepts a comma-separated metric selection, elapsed-time bounds, and `maxPoints` from 2 through 2000 (default 500). `get_daily_timeline` uses the same point limits. When a stored series exceeds the limit, deterministic evenly spaced downsampling retains its first and last points. `get_activity_summary` supports `none`, `daily`, `weekly`, and `monthly` grouping, rejects requests that would span more than 400 result periods, keeps sums `null` when no activity supplied the metric, and duration-weights average heart rate.

## Security

OpenHealthMCP stores sensitive personal health and fitness data.

- Generate a strong, random `MCP_AUTH_TOKEN` and database password.
- Never commit `.env`, credentials, Garmin session files, or database backups.
- Do not publish PostgreSQL to the host or Internet. The supplied Compose file does not expose port 5432.
- Use HTTPS whenever `/mcp` or `/admin/sync` is reachable outside a trusted local/private network.
- Restrict reverse-proxy and host access as appropriate for personal health data.
- Rotate credentials if logs or environment files are exposed.

Bearer tokens over plain HTTP can be intercepted. The built-in HTTP listener is intended to sit behind TLS termination for remote access.

## Garmin limitations

- Garmin Connect does not provide a simple official consumer API for this use case.
- Integration uses the maintained but unofficial `Unofficial.Garmin.Connect` package and undocumented Garmin behavior.
- Garmin can change authentication or response formats without notice.
- Real-account authentication, MFA, and payload compatibility must be verified with your own account and device data.
- The package exposes Garmin's activity list, `/splits`, `/hrTimeInZones`, `/details`, and `/dailyStress` models used here. It does not expose a stable, provider-independent meaning for Garmin-specific Training Effect labels/messages, so OpenHealthMCP stores only confirmed numeric values.
- Availability varies by activity type, device, sensors, and Garmin processing. In particular, power, temperature, respiration, cadence, Training Effect/load, laps, and HR zones can legitimately be absent.
- Missing optional values remain `null`; OpenHealthMCP does not fabricate provider measurements or payloads.
- If credentials are absent or authentication fails, synchronization records and returns an actionable failure while the database, health endpoint, and historical MCP reads remain available.

## Local development

The project targets .NET 10. Supply configuration through environment variables, then run:

```bash
dotnet restore
dotnet build
dotnet run
```

For normal development and runtime verification, Docker Compose is recommended because it supplies PostgreSQL and applies migrations automatically.

## Future providers

The normalized schema and `IHealthDataProvider` boundary are designed to permit later support for providers such as Strava, Apple Health, Fitbit, Whoop, and Health Connect. They are not implemented in v0.1, and no multi-provider conflict resolution is attempted.

## License

OpenHealthMCP is licensed under the [MIT License](LICENSE).
