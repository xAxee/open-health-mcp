using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using OpenHealthMCP.Data;

namespace OpenHealthMCP.Mcp;

[McpServerToolType]
public sealed class SeriesTools
{
    private const string DefaultSource = "garmin";
    private const int DefaultMaxPoints = 500;
    private const int MaximumMaxPoints = 5000;
    private static readonly string[] DayMetrics =
        ["heart_rate", "stress", "body_battery", "hrv", "spo2", "respiration", "sleep_respiration", "sleep_stage"];
    private static readonly string[] ActivityFields =
        ["heart_rate", "speed", "pace", "elevation", "cadence", "power", "temperature", "distance", "respiration", "latitude", "longitude"];

    [McpServerTool(Name = "get_day_series", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns bounded measured daily time-series from PostgreSQL. Supports multiple metrics, UTC range filtering, explicit interval aggregation, and transparent downsampling. Missing measurements are never interpolated.")]
    public static async Task<DaySeriesResult> GetDaySeriesAsync(
        [Description("Provider calendar date in YYYY-MM-DD format.")] string date,
        [Description("Comma-separated metrics: heart_rate, stress, body_battery, hrv, spo2, respiration, sleep_respiration, sleep_stage.")] string metrics,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Optional inclusive UTC timestamp bound in ISO 8601 format.")] string? from = null,
        [Description("Optional inclusive UTC timestamp bound in ISO 8601 format.")] string? to = null,
        [Description("Response aggregation interval: raw, 1s, 5s, 15s, 30s, 1m, 5m, 15m, 30m, or 1h. Defaults to raw.")] string? interval = null,
        [Description("Maximum returned points across all selected metrics; defaults to 500 and cannot exceed 5000.")] int? maxPoints = null,
        [Description("Optional provider source. Defaults to garmin.")] string? source = null)
    {
        var parsedDate = ParseDate(date, nameof(date));
        var selectedMetrics = ParseSelection(metrics, DayMetrics, nameof(metrics));
        var (fromUtc, toUtc) = ParseTimestampRange(from, to);
        var normalizedSource = NormalizeSource(source);
        var pointLimit = ValidatePointLimit(maxPoints);
        var requestedInterval = SeriesProcessing.ParseInterval(interval);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.HealthMetricSamples.AsNoTracking()
            .Where(item => item.Source == normalizedSource &&
                           item.LocalDate == parsedDate &&
                           selectedMetrics.Contains(item.Metric));
        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.TimestampUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.TimestampUtc <= toUtc.Value);
        }

        var rows = await query
            .OrderBy(item => item.TimestampUtc)
            .ThenBy(item => item.Metric)
            .Select(item => new DaySeriesRow(
                item.Metric,
                item.TimestampUtc,
                item.EndTimestampUtc,
                item.ValueNumeric,
                item.ValueText,
                item.Unit,
                item.SourceType,
                item.Quality))
            .ToListAsync(cancellationToken);
        var utcOffset = await dbContext.DailyMetrics.AsNoTracking()
            .Where(item => item.Source == normalizedSource && item.Date == parsedDate)
            .Select(item => item.UtcOffsetMinutes)
            .SingleOrDefaultAsync(cancellationToken);

        var available = rows.Select(item => item.Metric).Distinct(StringComparer.Ordinal).Order().ToArray();
        var aggregated = requestedInterval.Seconds.HasValue
            ? AggregateDayRows(rows, requestedInterval.Seconds.Value)
            : rows.Select(ToPoint).ToArray();
        var returned = SeriesProcessing.Downsample(aggregated, pointLimit);
        var effectiveInterval = returned.Count < 2
            ? requestedInterval.Name
            : SeriesProcessing.Resolution(returned.Select(point => point.TimestampUtc.ToUnixTimeMilliseconds() / 1000d));
        return new DaySeriesResult(
            normalizedSource,
            parsedDate,
            selectedMetrics,
            available,
            rows.Count,
            returned.Count,
            effectiveInterval,
            requestedInterval.Name,
            requestedInterval.Seconds.HasValue,
            aggregated.Count > returned.Count,
            "timestamp_utc_with_provider_local_date",
            null,
            utcOffset,
            rows.Count == 0 ? null : rows.Min(item => item.TimestampUtc),
            rows.Count == 0 ? null : rows.Max(item => item.TimestampUtc),
            returned);
    }

    [McpServerTool(Name = "get_activity_series", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns bounded measured activity samples from PostgreSQL. Supports field selection, elapsed-time range, explicit interval aggregation, and transparent response-only downsampling.")]
    public static async Task<ActivitySeriesResult> GetActivitySeriesAsync(
        [Description("Provider activity identifier returned by get_activities.")] string activityId,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Optional comma-separated fields. Defaults to all available fields.")] string? fields = null,
        [Description("Optional inclusive elapsed-time lower bound in seconds.")] double? fromElapsedSeconds = null,
        [Description("Optional inclusive elapsed-time upper bound in seconds.")] double? toElapsedSeconds = null,
        [Description("Response aggregation interval: raw, 1s, 5s, 15s, 30s, 1m, 5m, 15m, 30m, or 1h. Defaults to raw.")] string? interval = null,
        [Description("Maximum returned points; defaults to 500 and cannot exceed 5000.")] int? maxPoints = null,
        [Description("Optional provider source. Defaults to garmin.")] string? source = null)
    {
        ValidateActivityId(activityId);
        ValidateElapsedRange(fromElapsedSeconds, toElapsedSeconds);
        activityId = activityId.Trim();
        var normalizedSource = NormalizeSource(source);
        var pointLimit = ValidatePointLimit(maxPoints);
        var requestedInterval = SeriesProcessing.ParseInterval(interval);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var activity = await dbContext.Activities.AsNoTracking()
            .Where(item => item.Source == normalizedSource && item.ExternalId == activityId)
            .Select(item => new { item.Id, item.StreamsSyncedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (activity is null)
        {
            return EmptyActivityResult(normalizedSource, activityId, false, requestedInterval.Name);
        }

        var query = dbContext.ActivitySamples.AsNoTracking().Where(item => item.ActivityId == activity.Id);
        if (fromElapsedSeconds.HasValue)
        {
            query = query.Where(item => item.ElapsedSeconds >= fromElapsedSeconds.Value);
        }

        if (toElapsedSeconds.HasValue)
        {
            query = query.Where(item => item.ElapsedSeconds <= toElapsedSeconds.Value);
        }

        var rows = await query.OrderBy(item => item.ElapsedSeconds)
            .Select(item => new ActivitySeriesRow(
                item.TimestampUtc,
                item.ElapsedSeconds,
                item.HeartRateBpm,
                item.SpeedMetersPerSecond,
                item.PaceSecondsPerKilometer,
                item.ElevationMeters,
                item.Cadence,
                item.PowerWatts,
                item.TemperatureCelsius,
                item.DistanceMeters,
                item.RespirationRate,
                item.Latitude,
                item.Longitude,
                item.SourceType))
            .ToListAsync(cancellationToken);
        var available = AvailableActivityFields(rows);
        var selectedFields = string.IsNullOrWhiteSpace(fields)
            ? available
            : ParseSelection(fields, ActivityFields, nameof(fields));
        if (selectedFields.Any(field => !available.Contains(field, StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                $"fields contains an unavailable value. Available fields: {string.Join(", ", available)}.",
                nameof(fields));
        }

        var rawResolution = SeriesProcessing.Resolution(rows.Select(item => item.ElapsedSeconds));
        var aggregated = requestedInterval.Seconds.HasValue
            ? AggregateActivityRows(rows, selectedFields, requestedInterval.Seconds.Value)
            : rows.Select(item => ToPoint(item, selectedFields, 1, null)).Where(item => item.Values.Count > 0).ToArray();
        var returned = SeriesProcessing.Downsample(aggregated, pointLimit);
        var effectiveResolution = returned.Count < 2
            ? requestedInterval.Seconds.HasValue ? requestedInterval.Name : rawResolution
            : SeriesProcessing.Resolution(returned.Select(item => item.ElapsedSeconds));
        return new ActivitySeriesResult(
            true,
            normalizedSource,
            activityId,
            activity.StreamsSyncedAt.HasValue,
            selectedFields,
            available,
            rows.Count,
            returned.Count,
            requestedInterval.Name,
            effectiveResolution,
            rawResolution,
            effectiveResolution,
            requestedInterval.Seconds.HasValue,
            aggregated.Count > returned.Count,
            "elapsed_seconds_with_optional_timestamp_utc",
            returned);
    }

    private static IReadOnlyList<DaySeriesPoint> AggregateDayRows(
        IReadOnlyList<DaySeriesRow> rows,
        double intervalSeconds) => rows
        .GroupBy(item => new { item.Metric, Bucket = SeriesProcessing.TimeBucket(item.TimestampUtc, intervalSeconds) })
        .OrderBy(group => group.Min(item => item.TimestampUtc))
        .ThenBy(group => group.Key.Metric, StringComparer.Ordinal)
        .Select(group =>
        {
            var values = group.Where(item => item.ValueNumeric.HasValue).Select(item => item.ValueNumeric!.Value).ToArray();
            var last = group.OrderBy(item => item.TimestampUtc).Last();
            var stageMetric = group.Key.Metric == "sleep_stage";
            return new DaySeriesPoint(
                group.Key.Metric,
                group.Min(item => item.TimestampUtc),
                stageMetric ? last.EndTimestampUtc : group.Max(item => item.EndTimestampUtc),
                stageMetric ? last.ValueNumeric : values.Length == 0 ? null : values.Average(),
                stageMetric ? last.ValueText : null,
                last.Unit,
                stageMetric ? last.SourceType : "derived_by_openhealth",
                last.Quality,
                group.Count(),
                stageMetric ? "last-measured-stage-v1" : "bucket-average-v1");
        })
        .ToArray();

    private static DaySeriesPoint ToPoint(DaySeriesRow row) => new(
        row.Metric, row.TimestampUtc, row.EndTimestampUtc, row.ValueNumeric, row.ValueText,
        row.Unit, row.SourceType, row.Quality, 1, null);

    private static IReadOnlyList<ActivitySeriesPoint> AggregateActivityRows(
        IReadOnlyList<ActivitySeriesRow> rows,
        IReadOnlyList<string> fields,
        double intervalSeconds) => rows
        .GroupBy(item => SeriesProcessing.ElapsedBucket(item.ElapsedSeconds, intervalSeconds))
        .OrderBy(group => group.Key)
        .Select(group =>
        {
            var representative = group.OrderBy(item => item.ElapsedSeconds).First();
            var ordered = group.OrderBy(item => item.ElapsedSeconds).ToArray();
            var values = fields
                .Select(field => new { Field = field, Value = AggregateField(ordered, field) })
                .Where(item => item.Value.HasValue)
                .ToDictionary(item => item.Field, item => item.Value!.Value, StringComparer.Ordinal);
            return new ActivitySeriesPoint(
                representative.TimestampUtc,
                representative.ElapsedSeconds,
                values,
                "derived_by_openhealth",
                group.Count(),
                "bucket-average-v1");
        })
        .Where(item => item.Values.Count > 0)
        .ToArray();

    private static ActivitySeriesPoint ToPoint(
        ActivitySeriesRow row,
        IReadOnlyList<string> fields,
        int measurementCount,
        string? algorithm) => new(
        row.TimestampUtc,
        row.ElapsedSeconds,
        fields.Select(field => new { Field = field, Value = FieldValue(row, field) })
            .Where(item => item.Value.HasValue)
            .ToDictionary(item => item.Field, item => item.Value!.Value, StringComparer.Ordinal),
        row.SourceType,
        measurementCount,
        algorithm);

    private static double? AggregateField(IReadOnlyList<ActivitySeriesRow> rows, string field)
    {
        var values = rows.Select(row => FieldValue(row, field)).Where(value => value.HasValue)
            .Select(value => value!.Value).ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        return field is "distance" or "latitude" or "longitude" ? values[^1] : values.Average();
    }

    private static double? FieldValue(ActivitySeriesRow row, string field) => field switch
    {
        "heart_rate" => row.HeartRateBpm,
        "speed" => row.SpeedMetersPerSecond,
        "pace" => row.PaceSecondsPerKilometer,
        "elevation" => row.ElevationMeters,
        "cadence" => row.Cadence,
        "power" => row.PowerWatts,
        "temperature" => row.TemperatureCelsius,
        "distance" => row.DistanceMeters,
        "respiration" => row.RespirationRate,
        "latitude" => row.Latitude,
        "longitude" => row.Longitude,
        _ => null
    };

    private static IReadOnlyList<string> AvailableActivityFields(IReadOnlyList<ActivitySeriesRow> rows) =>
        ActivityFields.Where(field => rows.Any(row => FieldValue(row, field).HasValue)).ToArray();

    private static ActivitySeriesResult EmptyActivityResult(
        string source,
        string activityId,
        bool synchronized,
        string interval) => new(
        false, source, activityId, synchronized, [], [], 0, 0, interval, interval,
        "unknown", interval == "raw" ? "unknown" : interval, interval != "raw", false,
        "elapsed_seconds_with_optional_timestamp_utc", []);

    private static DateOnly ParseDate(string value, string parameterName) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date)
            ? date
            : throw new ArgumentException($"{parameterName} must use YYYY-MM-DD format.", parameterName);

    private static (DateTimeOffset? From, DateTimeOffset? To) ParseTimestampRange(string? from, string? to)
    {
        var parsedFrom = ParseOptionalTimestamp(from, nameof(from));
        var parsedTo = ParseOptionalTimestamp(to, nameof(to));
        if (parsedFrom > parsedTo)
        {
            throw new ArgumentException("from must not be after to.");
        }

        return (parsedFrom, parsedTo);
    }

    private static DateTimeOffset? ParseOptionalTimestamp(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var timestamp)
            ? timestamp
            : throw new ArgumentException($"{parameterName} must be an ISO 8601 timestamp.", parameterName);
    }

    private static IReadOnlyList<string> ParseSelection(
        string selection,
        IReadOnlyList<string> supported,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(selection))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        var selected = selection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (selected.Length == 0 || selected.Any(item => !supported.Contains(item, StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                $"{parameterName} contains an unsupported value. Supported values: {string.Join(", ", supported)}.",
                parameterName);
        }

        return selected;
    }

    private static string NormalizeSource(string? source)
    {
        var normalized = string.IsNullOrWhiteSpace(source) ? DefaultSource : source.Trim().ToLowerInvariant();
        if (normalized.Length > 50 || normalized.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("source contains invalid characters.", nameof(source));
        }

        return normalized;
    }

    private static int ValidatePointLimit(int? maxPoints)
    {
        var value = maxPoints ?? DefaultMaxPoints;
        return value is >= 2 and <= MaximumMaxPoints
            ? value
            : throw new ArgumentException($"maxPoints must be between 2 and {MaximumMaxPoints}.", nameof(maxPoints));
    }

    private static void ValidateActivityId(string activityId)
    {
        if (string.IsNullOrWhiteSpace(activityId) || activityId.Length > 200)
        {
            throw new ArgumentException("activityId is required and cannot exceed 200 characters.", nameof(activityId));
        }
    }

    private static void ValidateElapsedRange(double? from, double? to)
    {
        if (from is < 0 || to is < 0 || from > to)
        {
            throw new ArgumentException("Elapsed-time bounds must be non-negative and from must not be after to.");
        }
    }

    private sealed record DaySeriesRow(
        string Metric,
        DateTimeOffset TimestampUtc,
        DateTimeOffset? EndTimestampUtc,
        double? ValueNumeric,
        string? ValueText,
        string Unit,
        string SourceType,
        string? Quality);

    private sealed record ActivitySeriesRow(
        DateTimeOffset? TimestampUtc,
        double ElapsedSeconds,
        double? HeartRateBpm,
        double? SpeedMetersPerSecond,
        double? PaceSecondsPerKilometer,
        double? ElevationMeters,
        double? Cadence,
        double? PowerWatts,
        double? TemperatureCelsius,
        double? DistanceMeters,
        double? RespirationRate,
        double? Latitude,
        double? Longitude,
        string SourceType);
}