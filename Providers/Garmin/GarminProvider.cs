using System.Text.Json;
using Garmin.Connect.Models;
using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data;
using OpenHealthMCP.Data.Entities;

namespace OpenHealthMCP.Providers.Garmin;

internal sealed class GarminProvider(
    IDbContextFactory<AppDbContext> dbContextFactory,
    GarminClientSession session,
    GarminRawPayloadCollector payloadCollector,
    ILogger<GarminProvider> logger) : IHealthDataProvider
{
    private const string SourceName = "garmin";
    private int _authenticationLogged;

    public string Name => SourceName;

    public async Task SyncAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (from > to)
        {
            throw new ArgumentException("The synchronization start date must not be after the end date.");
        }

        logger.LogInformation("Garmin sync started for {From} through {To}", from, to);
        var failures = new List<Exception>();
        var dailyUpdates = 0;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            dailyUpdates += await RunUnitAsync(
                $"daily summary for {date}",
                () => SyncDailySummaryAsync(date, cancellationToken),
                failures,
                cancellationToken);

            dailyUpdates += await RunUnitAsync(
                $"heart rate for {date}",
                () => SyncHeartRateAsync(date, cancellationToken),
                failures,
                cancellationToken);

            dailyUpdates += await RunUnitAsync(
                $"sleep for {date}",
                () => SyncSleepAsync(date, cancellationToken),
                failures,
                cancellationToken);

            dailyUpdates += await RunUnitAsync(
                $"HRV for {date}",
                () => SyncHrvAsync(date, cancellationToken),
                failures,
                cancellationToken);
        }

        var activityUpdates = await RunUnitAsync(
            $"activities for {from} through {to}",
            () => SyncActivitiesAsync(from, to, cancellationToken),
            failures,
            cancellationToken);

        logger.LogInformation(
            "Garmin sync persisted {DailyUpdates} daily segments and {ActivityUpdates} activities",
            dailyUpdates,
            activityUpdates);

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"Garmin synchronization completed with {failures.Count} failed data unit(s).",
                failures);
        }
    }

    private async Task<int> SyncDailySummaryAsync(DateOnly date, CancellationToken cancellationToken)
    {
        using var capture = payloadCollector.BeginCapture();
        var summary = await session.Client.GetUserSummary(date.ToDateTime(TimeOnly.MinValue), cancellationToken);
        LogAuthenticationSucceeded();
        var payload = RequirePayload(capture, "daily summary");
        using var document = JsonDocument.Parse(payload.Payload);
        var root = document.RootElement;

        await UpsertDailySegmentAsync(
            date,
            "daily_summary",
            payload.Payload,
            metric =>
            {
                metric.Steps = GetInt32(root, "totalSteps");
                metric.RestingHeartRate = GetInt32(root, "restingHeartRate");
                metric.MinHeartRate = GetInt32(root, "minHeartRate");
                metric.MaxHeartRate = GetInt32(root, "maxHeartRate");
                metric.StressAverage = GetDouble(root, "averageStressLevel");
                metric.BodyBatteryMin = GetInt32(root, "bodyBatteryLowestValue");
                metric.BodyBatteryMax = GetInt32(root, "bodyBatteryHighestValue");
                metric.Calories = GetInt32(root, "totalKilocalories");
            },
            cancellationToken);

        return summary is null ? 0 : 1;
    }

    private async Task<int> SyncHeartRateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        using var capture = payloadCollector.BeginCapture();
        var heartRate = await session.Client.GetWellnessHeartRates(date.ToDateTime(TimeOnly.MinValue), cancellationToken);
        var payload = RequirePayload(capture, "heart rate");
        using var document = JsonDocument.Parse(payload.Payload);
        var root = document.RootElement;

        int? averageHeartRate = null;
        if (heartRate?.HeartRateValues is { Length: > 0 })
        {
            var measuredValues = heartRate.HeartRateValues
                .Where(value => value.Length > 1 && value[1] > 0)
                .Select(value => value[1])
                .ToArray();

            if (measuredValues.Length > 0)
            {
                averageHeartRate = Convert.ToInt32(Math.Round(measuredValues.Average()));
            }
        }

        await UpsertDailySegmentAsync(
            date,
            "heart_rate",
            payload.Payload,
            metric =>
            {
                metric.RestingHeartRate = GetInt32(root, "restingHeartRate");
                metric.MinHeartRate = GetInt32(root, "minHeartRate");
                metric.MaxHeartRate = GetInt32(root, "maxHeartRate");
                metric.AverageHeartRate = averageHeartRate;
            },
            cancellationToken);

        return 1;
    }

    private async Task<int> SyncSleepAsync(DateOnly date, CancellationToken cancellationToken)
    {
        using var capture = payloadCollector.BeginCapture();
        await session.Client.GetWellnessSleepData(date.ToDateTime(TimeOnly.MinValue), cancellationToken);
        var payload = RequirePayload(capture, "sleep");
        using var document = JsonDocument.Parse(payload.Payload);

        double? sleepScore = null;
        if (TryGetProperty(document.RootElement, "dailySleepDTO", out var dailySleep) &&
            TryGetProperty(dailySleep, "sleepScores", out var sleepScores) &&
            TryGetProperty(sleepScores, "overall", out var overall))
        {
            sleepScore = GetDouble(overall, "value");
        }

        await UpsertDailySegmentAsync(
            date,
            "sleep",
            payload.Payload,
            metric => metric.SleepScore = sleepScore,
            cancellationToken);

        return 1;
    }

    private async Task<int> SyncHrvAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var asDateTime = date.ToDateTime(TimeOnly.MinValue);
        using var capture = payloadCollector.BeginCapture();
        var report = await session.Client.GetReportHrvStatus(asDateTime, asDateTime, cancellationToken);
        var payload = RequirePayload(capture, "HRV");
        var summary = report?.HrvSummaries?.FirstOrDefault(item => item.CalendarDate == date);

        await UpsertDailySegmentAsync(
            date,
            "hrv",
            payload.Payload,
            metric => metric.Hrv = summary is null ? null : summary.LastNightAvg,
            cancellationToken);

        return 1;
    }

    private async Task<int> SyncActivitiesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        using var capture = payloadCollector.BeginCapture();
        var activities = await session.Client.GetActivitiesByDate(
            from.ToDateTime(TimeOnly.MinValue),
            to.ToDateTime(TimeOnly.MaxValue),
            string.Empty,
            cancellationToken);
        var payload = RequirePayload(capture, "activities");

        using var document = JsonDocument.Parse(payload.Payload);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Garmin activities response was not a JSON array.");
        }

        var rawById = document.RootElement
            .EnumerateArray()
            .Where(element => element.TryGetProperty("activityId", out _))
            .ToDictionary(
                element => element.GetProperty("activityId").ToString(),
                element => element.GetRawText(),
                StringComparer.Ordinal);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var count = 0;

        foreach (var garminActivity in activities ?? [])
        {
            var externalId = garminActivity.ActivityId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!rawById.TryGetValue(externalId, out var rawJson))
            {
                throw new JsonException($"Garmin activity {externalId} was missing from its raw response.");
            }

            var activity = await dbContext.Activities.SingleOrDefaultAsync(
                item => item.Source == SourceName && item.ExternalId == externalId,
                cancellationToken);

            if (activity is null)
            {
                activity = new Activity
                {
                    Source = SourceName,
                    ExternalId = externalId,
                    Name = garminActivity.ActivityName ?? "Unnamed activity",
                    ActivityType = GetActivityType(garminActivity, rawJson),
                    StartedAt = ToUtcOffset(garminActivity.StartTimeGmt),
                    CreatedAt = now,
                    UpdatedAt = now
                };
                dbContext.Activities.Add(activity);
            }

            activity.Name = garminActivity.ActivityName ?? "Unnamed activity";
            activity.ActivityType = GetActivityType(garminActivity, rawJson);
            activity.StartedAt = ToUtcOffset(garminActivity.StartTimeGmt);
            activity.DurationSeconds = GetPositiveOrNull(garminActivity.Duration);
            activity.DistanceMeters = GetPositiveOrNull(garminActivity.Distance);
            activity.Calories = GetPositiveIntOrNull(garminActivity.Calories);
            activity.AverageHeartRate = GetPositiveIntOrNull(garminActivity.AverageHr);
            activity.MaxHeartRate = GetPositiveIntOrNull(garminActivity.MaxHr);
            activity.ElevationGainMeters = GetPositiveOrNull(garminActivity.ElevationGain);
            activity.UpdatedAt = now;

            await UpsertRawAsync(
                dbContext,
                "activity",
                externalId,
                JsonDocument.Parse(rawJson),
                now,
                cancellationToken);
            count++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task UpsertDailySegmentAsync(
        DateOnly date,
        string dataType,
        byte[] payload,
        Action<DailyMetric> update,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var metric = await dbContext.DailyMetrics.SingleOrDefaultAsync(
            item => item.Source == SourceName && item.Date == date,
            cancellationToken);

        if (metric is null)
        {
            metric = new DailyMetric
            {
                Source = SourceName,
                Date = date,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.DailyMetrics.Add(metric);
        }

        update(metric);
        metric.UpdatedAt = now;

        await UpsertRawAsync(
            dbContext,
            dataType,
            date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            JsonDocument.Parse(payload),
            now,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertRawAsync(
        AppDbContext dbContext,
        string dataType,
        string externalId,
        JsonDocument payload,
        DateTimeOffset fetchedAt,
        CancellationToken cancellationToken)
    {
        var raw = await dbContext.RawProviderData.SingleOrDefaultAsync(
            item => item.Source == SourceName &&
                    item.DataType == dataType &&
                    item.ExternalId == externalId,
            cancellationToken);

        if (raw is null)
        {
            dbContext.RawProviderData.Add(new RawProviderData
            {
                Source = SourceName,
                DataType = dataType,
                ExternalId = externalId,
                FetchedAt = fetchedAt,
                Payload = payload
            });
            return;
        }

        raw.Payload = payload;
        raw.FetchedAt = fetchedAt;
    }

    private async Task<int> RunUnitAsync(
        string unit,
        Func<Task<int>> action,
        ICollection<Exception> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Garmin synchronization unit failed: {Unit}. {Error}", unit, SafeError(exception));
            failures.Add(new InvalidOperationException($"Failed to synchronize {unit}: {SafeError(exception)}"));
            return 0;
        }
    }

    private void LogAuthenticationSucceeded()
    {
        if (Interlocked.Exchange(ref _authenticationLogged, 1) == 0)
        {
            logger.LogInformation("Garmin authentication succeeded");
        }
    }

    private static CapturedGarminPayload RequirePayload(
        GarminRawPayloadCollector.CaptureScope capture,
        string dataType) => capture.Last ?? throw new InvalidOperationException(
        $"Garmin returned {dataType} without a capturable JSON payload.");

    private static string SafeError(Exception exception) => exception switch
    {
        AggregateException aggregate => aggregate.GetBaseException().Message,
        _ => exception.Message
    };

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            return element.TryGetProperty(propertyName, out value);
        }

        value = default;
        return false;
    }

    private static int? GetInt32(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt32(out var integer)
            ? integer
            : Convert.ToInt32(Math.Round(value.GetDouble()));
    }

    private static double? GetDouble(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static string GetActivityType(GarminActivity activity, string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        if (TryGetProperty(document.RootElement, "activityType", out var type) &&
            TryGetProperty(type, "typeKey", out var typeKey) &&
            typeKey.ValueKind == JsonValueKind.String)
        {
            return typeKey.GetString() ?? "unknown";
        }

        return activity.ActivityType?.TypeKey ?? "unknown";
    }

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static double? GetPositiveOrNull(double value) => value > 0 ? value : null;

    private static int? GetPositiveIntOrNull(double value) =>
        value > 0 ? Convert.ToInt32(Math.Round(value)) : null;
}