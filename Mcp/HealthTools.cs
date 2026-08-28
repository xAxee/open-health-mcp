using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using OpenHealthMCP.Data;
using OpenHealthMCP.Data.Entities;

namespace OpenHealthMCP.Mcp;

[McpServerToolType]
public sealed class HealthTools
{
    private const string DefaultSource = "garmin";
    private const int DefaultActivityLimit = 50;
    private const int MaximumActivityLimit = 200;
    private const int MaximumRangeDays = 3660;

    [McpServerTool(Name = "get_day", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns normalized health metrics for one calendar day. It provides data, not medical advice.")]
    public static async Task<DayLookupResult> GetDayAsync(
        [Description("Calendar date in YYYY-MM-DD format.")] string date,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Optional provider source. Defaults to garmin.")] string? source = null)
    {
        var parsedDate = ParseDate(date, nameof(date));
        var normalizedSource = NormalizeSource(source);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var result = await dbContext.DailyMetrics
            .AsNoTracking()
            .Where(item => item.Source == normalizedSource && item.Date == parsedDate)
            .Select(item => new DayResult(
                item.Source,
                item.Date,
                item.Steps,
                item.RestingHeartRate,
                item.AverageHeartRate,
                item.MinHeartRate,
                item.MaxHeartRate,
                item.Hrv,
                item.StressAverage,
                item.BodyBatteryMin,
                item.BodyBatteryMax,
                item.SleepScore,
                item.Calories))
            .SingleOrDefaultAsync(cancellationToken);

        return new DayLookupResult(result is not null, result);
    }

    [McpServerTool(Name = "get_activities", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns normalized activities in a date range, ordered newest first.")]
    public static async Task<IReadOnlyList<ActivityResult>> GetActivitiesAsync(
        [Description("Inclusive start date in YYYY-MM-DD format.")] string from,
        [Description("Inclusive end date in YYYY-MM-DD format.")] string to,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Optional normalized activity type.")] string? activityType = null,
        [Description("Optional result limit; defaults to 50 and cannot exceed 200.")] int? limit = null)
    {
        var (fromDate, toDate) = ParseRange(from, to, MaximumRangeDays);
        var effectiveLimit = limit ?? DefaultActivityLimit;
        if (effectiveLimit is < 1 or > MaximumActivityLimit)
        {
            throw new ArgumentException($"limit must be between 1 and {MaximumActivityLimit}.", nameof(limit));
        }

        var fromTimestamp = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var exclusiveTo = new DateTimeOffset(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Activities
            .AsNoTracking()
            .Where(item => item.StartedAt >= fromTimestamp && item.StartedAt < exclusiveTo);

        if (!string.IsNullOrWhiteSpace(activityType))
        {
            var normalizedType = activityType.Trim().ToLowerInvariant();
            query = query.Where(item => item.ActivityType == normalizedType);
        }

        return await query
            .OrderByDescending(item => item.StartedAt)
            .Take(effectiveLimit)
            .Select(ToActivityResult())
            .ToListAsync(cancellationToken);
    }

    [McpServerTool(Name = "get_activity", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns one normalized activity by provider activity identifier.")]
    public static async Task<ActivityLookupResult> GetActivityAsync(
        [Description("Provider activity identifier.")] string activityId,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activityId) || activityId.Length > 200)
        {
            throw new ArgumentException("activityId is required and cannot exceed 200 characters.", nameof(activityId));
        }

        var normalizedId = activityId.Trim();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var result = await dbContext.Activities
            .AsNoTracking()
            .Where(item => item.ExternalId == normalizedId)
            .OrderByDescending(item => item.Source == DefaultSource)
            .Select(ToActivityResult())
            .FirstOrDefaultAsync(cancellationToken);

        return new ActivityLookupResult(result is not null, result);
    }

    [McpServerTool(Name = "get_trend", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns deterministic statistics and daily samples for a supported normalized metric.")]
    public static async Task<TrendResult> GetTrendAsync(
        [Description("One of: steps, resting_heart_rate, hrv, stress, body_battery_max, sleep_score.")] string metric,
        [Description("Inclusive start date in YYYY-MM-DD format.")] string from,
        [Description("Inclusive end date in YYYY-MM-DD format.")] string to,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Optional provider source. Defaults to garmin.")] string? source = null)
    {
        var normalizedMetric = NormalizeMetric(metric);
        var normalizedSource = NormalizeSource(source);
        var (fromDate, toDate) = ParseRange(from, to, 366);
        var values = await LoadMetricValuesAsync(
            dbContextFactory,
            normalizedMetric,
            fromDate,
            toDate,
            normalizedSource,
            cancellationToken);

        return new TrendResult(
            normalizedMetric,
            fromDate,
            toDate,
            normalizedSource,
            Average(values),
            values.Count == 0 ? null : values.Min(item => item.Value),
            values.Count == 0 ? null : values.Max(item => item.Value),
            values.Count,
            values);
    }

    [McpServerTool(Name = "compare_periods", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Compares deterministic averages for two periods without providing medical interpretation.")]
    public static async Task<ComparePeriodsResult> ComparePeriodsAsync(
        [Description("One of: steps, resting_heart_rate, hrv, stress, body_battery_max, sleep_score.")] string metric,
        [Description("Inclusive period A start date in YYYY-MM-DD format.")] string periodAFrom,
        [Description("Inclusive period A end date in YYYY-MM-DD format.")] string periodATo,
        [Description("Inclusive period B start date in YYYY-MM-DD format.")] string periodBFrom,
        [Description("Inclusive period B end date in YYYY-MM-DD format.")] string periodBTo,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Optional provider source. Defaults to garmin.")] string? source = null)
    {
        var normalizedMetric = NormalizeMetric(metric);
        var normalizedSource = NormalizeSource(source);
        var (aFrom, aTo) = ParseRange(periodAFrom, periodATo, 3660);
        var (bFrom, bTo) = ParseRange(periodBFrom, periodBTo, 3660);

        var periodA = await LoadMetricValuesAsync(
            dbContextFactory,
            normalizedMetric,
            aFrom,
            aTo,
            normalizedSource,
            cancellationToken);
        var periodB = await LoadMetricValuesAsync(
            dbContextFactory,
            normalizedMetric,
            bFrom,
            bTo,
            normalizedSource,
            cancellationToken);

        var averageA = Average(periodA);
        var averageB = Average(periodB);
        var signedDifference = averageA.HasValue && averageB.HasValue ? averageB - averageA : null;
        double? absoluteDifference = signedDifference.HasValue ? Math.Abs(signedDifference.Value) : null;
        var percentageChange = signedDifference.HasValue && averageA is not null && averageA != 0
            ? signedDifference / averageA * 100
            : null;

        return new ComparePeriodsResult(
            normalizedMetric,
            aFrom,
            aTo,
            bFrom,
            bTo,
            normalizedSource,
            averageA,
            averageB,
            absoluteDifference,
            percentageChange,
            periodA.Count,
            periodB.Count);
    }

    private static async Task<IReadOnlyList<TrendValue>> LoadMetricValuesAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        string metric,
        DateOnly from,
        DateOnly to,
        string source,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.DailyMetrics
            .AsNoTracking()
            .Where(item => item.Source == source && item.Date >= from && item.Date <= to)
            .OrderBy(item => item.Date)
            .Select(item => new MetricRow(
                item.Date,
                item.Steps,
                item.RestingHeartRate,
                item.Hrv,
                item.StressAverage,
                item.BodyBatteryMax,
                item.SleepScore))
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new TrendValue(row.Date, GetMetricValue(row, metric)))
            .Where(item => !double.IsNaN(item.Value))
            .ToArray();
    }

    private static double GetMetricValue(MetricRow row, string metric) => metric switch
    {
        "steps" => row.Steps ?? double.NaN,
        "resting_heart_rate" => row.RestingHeartRate ?? double.NaN,
        "hrv" => row.Hrv ?? double.NaN,
        "stress" => row.StressAverage ?? double.NaN,
        "body_battery_max" => row.BodyBatteryMax ?? double.NaN,
        "sleep_score" => row.SleepScore ?? double.NaN,
        _ => double.NaN
    };

    private static System.Linq.Expressions.Expression<Func<Activity, ActivityResult>> ToActivityResult() =>
        item => new ActivityResult(
            item.Source,
            item.ExternalId,
            item.Name,
            item.ActivityType,
            item.StartedAt,
            item.DurationSeconds,
            item.DistanceMeters,
            item.Calories,
            item.AverageHeartRate,
            item.MaxHeartRate,
            item.ElevationGainMeters);

    private static DateOnly ParseDate(string value, string parameterName) =>
        DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var date)
            ? date
            : throw new ArgumentException($"{parameterName} must use YYYY-MM-DD format.", parameterName);

    private static (DateOnly From, DateOnly To) ParseRange(string from, string to, int maximumDays)
    {
        var fromDate = ParseDate(from, nameof(from));
        var toDate = ParseDate(to, nameof(to));
        if (fromDate > toDate)
        {
            throw new ArgumentException("from must not be after to.");
        }

        if (toDate.DayNumber - fromDate.DayNumber + 1 > maximumDays)
        {
            throw new ArgumentException($"Date range cannot exceed {maximumDays} days.");
        }

        return (fromDate, toDate);
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

    private static string NormalizeMetric(string metric)
    {
        var normalized = metric?.Trim().ToLowerInvariant();
        return normalized is "steps" or "resting_heart_rate" or "hrv" or "stress" or "body_battery_max" or "sleep_score"
            ? normalized
            : throw new ArgumentException("Unsupported metric.", nameof(metric));
    }

    private static double? Average(IReadOnlyList<TrendValue> values) =>
        values.Count == 0 ? null : values.Average(item => item.Value);

    private sealed record MetricRow(
        DateOnly Date,
        int? Steps,
        int? RestingHeartRate,
        double? Hrv,
        double? StressAverage,
        int? BodyBatteryMax,
        double? SleepScore);
}