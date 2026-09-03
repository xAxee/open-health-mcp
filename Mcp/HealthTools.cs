using System.ComponentModel;
using System.Text.Json;
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
    private const int DefaultSeriesPointLimit = 500;
    private const int MaximumSeriesPointLimit = 2000;
    private const int MaximumActivitySummaryGroups = 400;

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

        var item = await dbContext.DailyMetrics
            .AsNoTracking()
            .Where(item => item.Source == normalizedSource && item.Date == parsedDate)
            .SingleOrDefaultAsync(cancellationToken);

        return new DayLookupResult(item is not null, item is null ? null : ToDayResult(item));
    }

    private static DayResult ToDayResult(DailyMetric item) => new(
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
        item.Calories,
        item.ActiveCalories,
        item.ModerateIntensityMinutes,
        item.VigorousIntensityMinutes,
        item.SleepDurationSeconds,
        item.DeepSleepSeconds,
        item.LightSleepSeconds,
        item.RemSleepSeconds,
        item.AwakeSleepSeconds,
        item.AverageRespirationRate,
        item.AverageSpo2,
        item.DistanceMeters,
        item.ActiveSeconds,
        item.UtcOffsetMinutes,
        null,
        new DayCaloriesResult(item.Calories, item.ActiveCalories, item.BmrCalories),
        new DayGoalsResult(item.StepsGoal, item.FloorsGoal, item.IntensityGoal),
        new DayIntensityResult(
            item.ModerateIntensityMinutes,
            item.VigorousIntensityMinutes,
            item.TotalIntensityMinutes,
            "vigorous_minutes_count_twice",
            new MetricSourceMetadata("derived_by_openhealth", "garmin-intensity-total-v1")),
        new DayStressResult(
            item.StressAverage, item.StressMax, item.StressQualifier,
            item.RestStressSeconds, item.LowStressSeconds, item.MediumStressSeconds,
            item.HighStressSeconds, item.ActivityStressSeconds,
            item.RestStressPercentage, item.LowStressPercentage,
            item.MediumStressPercentage, item.HighStressPercentage),
        new DayBodyBatteryResult(
            item.BodyBatteryMin, item.BodyBatteryMax, item.BodyBatteryCharged,
            item.BodyBatteryDrained, item.BodyBatteryMostRecent),
        new DayHrvResult(
            item.Hrv, item.HrvFiveMinuteHigh, item.HrvStatus, item.HrvCreatedAt,
            null, new MetricSourceMetadata("garmin_api")),
        new DaySleepResult(
            item.SleepStartUtc, item.SleepEndUtc, item.SleepStartLocal, item.SleepEndLocal,
            item.SleepDurationSeconds, item.NapDurationSeconds, item.DeepSleepSeconds,
            item.LightSleepSeconds, item.RemSleepSeconds, item.AwakeSleepSeconds,
            item.UnmeasurableSleepSeconds, item.SleepScore, item.SleepQualifier,
            item.SleepAwakeCount, item.AverageSleepStress, ParseOptionalJson(item.SleepSubScoresJson)),
        new DaySpo2Result(
            item.AverageSpo2, item.MinimumSpo2, null, item.LatestSpo2,
            item.AverageSleepSpo2, null, item.Spo2WindowStartUtc, item.Spo2WindowEndUtc),
        new DayRespirationResult(
            item.AverageRespirationRate, item.AverageSleepRespirationRate,
            item.MinimumRespirationRate, item.MaximumRespirationRate),
        new DaySourceMetadata(
            "garmin_api",
            new MetricSourceMetadata("derived_by_openhealth", "measured-series-average-v1"),
            "garmin_api", "garmin_api", "garmin_api", "garmin_api", "garmin_api", "garmin_api"));

    private static JsonElement? ParseOptionalJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [McpServerTool(Name = "get_activities", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns lightweight normalized activity summaries in a date range, ordered newest first. Use get_activity and the activity detail tools for richer data.")]
    public static async Task<IReadOnlyList<ActivityListResult>> GetActivitiesAsync(
        [Description("Inclusive start date in YYYY-MM-DD format.")] string from,
        [Description("Inclusive end date in YYYY-MM-DD format.")] string to,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Optional normalized activity type.")] string? activityType = null,
        [Description("Optional result limit; defaults to 50 and cannot exceed 200.")] int? limit = null,
        [Description("Optional zero-based result offset; defaults to 0 and cannot exceed 100000.")] int? offset = null)
    {
        var (fromDate, toDate) = ParseRange(from, to, MaximumRangeDays);
        var effectiveLimit = limit ?? DefaultActivityLimit;
        if (effectiveLimit is < 1 or > MaximumActivityLimit)
        {
            throw new ArgumentException($"limit must be between 1 and {MaximumActivityLimit}.", nameof(limit));
        }
        var effectiveOffset = offset ?? 0;
        if (effectiveOffset is < 0 or > 100000)
        {
            throw new ArgumentException("offset must be between 0 and 100000.", nameof(offset));
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
            .ThenByDescending(item => item.Id)
            .Skip(effectiveOffset)
            .Take(effectiveLimit)
            .Select(ToActivityListResult())
            .ToListAsync(cancellationToken);
    }

    [McpServerTool(Name = "get_activity", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns one normalized activity with available timing, speed, pace, cadence, power, temperature, respiration and Garmin-provided training effect/load values. Missing provider values are null. Use get_activity_laps and get_activity_hr_zones for collections.")]
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

    [McpServerTool(Name = "get_activity_laps", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns normalized Garmin-provided laps/splits for one stored activity. An empty list with synchronized=true means the provider returned no laps; synchronized=false means enrichment has not completed.")]
    public static async Task<ActivityLapsResult> GetActivityLapsAsync(
        [Description("Provider activity ID returned by get_activities.")] string activityId,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Optional provider source. Defaults to garmin.")] string? source = null)
    {
        ValidateActivityId(activityId);
        activityId = activityId.Trim();
        var normalizedSource = NormalizeSource(source);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var activity = await dbContext.Activities
            .AsNoTracking()
            .Where(item => item.Source == normalizedSource && item.ExternalId == activityId)
            .Select(item => new { item.Id, item.LapsSyncedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (activity is null)
        {
            return new ActivityLapsResult(false, normalizedSource, activityId, false, []);
        }

        var laps = await dbContext.ActivityLaps
            .AsNoTracking()
            .Where(item => item.ActivityId == activity.Id)
            .OrderBy(item => item.LapIndex)
            .Select(item => new ActivityLapResult(
                item.LapIndex,
                item.StartedAt,
                item.DurationSeconds,
                item.ElapsedDurationSeconds,
                item.MovingDurationSeconds,
                item.DistanceMeters,
                item.AverageSpeedMetersPerSecond,
                item.MaxSpeedMetersPerSecond,
                item.AveragePaceSecondsPerKilometer,
                item.Calories,
                item.AverageHeartRate,
                item.MaxHeartRate,
                item.ElevationGainMeters,
                item.ElevationLossMeters,
                item.MinElevationMeters,
                item.MaxElevationMeters,
                item.AverageCadence,
                item.MaxCadence,
                item.CadenceUnit,
                item.AverageTemperatureCelsius,
                item.MinTemperatureCelsius,
                item.MaxTemperatureCelsius,
                item.AverageRespirationRate,
                item.MaxRespirationRate,
                item.IntensityType))
            .ToListAsync(cancellationToken);

        return new ActivityLapsResult(
            true,
            normalizedSource,
            activityId,
            activity.LapsSyncedAt.HasValue,
            laps);
    }

    [McpServerTool(Name = "get_activity_hr_zones", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns Garmin-provided time in heart-rate zones for one stored activity. Percentages are normalized from Garmin secsInZone values; zone boundaries are not inferred.")]
    public static async Task<ActivityHeartRateZonesResult> GetActivityHeartRateZonesAsync(
        [Description("Provider activity ID returned by get_activities.")] string activityId,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Optional provider source. Defaults to garmin.")] string? source = null)
    {
        ValidateActivityId(activityId);
        activityId = activityId.Trim();
        var normalizedSource = NormalizeSource(source);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var activity = await dbContext.Activities
            .AsNoTracking()
            .Where(item => item.Source == normalizedSource && item.ExternalId == activityId)
            .Select(item => new { item.Id, item.HeartRateZonesSyncedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (activity is null)
        {
            return new ActivityHeartRateZonesResult(false, normalizedSource, activityId, false, []);
        }

        var storedZones = await dbContext.ActivityHeartRateZones
            .AsNoTracking()
            .Where(item => item.ActivityId == activity.Id)
            .OrderBy(item => item.ZoneNumber)
            .Select(item => new ActivityZoneRow(
                item.ZoneNumber, item.TimeSeconds, item.Percentage, item.LowBoundaryBpm))
            .ToListAsync(cancellationToken);
        var zones = ActivityZoneMapper.Map(storedZones);

        return new ActivityHeartRateZonesResult(
            true,
            normalizedSource,
            activityId,
            activity.HeartRateZonesSyncedAt.HasValue,
            zones);
    }

    [McpServerTool(Name = "get_activity_streams", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns bounded, normalized time-series samples for one stored activity. It reads PostgreSQL only. Select metrics with a comma-separated list; samples are deterministically downsampled when needed.")]
    public static async Task<ActivityStreamsResult> GetActivityStreamsAsync(
        [Description("Provider activity ID returned by get_activities.")] string activityId,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Optional comma-separated metrics. Available names are returned by the tool. Defaults to every available metric.")] string? metrics = null,
        [Description("Maximum returned samples; defaults to 500 and cannot exceed 2000.")] int? maxPoints = null,
        [Description("Optional inclusive elapsed-time lower bound in seconds.")] double? fromElapsedSeconds = null,
        [Description("Optional inclusive elapsed-time upper bound in seconds.")] double? toElapsedSeconds = null,
        [Description("Optional provider source. Defaults to garmin.")] string? source = null)
    {
        ValidateActivityId(activityId);
        activityId = activityId.Trim();
        var normalizedSource = NormalizeSource(source);
        var effectiveLimit = ValidateSeriesLimit(maxPoints);
        if (fromElapsedSeconds is < 0 || toElapsedSeconds is < 0 ||
            fromElapsedSeconds.HasValue && toElapsedSeconds.HasValue && fromElapsedSeconds > toElapsedSeconds)
        {
            throw new ArgumentException("Elapsed-time bounds must be non-negative and ordered.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var activity = await dbContext.Activities
            .AsNoTracking()
            .Where(item => item.Source == normalizedSource && item.ExternalId == activityId)
            .Select(item => new { item.Id, item.StreamsSyncedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (activity is null)
        {
            return new ActivityStreamsResult(false, normalizedSource, activityId, false, 0, 0, [], [], []);
        }

        var stream = await dbContext.ActivityStreams
            .AsNoTracking()
            .Where(item => item.ActivityId == activity.Id)
            .Select(item => new { item.SampleCount, item.AvailableMetrics, item.Samples })
            .SingleOrDefaultAsync(cancellationToken);
        if (stream is null)
        {
            return new ActivityStreamsResult(
                true, normalizedSource, activityId, activity.StreamsSyncedAt.HasValue, 0, 0, [], [], []);
        }

        var selectableMetrics = stream.AvailableMetrics
            .Where(metric => metric is not "timestamp" and not "elapsedTimeSeconds")
            .ToArray();
        var selectedMetrics = ParseSelectedMetrics(metrics, selectableMetrics);
        var samples = ParseActivityStreamSamples(
            stream.Samples.RootElement,
            selectedMetrics,
            fromElapsedSeconds,
            toElapsedSeconds);
        var downsampled = Downsample(samples, effectiveLimit);
        return new ActivityStreamsResult(
            true,
            normalizedSource,
            activityId,
            activity.StreamsSyncedAt.HasValue,
            stream.SampleCount,
            downsampled.Count,
            selectableMetrics,
            selectedMetrics,
            downsampled);
    }

    [McpServerTool(Name = "get_daily_timeline", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns a bounded stored daily timeline for heart_rate, stress, or body_battery. It reads PostgreSQL only and provides no interpretation.")]
    public static async Task<DailyTimelineResult> GetDailyTimelineAsync(
        [Description("Calendar date in YYYY-MM-DD format.")] string date,
        [Description("One of: heart_rate, stress, body_battery.")] string metric,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Maximum returned samples; defaults to 500 and cannot exceed 2000.")] int? maxPoints = null,
        [Description("Optional provider source. Defaults to garmin.")] string? source = null)
    {
        var parsedDate = ParseDate(date, nameof(date));
        var normalizedMetric = NormalizeTimelineMetric(metric);
        var normalizedSource = NormalizeSource(source);
        var effectiveLimit = ValidateSeriesLimit(maxPoints);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var timeline = await dbContext.DailyTimelines
            .AsNoTracking()
            .Where(item => item.Source == normalizedSource && item.Date == parsedDate && item.Metric == normalizedMetric)
            .Select(item => new { item.SampleCount, item.Samples })
            .SingleOrDefaultAsync(cancellationToken);
        if (timeline is null)
        {
            return new DailyTimelineResult(normalizedSource, parsedDate, normalizedMetric, false, 0, 0, []);
        }

        var samples = ParseDailyTimelineSamples(timeline.Samples.RootElement);
        var downsampled = Downsample(samples, effectiveLimit);
        return new DailyTimelineResult(
            normalizedSource,
            parsedDate,
            normalizedMetric,
            true,
            timeline.SampleCount,
            downsampled.Count,
            downsampled);
    }

    [McpServerTool(Name = "get_activity_summary", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns deterministic activity totals for a date range, optionally filtered by activity type and grouped daily, weekly, or monthly. Heart rate is duration-weighted; missing sums stay null.")]
    public static async Task<ActivitySummaryResult> GetActivitySummaryAsync(
        [Description("Inclusive start date in YYYY-MM-DD format.")] string from,
        [Description("Inclusive end date in YYYY-MM-DD format.")] string to,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken,
        [Description("Optional normalized activity type.")] string? activityType = null,
        [Description("Grouping: none, daily, weekly, or monthly. Defaults to none.")] string? groupBy = null,
        [Description("Optional provider source. Defaults to garmin.")] string? source = null)
    {
        var (fromDate, toDate) = ParseRange(from, to, MaximumRangeDays);
        var normalizedSource = NormalizeSource(source);
        var normalizedType = NormalizeActivityType(activityType);
        var normalizedGrouping = NormalizeGrouping(groupBy);
        ValidateGroupingRange(fromDate, toDate, normalizedGrouping);
        var fromTimestamp = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var exclusiveTo = new DateTimeOffset(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Activities
            .AsNoTracking()
            .Where(item => item.Source == normalizedSource && item.StartedAt >= fromTimestamp && item.StartedAt < exclusiveTo);
        if (normalizedType is not null)
        {
            query = query.Where(item => item.ActivityType == normalizedType);
        }

        var rows = await query.Select(item => new ActivityAggregateRow(
            item.Id,
            item.ActivityType,
            item.StartedAt,
            item.DurationSeconds,
            item.MovingDurationSeconds,
            item.DistanceMeters,
            item.ElevationGainMeters,
            item.ElevationLossMeters,
            item.Calories,
            item.Steps,
            item.AverageHeartRate)).ToListAsync(cancellationToken);
        var activityIds = rows.Select(row => row.Id).ToArray();
        var zones = activityIds.Length == 0
            ? []
            : await dbContext.ActivityHeartRateZones
                .AsNoTracking()
                .Where(zone => activityIds.Contains(zone.ActivityId))
                .Select(zone => new ZoneAggregateRow(zone.ActivityId, zone.ZoneNumber, zone.TimeSeconds))
                .ToListAsync(cancellationToken);

        var total = AggregateActivities(rows, zones);
        var byType = AggregateByType(rows, zones);
        var groups = normalizedGrouping == "none"
            ? []
            : GroupActivities(rows, zones, normalizedGrouping, fromDate, toDate);
        return new ActivitySummaryResult(
            normalizedSource, fromDate, toDate, normalizedType, normalizedGrouping, total, byType, groups);
    }

    [McpServerTool(Name = "get_trend", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns deterministic statistics and daily samples for a supported normalized metric.")]
    public static async Task<TrendResult> GetTrendAsync(
        [Description("Normalized daily metric. Supported health metrics: steps, resting_heart_rate, average_heart_rate, min_heart_rate, max_heart_rate, hrv, stress, body_battery_min, body_battery_max, sleep_score, calories, active_calories, moderate_intensity_minutes, vigorous_intensity_minutes, intensity_minutes, sleep_duration_seconds, deep_sleep_seconds, light_sleep_seconds, rem_sleep_seconds, awake_sleep_seconds, average_respiration_rate, average_spo2. Activity metrics use activity_ prefix: count, duration_seconds, moving_duration_seconds, distance_meters, elevation_gain_meters, elevation_loss_meters, calories, steps, average_heart_rate, training_load, aerobic_training_effect, anaerobic_training_effect, vo2_max.")] string metric,
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
        [Description("Any normalized metric supported by get_trend, including activity_* daily aggregates.")] string metric,
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
        if (metric.StartsWith("activity_", StringComparison.Ordinal))
        {
            var fromTimestamp = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var exclusiveTo = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var activities = await dbContext.Activities
                .AsNoTracking()
                .Where(item => item.Source == source && item.StartedAt >= fromTimestamp && item.StartedAt < exclusiveTo)
                .Select(item => new ActivityTrendRow(
                    item.StartedAt,
                    item.DurationSeconds,
                    item.MovingDurationSeconds,
                    item.DistanceMeters,
                    item.ElevationGainMeters,
                    item.ElevationLossMeters,
                    item.Calories,
                    item.Steps,
                    item.AverageHeartRate,
                    item.TrainingLoad,
                    item.AerobicTrainingEffect,
                    item.AnaerobicTrainingEffect,
                    item.Vo2Max))
                .ToListAsync(cancellationToken);

            var values = activities
                .GroupBy(item => DateOnly.FromDateTime(item.StartedAt.UtcDateTime))
                .OrderBy(group => group.Key)
                .Select(group => new TrendValue(group.Key, GetActivityMetricValue(group.ToArray(), metric)))
                .Where(item => !double.IsNaN(item.Value))
                .ToArray();
            if (metric != "activity_count")
            {
                return values;
            }

            var counts = values.ToDictionary(value => value.Date, value => value.Value);
            return Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
                .Select(offset => from.AddDays(offset))
                .Select(date => new TrendValue(date, counts.GetValueOrDefault(date)))
                .ToArray();
        }

        var rows = await dbContext.DailyMetrics
            .AsNoTracking()
            .Where(item => item.Source == source && item.Date >= from && item.Date <= to)
            .OrderBy(item => item.Date)
            .Select(item => new MetricRow(
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
                item.Calories,
                item.ActiveCalories,
                item.ModerateIntensityMinutes,
                item.VigorousIntensityMinutes,
                item.SleepDurationSeconds,
                item.DeepSleepSeconds,
                item.LightSleepSeconds,
                item.RemSleepSeconds,
                item.AwakeSleepSeconds,
                item.AverageRespirationRate,
                item.AverageSpo2))
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
        "average_heart_rate" => row.AverageHeartRate ?? double.NaN,
        "min_heart_rate" => row.MinHeartRate ?? double.NaN,
        "max_heart_rate" => row.MaxHeartRate ?? double.NaN,
        "hrv" => row.Hrv ?? double.NaN,
        "stress" => row.StressAverage ?? double.NaN,
        "body_battery_min" => row.BodyBatteryMin ?? double.NaN,
        "body_battery_max" => row.BodyBatteryMax ?? double.NaN,
        "sleep_score" => row.SleepScore ?? double.NaN,
        "calories" => row.Calories ?? double.NaN,
        "active_calories" => row.ActiveCalories ?? double.NaN,
        "moderate_intensity_minutes" => row.ModerateIntensityMinutes ?? double.NaN,
        "vigorous_intensity_minutes" => row.VigorousIntensityMinutes ?? double.NaN,
        "intensity_minutes" => SumNullable(row.ModerateIntensityMinutes, row.VigorousIntensityMinutes),
        "sleep_duration_seconds" => row.SleepDurationSeconds ?? double.NaN,
        "deep_sleep_seconds" => row.DeepSleepSeconds ?? double.NaN,
        "light_sleep_seconds" => row.LightSleepSeconds ?? double.NaN,
        "rem_sleep_seconds" => row.RemSleepSeconds ?? double.NaN,
        "awake_sleep_seconds" => row.AwakeSleepSeconds ?? double.NaN,
        "average_respiration_rate" => row.AverageRespirationRate ?? double.NaN,
        "average_spo2" => row.AverageSpo2 ?? double.NaN,
        _ => double.NaN
    };

    private static double GetActivityMetricValue(IReadOnlyList<ActivityTrendRow> rows, string metric) => metric switch
    {
        "activity_count" => rows.Count,
        "activity_duration_seconds" => SumOrNaN(rows.Select(row => row.DurationSeconds)),
        "activity_moving_duration_seconds" => SumOrNaN(rows.Select(row => row.MovingDurationSeconds)),
        "activity_distance_meters" => SumOrNaN(rows.Select(row => row.DistanceMeters)),
        "activity_elevation_gain_meters" => SumOrNaN(rows.Select(row => row.ElevationGainMeters)),
        "activity_elevation_loss_meters" => SumOrNaN(rows.Select(row => row.ElevationLossMeters)),
        "activity_calories" => SumOrNaN(rows.Select(row => row.Calories.HasValue ? (double?)row.Calories : null)),
        "activity_steps" => SumOrNaN(rows.Select(row => row.Steps.HasValue ? (double?)row.Steps : null)),
        "activity_average_heart_rate" => WeightedAverageHeartRate(rows.Select(row =>
            new HeartRateWeight(row.AverageHeartRate, row.DurationSeconds))),
        "activity_training_load" => SumOrNaN(rows.Select(row => row.TrainingLoad)),
        "activity_aerobic_training_effect" => AverageOrNaN(rows.Select(row => row.AerobicTrainingEffect)),
        "activity_anaerobic_training_effect" => AverageOrNaN(rows.Select(row => row.AnaerobicTrainingEffect)),
        "activity_vo2_max" => AverageOrNaN(rows.Select(row => row.Vo2Max)),
        _ => double.NaN
    };

    private static System.Linq.Expressions.Expression<Func<Activity, ActivityListResult>> ToActivityListResult() =>
        item => new ActivityListResult(
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

    private static System.Linq.Expressions.Expression<Func<Activity, ActivityResult>> ToActivityResult() =>
        item => new ActivityResult(
            item.Source,
            item.ExternalId,
            item.Name,
            item.ActivityType,
            item.StartedAt,
            item.DurationSeconds,
            item.ElapsedDurationSeconds,
            item.MovingDurationSeconds,
            item.DistanceMeters,
            item.Calories,
            item.AverageHeartRate,
            item.MaxHeartRate,
            item.ElevationGainMeters,
            item.ElevationLossMeters,
            item.AverageSpeedMetersPerSecond,
            item.MaxSpeedMetersPerSecond,
            item.AveragePaceSecondsPerKilometer,
            item.Steps,
            item.AverageCadence,
            item.MaxCadence,
            item.CadenceUnit,
            item.AveragePowerWatts,
            item.MaxPowerWatts,
            item.NormalizedPowerWatts,
            item.MinTemperatureCelsius,
            item.MaxTemperatureCelsius,
            item.AverageRespirationRate,
            item.MinRespirationRate,
            item.MaxRespirationRate,
            item.AverageSwolf,
            item.ActiveLengths,
            item.AerobicTrainingEffect,
            item.AnaerobicTrainingEffect,
            item.TrainingLoad,
            item.TrainingStressScore,
            item.IntensityFactor,
            item.Vo2Max,
            item.LapsSyncedAt != null,
            item.HeartRateZonesSyncedAt != null,
            item.StreamsSyncedAt != null,
            item.MinElevationMeters,
            item.MaxElevationMeters,
            item.MaxTwentyMinutePowerWatts,
            item.AverageVerticalOscillationMillimeters,
            item.AverageGroundContactTimeMilliseconds,
            item.AverageStrideLengthMeters,
            item.ParentExternalId,
            item.IsParent,
            new MetricSourceMetadata("garmin_api"),
            new MetricSourceMetadata("derived_by_openhealth", "pace-from-average-speed-v1"));

    private static void ValidateActivityId(string activityId)
    {
        if (string.IsNullOrWhiteSpace(activityId) || activityId.Length > 200)
        {
            throw new ArgumentException("activityId is required and cannot exceed 200 characters.", nameof(activityId));
        }
    }

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
        return normalized is
            "steps" or "resting_heart_rate" or "average_heart_rate" or "min_heart_rate" or "max_heart_rate" or
            "hrv" or "stress" or "body_battery_min" or "body_battery_max" or "sleep_score" or
            "calories" or "active_calories" or "moderate_intensity_minutes" or
            "vigorous_intensity_minutes" or "intensity_minutes" or "sleep_duration_seconds" or
            "deep_sleep_seconds" or "light_sleep_seconds" or "rem_sleep_seconds" or "awake_sleep_seconds" or
            "average_respiration_rate" or "average_spo2" or
            "activity_count" or "activity_duration_seconds" or "activity_moving_duration_seconds" or
            "activity_distance_meters" or "activity_elevation_gain_meters" or "activity_elevation_loss_meters" or
            "activity_calories" or "activity_steps" or "activity_average_heart_rate" or
            "activity_training_load" or "activity_aerobic_training_effect" or
            "activity_anaerobic_training_effect" or "activity_vo2_max"
            ? normalized
            : throw new ArgumentException("Unsupported metric.", nameof(metric));
    }

    private static string NormalizeTimelineMetric(string metric)
    {
        var normalized = metric?.Trim().ToLowerInvariant();
        return normalized is "heart_rate" or "stress" or "body_battery"
            ? normalized
            : throw new ArgumentException("metric must be heart_rate, stress, or body_battery.", nameof(metric));
    }

    private static string NormalizeGrouping(string? groupBy)
    {
        var normalized = string.IsNullOrWhiteSpace(groupBy) ? "none" : groupBy.Trim().ToLowerInvariant();
        return normalized is "none" or "daily" or "weekly" or "monthly"
            ? normalized
            : throw new ArgumentException("groupBy must be none, daily, weekly, or monthly.", nameof(groupBy));
    }

    private static void ValidateGroupingRange(DateOnly from, DateOnly to, string grouping)
    {
        if (grouping == "none")
        {
            return;
        }

        var first = GroupStart(from, grouping);
        var last = GroupStart(to, grouping);
        var groupCount = grouping switch
        {
            "daily" => last.DayNumber - first.DayNumber + 1,
            "weekly" => (last.DayNumber - first.DayNumber) / 7 + 1,
            "monthly" => (last.Year - first.Year) * 12 + last.Month - first.Month + 1,
            _ => 1
        };
        if (groupCount > MaximumActivitySummaryGroups)
        {
            throw new ArgumentException(
                $"The requested grouping would produce more than {MaximumActivitySummaryGroups} periods. " +
                "Use a coarser grouping or shorter range.",
                nameof(grouping));
        }
    }

    private static string? NormalizeActivityType(string? activityType)
    {
        if (string.IsNullOrWhiteSpace(activityType))
        {
            return null;
        }

        var normalized = activityType.Trim().ToLowerInvariant();
        if (normalized.Length > 100 || normalized.Any(character =>
                !char.IsLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException("activityType contains invalid characters.", nameof(activityType));
        }

        return normalized;
    }

    private static int ValidateSeriesLimit(int? maxPoints)
    {
        var effectiveLimit = maxPoints ?? DefaultSeriesPointLimit;
        if (effectiveLimit is < 2 or > MaximumSeriesPointLimit)
        {
            throw new ArgumentException(
                $"maxPoints must be between 2 and {MaximumSeriesPointLimit}.",
                nameof(maxPoints));
        }

        return effectiveLimit;
    }

    private static IReadOnlyList<string> ParseSelectedMetrics(string? metrics, IReadOnlyList<string> available)
    {
        if (string.IsNullOrWhiteSpace(metrics))
        {
            return available;
        }

        var selected = metrics.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(metric => metric.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (selected.Length == 0 || selected.Any(metric => !available.Contains(metric, StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                $"metrics contains an unavailable value. Available metrics: {string.Join(", ", available)}.",
                nameof(metrics));
        }

        return selected;
    }

    private static IReadOnlyList<ActivityStreamPoint> ParseActivityStreamSamples(
        JsonElement samples,
        IReadOnlyList<string> selectedMetrics,
        double? fromElapsedSeconds,
        double? toElapsedSeconds)
    {
        if (samples.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<ActivityStreamPoint>();
        foreach (var sample in samples.EnumerateArray())
        {
            var elapsed = GetJsonDouble(sample, "elapsedTimeSeconds");
            if (fromElapsedSeconds.HasValue && (!elapsed.HasValue || elapsed < fromElapsedSeconds) ||
                toElapsedSeconds.HasValue && (!elapsed.HasValue || elapsed > toElapsedSeconds))
            {
                continue;
            }

            var timestamp = GetJsonTimestamp(sample, "timestamp");
            var values = selectedMetrics
                .Select(metric => new { Metric = metric, Value = GetJsonDouble(sample, metric) })
                .Where(item => item.Value.HasValue)
                .ToDictionary(item => item.Metric, item => item.Value!.Value, StringComparer.Ordinal);
            if (values.Count > 0)
            {
                result.Add(new ActivityStreamPoint(timestamp, elapsed, values));
            }
        }

        return result;
    }

    private static IReadOnlyList<DailyTimelinePoint> ParseDailyTimelineSamples(JsonElement samples)
    {
        if (samples.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<DailyTimelinePoint>();
        foreach (var sample in samples.EnumerateArray())
        {
            var timestamp = GetJsonTimestamp(sample, "Timestamp") ?? GetJsonTimestamp(sample, "timestamp");
            var value = GetJsonDouble(sample, "Value") ?? GetJsonDouble(sample, "value");
            if (timestamp.HasValue && value.HasValue)
            {
                result.Add(new DailyTimelinePoint(timestamp.Value, value.Value));
            }
        }

        return result;
    }

    private static double? GetJsonDouble(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number) && double.IsFinite(number)
            ? number
            : null;

    private static DateTimeOffset? GetJsonTimestamp(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        value.TryGetDateTimeOffset(out var timestamp)
            ? timestamp
            : null;

    private static IReadOnlyList<T> Downsample<T>(IReadOnlyList<T> values, int maxPoints)
    {
        if (values.Count <= maxPoints)
        {
            return values;
        }

        var result = new List<T>(maxPoints);
        for (var index = 0; index < maxPoints; index++)
        {
            var sourceIndex = (int)Math.Round(index * (values.Count - 1d) / (maxPoints - 1));
            result.Add(values[sourceIndex]);
        }

        return result;
    }

    private static ActivitySummaryValues AggregateActivities(
        IReadOnlyList<ActivityAggregateRow> rows,
        IReadOnlyList<ZoneAggregateRow> zones) => new(
        rows.Count,
        SumNullable(rows.Select(row => row.DurationSeconds)),
        SumNullable(rows.Select(row => row.MovingDurationSeconds)),
        SumNullable(rows.Select(row => row.DistanceMeters)),
        SumNullable(rows.Select(row => row.ElevationGainMeters)),
        SumNullable(rows.Select(row => row.ElevationLossMeters)),
        SumNullableInt(rows.Select(row => row.Calories)),
        SumNullableInt(rows.Select(row => row.Steps)),
        NullIfNaN(WeightedAverageHeartRate(rows.Select(row =>
            new HeartRateWeight(row.AverageHeartRate, row.DurationSeconds)))),
        zones.GroupBy(zone => zone.ZoneNumber)
            .OrderBy(group => group.Key)
            .Select(group => new ActivitySummaryHeartRateZone(group.Key, group.Sum(zone => zone.TimeSeconds)))
            .ToArray());

    private static IReadOnlyList<ActivityTypeSummary> AggregateByType(
        IReadOnlyList<ActivityAggregateRow> rows,
        IReadOnlyList<ZoneAggregateRow> zones) => rows
        .GroupBy(row => row.ActivityType, StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group =>
        {
            var groupedRows = group.ToArray();
            var ids = groupedRows.Select(row => row.Id).ToHashSet();
            return new ActivityTypeSummary(
                group.Key,
                AggregateActivities(groupedRows, zones.Where(zone => ids.Contains(zone.ActivityId)).ToArray()));
        })
        .ToArray();

    private static IReadOnlyList<ActivitySummaryGroup> GroupActivities(
        IReadOnlyList<ActivityAggregateRow> rows,
        IReadOnlyList<ZoneAggregateRow> zones,
        string grouping,
        DateOnly requestedFrom,
        DateOnly requestedTo) => rows
        .GroupBy(row => GroupStart(DateOnly.FromDateTime(row.StartedAt.UtcDateTime), grouping))
        .OrderBy(group => group.Key)
        .Select(group =>
        {
            var groupedRows = group.ToArray();
            var ids = groupedRows.Select(row => row.Id).ToHashSet();
            var groupEnd = GroupEnd(group.Key, grouping);
            return new ActivitySummaryGroup(
                group.Key < requestedFrom ? requestedFrom : group.Key,
                groupEnd > requestedTo ? requestedTo : groupEnd,
                AggregateActivities(groupedRows, zones.Where(zone => ids.Contains(zone.ActivityId)).ToArray()),
                AggregateByType(groupedRows, zones.Where(zone => ids.Contains(zone.ActivityId)).ToArray()));
        })
        .ToArray();

    private static DateOnly GroupStart(DateOnly date, string grouping) => grouping switch
    {
        "daily" => date,
        "weekly" => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        "monthly" => new DateOnly(date.Year, date.Month, 1),
        _ => date
    };

    private static DateOnly GroupEnd(DateOnly start, string grouping) => grouping switch
    {
        "daily" => start,
        "weekly" => start.AddDays(6),
        "monthly" => start.AddMonths(1).AddDays(-1),
        _ => start
    };

    private static double? SumNullable(IEnumerable<double?> values)
    {
        var present = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return present.Length == 0 ? null : present.Sum();
    }

    private static int? SumNullableInt(IEnumerable<int?> values)
    {
        var present = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return present.Length == 0 ? null : present.Sum();
    }

    private static double SumNullable(int? left, int? right) =>
        left.HasValue || right.HasValue ? left.GetValueOrDefault() + right.GetValueOrDefault() : double.NaN;

    private static double SumOrNaN(IEnumerable<double?> values) => SumNullable(values) ?? double.NaN;

    private static double AverageOrNaN(IEnumerable<double?> values)
    {
        var present = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return present.Length == 0 ? double.NaN : present.Average();
    }

    private static double WeightedAverageHeartRate(IEnumerable<HeartRateWeight> values)
    {
        var present = values
            .Where(value => value.HeartRate.HasValue && value.DurationSeconds is > 0)
            .ToArray();
        return present.Length == 0
            ? double.NaN
            : present.Sum(value => value.HeartRate!.Value * value.DurationSeconds!.Value) /
              present.Sum(value => value.DurationSeconds!.Value);
    }

    private static double? NullIfNaN(double value) => double.IsNaN(value) ? null : value;

    private static double? Average(IReadOnlyList<TrendValue> values) =>
        values.Count == 0 ? null : values.Average(item => item.Value);

    private sealed record MetricRow(
        DateOnly Date,
        int? Steps,
        int? RestingHeartRate,
        int? AverageHeartRate,
        int? MinHeartRate,
        int? MaxHeartRate,
        double? Hrv,
        double? StressAverage,
        int? BodyBatteryMin,
        int? BodyBatteryMax,
        double? SleepScore,
        int? Calories,
        int? ActiveCalories,
        int? ModerateIntensityMinutes,
        int? VigorousIntensityMinutes,
        int? SleepDurationSeconds,
        int? DeepSleepSeconds,
        int? LightSleepSeconds,
        int? RemSleepSeconds,
        int? AwakeSleepSeconds,
        double? AverageRespirationRate,
        double? AverageSpo2);

    private sealed record ActivityAggregateRow(
        long Id,
        string ActivityType,
        DateTimeOffset StartedAt,
        double? DurationSeconds,
        double? MovingDurationSeconds,
        double? DistanceMeters,
        double? ElevationGainMeters,
        double? ElevationLossMeters,
        int? Calories,
        int? Steps,
        int? AverageHeartRate);

    private sealed record ZoneAggregateRow(long ActivityId, int ZoneNumber, double TimeSeconds);

    private sealed record ActivityTrendRow(
        DateTimeOffset StartedAt,
        double? DurationSeconds,
        double? MovingDurationSeconds,
        double? DistanceMeters,
        double? ElevationGainMeters,
        double? ElevationLossMeters,
        int? Calories,
        int? Steps,
        int? AverageHeartRate,
        double? TrainingLoad,
        double? AerobicTrainingEffect,
        double? AnaerobicTrainingEffect,
        double? Vo2Max);

    private sealed record HeartRateWeight(int? HeartRate, double? DurationSeconds);
}