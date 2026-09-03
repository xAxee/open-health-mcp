using System.Text.Json;
using System.Security.Cryptography;
using Garmin.Connect.Models;
using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data;
using OpenHealthMCP.Data.Entities;

namespace OpenHealthMCP.Providers.Garmin;

internal sealed class GarminProvider(
    IDbContextFactory<AppDbContext> dbContextFactory,
    GarminClientSession session,
    GarminRawPayloadCollector payloadCollector,
    GarminOptions options,
    ILogger<GarminProvider> logger) : IHealthDataProvider
{
    private const string SourceName = "garmin";
    private const int MaximumActivityStreamSamples = 2000;
    private const int ActivityStreamBackfillDays = 365;
    private const string ParserVersion = "garmin-v1";
    private int _authenticationLogged;

    public string Name => SourceName;

    public async Task SyncAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException(
                "Garmin is not configured. Set GARMIN_EMAIL and GARMIN_PASSWORD.");
        }

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
                $"daily timelines for {date}",
                () => SyncDailyTimelinesAsync(date, cancellationToken),
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

            dailyUpdates += await RunUnitAsync(
                $"SpO2 for {date}",
                () => SyncSpo2Async(date, cancellationToken),
                failures,
                cancellationToken);

            dailyUpdates += await RunUnitAsync(
                $"respiration for {date}",
                () => SyncRespirationAsync(date, cancellationToken),
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
        var payload = await FetchPayloadAsync(
            "daily summary",
            "/usersummary-service/usersummary/daily/",
            () => session.Client.GetUserSummary(date.ToDateTime(TimeOnly.MinValue), cancellationToken),
            cancellationToken);
        if (payload is null)
        {
            return 0;
        }

        using var document = JsonDocument.Parse(payload.Payload);
        var root = document.RootElement;
        var summary = GarminDailyPayloadParser.ParseSummary(root);

        await UpsertDailySegmentAsync(
            date,
            "daily",
            payload,
            metric =>
            {
                metric.Steps = GetInt32(root, "totalSteps");
                metric.UtcOffsetMinutes = summary.UtcOffsetMinutes;
                metric.DistanceMeters = summary.DistanceMeters;
                metric.ActiveSeconds = summary.ActiveSeconds;
                metric.RestingHeartRate = GetInt32(root, "restingHeartRate");
                metric.MinHeartRate = GetInt32(root, "minHeartRate");
                metric.MaxHeartRate = GetInt32(root, "maxHeartRate");
                metric.StressAverage = GetDouble(root, "averageStressLevel");
                metric.StressMax = summary.StressMax;
                metric.StressQualifier = summary.StressQualifier;
                metric.RestStressSeconds = summary.RestStressSeconds;
                metric.LowStressSeconds = summary.LowStressSeconds;
                metric.MediumStressSeconds = summary.MediumStressSeconds;
                metric.HighStressSeconds = summary.HighStressSeconds;
                metric.ActivityStressSeconds = summary.ActivityStressSeconds;
                metric.RestStressPercentage = summary.RestStressPercentage;
                metric.LowStressPercentage = summary.LowStressPercentage;
                metric.MediumStressPercentage = summary.MediumStressPercentage;
                metric.HighStressPercentage = summary.HighStressPercentage;
                metric.BodyBatteryMin = GetInt32(root, "bodyBatteryLowestValue");
                metric.BodyBatteryMax = GetInt32(root, "bodyBatteryHighestValue");
                metric.BodyBatteryCharged = summary.BodyBatteryCharged;
                metric.BodyBatteryDrained = summary.BodyBatteryDrained;
                metric.BodyBatteryMostRecent = summary.BodyBatteryMostRecent;
                metric.Calories = GetInt32(root, "totalKilocalories");
                metric.ActiveCalories = GetInt32(root, "activeKilocalories");
                metric.BmrCalories = summary.BmrCalories;
                metric.StepsGoal = summary.StepsGoal;
                metric.FloorsGoal = summary.FloorsGoal;
                metric.IntensityGoal = summary.IntensityGoal;
                metric.FloorsClimbed = summary.FloorsClimbed;
                metric.ModerateIntensityMinutes = GetInt32(root, "moderateIntensityMinutes");
                metric.VigorousIntensityMinutes = GetInt32(root, "vigorousIntensityMinutes");
                metric.TotalIntensityMinutes = summary.TotalIntensityMinutes;
                metric.AverageRespirationRate = GetDouble(root, "avgWakingRespirationValue");
                metric.MinimumRespirationRate = summary.MinimumRespirationRate;
                metric.MaximumRespirationRate = summary.MaximumRespirationRate;
                metric.AverageSpo2 = GetDouble(root, "averageSpo2");
                metric.MinimumSpo2 = summary.MinimumSpo2;
                metric.LatestSpo2 = summary.LatestSpo2;
                metric.WellnessStartUtc = summary.WellnessStartUtc;
                metric.WellnessEndUtc = summary.WellnessEndUtc;
                metric.WellnessStartLocal = summary.WellnessStartLocal;
                metric.WellnessEndLocal = summary.WellnessEndLocal;
            },
            cancellationToken);

        return 1;
    }

    private async Task<int> SyncHeartRateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var payload = await FetchPayloadAsync(
            "heart rate",
            "/wellness-service/wellness/dailyHeartRate/",
            () => session.Client.GetWellnessHeartRates(date.ToDateTime(TimeOnly.MinValue), cancellationToken),
            cancellationToken);
        if (payload is null)
        {
            return 0;
        }

        using var document = JsonDocument.Parse(payload.Payload);
        var root = document.RootElement;
        var averageHeartRate = GetAverageHeartRate(root);

        await UpsertDailySegmentAsync(
            date,
            "heart_rate",
            payload,
            metric =>
            {
                metric.RestingHeartRate = GetInt32(root, "restingHeartRate");
                metric.MinHeartRate = GetInt32(root, "minHeartRate");
                metric.MaxHeartRate = GetInt32(root, "maxHeartRate");
                metric.AverageHeartRate = averageHeartRate;
            },
            cancellationToken);

        var timeline = GarminTimeSeriesPayloadParser.ParseDescriptorTimeline(
            root,
            "heartRateValueDescriptors",
            "heartRateValues",
            "timestamp",
            "heartRate");
        await UpsertTimelineAsync(date, "heart_rate", "bpm", timeline, cancellationToken);

        return 1;
    }

    private async Task<int> SyncDailyTimelinesAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var asDateTime = date.ToDateTime(TimeOnly.MinValue);
        var payload = await FetchPayloadAsync(
            "daily stress and Body Battery",
            "/wellness-service/wellness/dailyStress/",
            () => session.Client.GetAllDayStress(asDateTime, cancellationToken),
            cancellationToken);
        if (payload is null)
        {
            return 0;
        }

        using var document = JsonDocument.Parse(payload.Payload);
        var root = document.RootElement;
        var stress = GarminTimeSeriesPayloadParser.ParseDescriptorTimeline(
            root,
            "stressValueDescriptorsDTOList",
            "stressValuesArray",
            "timestamp",
            "stressLevel");
        var bodyBattery = GarminTimeSeriesPayloadParser.ParseDescriptorTimeline(
            root,
            "bodyBatteryValueDescriptorsDTOList",
            "bodyBatteryValuesArray",
            "timestamp",
            "bodyBatteryLevel");

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await UpsertTimelineAsync(dbContext, date, "stress", "score", stress, now, cancellationToken);
        await UpsertTimelineAsync(dbContext, date, "body_battery", "score", bodyBattery, now, cancellationToken);
        await UpsertRawAsync(
            dbContext,
            "daily_timeline",
            date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            payload,
            now,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return 1;
    }

    private async Task<int> SyncSleepAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var payload = await FetchPayloadAsync(
            "sleep",
            "/wellness-service/wellness/dailySleepData/",
            () => session.Client.GetWellnessSleepData(date.ToDateTime(TimeOnly.MinValue), cancellationToken),
            cancellationToken);
        if (payload is null)
        {
            return 0;
        }

        using var document = JsonDocument.Parse(payload.Payload);
        var parsedSleep = GarminDailyPayloadParser.ParseSleep(document.RootElement);
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
            payload,
            metric =>
            {
                metric.SleepScore = sleepScore;
                if (TryGetProperty(document.RootElement, "dailySleepDTO", out var dailySleep))
                {
                    metric.SleepDurationSeconds = GetInt32(dailySleep, "sleepTimeSeconds");
                    metric.DeepSleepSeconds = GetInt32(dailySleep, "deepSleepSeconds");
                    metric.LightSleepSeconds = GetInt32(dailySleep, "lightSleepSeconds");
                    metric.RemSleepSeconds = GetInt32(dailySleep, "remSleepSeconds");
                    metric.AwakeSleepSeconds = GetInt32(dailySleep, "awakeSleepSeconds");
                }

                metric.NapDurationSeconds = parsedSleep.NapDurationSeconds;
                metric.UnmeasurableSleepSeconds = parsedSleep.UnmeasurableSleepSeconds;
                metric.SleepStartUtc = parsedSleep.SleepStartUtc;
                metric.SleepEndUtc = parsedSleep.SleepEndUtc;
                metric.SleepStartLocal = parsedSleep.SleepStartLocal;
                metric.SleepEndLocal = parsedSleep.SleepEndLocal;
                metric.SleepQualifier = parsedSleep.SleepQualifier;
                metric.SleepAwakeCount = parsedSleep.AwakeCount;
                metric.AverageSleepStress = parsedSleep.AverageSleepStress;
                metric.AverageSleepRespirationRate = parsedSleep.AverageSleepRespirationRate;
                metric.SleepSubScoresJson = parsedSleep.SubScoresJson;
                metric.UtcOffsetMinutes ??= parsedSleep.UtcOffsetMinutes;
            },
            cancellationToken);

        await ReplaceSleepSeriesAsync(date, document.RootElement, cancellationToken);

        return 1;
    }

    private async Task<int> SyncHrvAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var asDateTime = date.ToDateTime(TimeOnly.MinValue);
        var payload = await FetchPayloadAsync(
            "HRV",
            "/hrv-service/hrv/daily/",
            () => session.Client.GetReportHrvStatus(asDateTime, asDateTime, cancellationToken),
            cancellationToken);
        if (payload is null)
        {
            return 0;
        }

        using var document = JsonDocument.Parse(payload.Payload);
        var hrv = GarminDailyPayloadParser.ParseHrv(document.RootElement, date);

        await UpsertDailySegmentAsync(
            date,
            "hrv",
            payload,
            metric =>
            {
                metric.Hrv = hrv.LastNightAverage;
                metric.HrvFiveMinuteHigh = hrv.FiveMinuteHigh;
                metric.HrvStatus = hrv.Status;
                metric.HrvCreatedAt = hrv.CreatedAt;
            },
            cancellationToken);

        return 1;
    }

    private async Task<int> SyncSpo2Async(DateOnly date, CancellationToken cancellationToken)
    {
        var payload = await FetchPayloadAsync(
            "SpO2",
            "/wellness-service/wellness/daily/spo2/",
            () => session.Client.GetSpo2Data(date.ToDateTime(TimeOnly.MinValue), cancellationToken),
            cancellationToken);
        if (payload is null)
        {
            return 0;
        }

        using var document = JsonDocument.Parse(payload.Payload);
        var spo2 = GarminDailyPayloadParser.ParseSpo2(document.RootElement);
        await UpsertDailySegmentAsync(
            date,
            "spo2",
            payload,
            metric =>
            {
                metric.AverageSpo2 = spo2.Average ?? metric.AverageSpo2;
                metric.MinimumSpo2 = spo2.Minimum ?? metric.MinimumSpo2;
                metric.LatestSpo2 = spo2.Latest ?? metric.LatestSpo2;
                metric.AverageSleepSpo2 = spo2.SleepAverage;
                metric.Spo2WindowStartUtc = spo2.WindowStartUtc;
                metric.Spo2WindowEndUtc = spo2.WindowEndUtc;
            },
            cancellationToken);
        return 1;
    }

    private async Task<int> SyncRespirationAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var payload = await FetchPayloadAsync(
            "respiration",
            "/wellness-service/wellness/daily/respiration/",
            () => session.Client.GetRespirationData(date.ToDateTime(TimeOnly.MinValue), cancellationToken),
            cancellationToken);
        if (payload is null)
        {
            return 0;
        }

        using var document = JsonDocument.Parse(payload.Payload);
        var respiration = GarminDailyPayloadParser.ParseRespiration(document.RootElement);
        await UpsertDailySegmentAsync(
            date,
            "respiration",
            payload,
            metric =>
            {
                metric.AverageRespirationRate = respiration.WakingAverage ?? metric.AverageRespirationRate;
                metric.AverageSleepRespirationRate =
                    respiration.SleepAverage ?? metric.AverageSleepRespirationRate;
                metric.MinimumRespirationRate = respiration.Minimum ?? metric.MinimumRespirationRate;
                metric.MaximumRespirationRate = respiration.Maximum ?? metric.MaximumRespirationRate;
            },
            cancellationToken);

        var timeline = GarminTimeSeriesPayloadParser.ParseDescriptorTimeline(
            document.RootElement,
            "respirationValueDescriptorsDTOList",
            "respirationValuesArray",
            "timestamp",
            "respirationValue");
        await UpsertTimelineAsync(date, "respiration", "breaths_per_minute", timeline, cancellationToken);
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
        var activityPayloads = capture.Payloads
            .Where(item => item.RequestUri.AbsolutePath.Contains(
                "/activitylist-service/activities/search/activities",
                StringComparison.Ordinal))
            .ToArray();
        capture.Dispose();
        if (activityPayloads.Length == 0 && activities is { Length: > 0 })
        {
            throw new InvalidOperationException(
                "Garmin returned activities without capturable JSON payloads.");
        }

        var rawById = new Dictionary<string, RawActivityPayload>(StringComparer.Ordinal);
        foreach (var payload in activityPayloads)
        {
            using var document = JsonDocument.Parse(payload.Payload);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("Garmin activities response was not a JSON array.");
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("activityId", out var activityId))
                {
                    rawById[activityId.ToString()] = new RawActivityPayload(element.GetRawText(), payload);
                }
            }
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var count = 0;
        var summariesByExternalId = new Dictionary<string, GarminActivitySummaryData>(StringComparer.Ordinal);

        foreach (var garminActivity in activities ?? [])
        {
            var externalId = garminActivity.ActivityId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!rawById.TryGetValue(externalId, out var rawPayload))
            {
                throw new JsonException($"Garmin activity {externalId} was missing from its raw response.");
            }

            var rawJson = rawPayload.Json;
            using var rawDocument = JsonDocument.Parse(rawJson);
            var rawActivity = rawDocument.RootElement;
            var summary = GarminActivityPayloadParser.ParseSummary(rawActivity);
            summariesByExternalId[externalId] = summary;

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
            ApplySummary(activity, summary);
            activity.UpdatedAt = now;

            await UpsertRawAsync(
                dbContext,
                "activity",
                externalId,
                new CapturedGarminPayload(
                    rawPayload.Response.RequestUri,
                    rawPayload.Response.StatusCode,
                    System.Text.Encoding.UTF8.GetBytes(rawJson)),
                now,
                cancellationToken);
            count++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnrichActivitiesAsync(summariesByExternalId, cancellationToken);
        return count;
    }

    private async Task EnrichActivitiesAsync(
        IReadOnlyDictionary<string, GarminActivitySummaryData> summariesByExternalId,
        CancellationToken cancellationToken)
    {
        if (options.ActivityEnrichmentLimit == 0 || summariesByExternalId.Count == 0)
        {
            return;
        }

        var externalIds = summariesByExternalId.Keys.ToArray();
        var now = DateTimeOffset.UtcNow;
        var recentActivityThreshold = now.AddDays(-7);
        var refreshThreshold = now.AddHours(-24);
        await using var lookupContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await lookupContext.Activities
            .AsNoTracking()
            .Where(item => item.Source == SourceName &&
                           externalIds.Contains(item.ExternalId) &&
                           (item.LapsSyncedAt == null ||
                            item.HeartRateZonesSyncedAt == null ||
                            item.StartedAt >= now.AddDays(-ActivityStreamBackfillDays) && item.StreamsSyncedAt == null ||
                            item.StartedAt >= recentActivityThreshold &&
                            (item.LapsSyncedAt < refreshThreshold ||
                             item.HeartRateZonesSyncedAt < refreshThreshold ||
                             item.StreamsSyncedAt < refreshThreshold)))
            .OrderByDescending(item => item.StartedAt)
            .Take(options.ActivityEnrichmentLimit)
            .ToListAsync(cancellationToken);

        foreach (var activity in candidates)
        {
            var summary = summariesByExternalId[activity.ExternalId];
            var refreshRecent = activity.StartedAt >= recentActivityThreshold;
            if (activity.LapsSyncedAt is null ||
                refreshRecent && activity.LapsSyncedAt < refreshThreshold)
            {
                await using var lapsContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var lapsActivity = await lapsContext.Activities.SingleAsync(
                    item => item.Id == activity.Id,
                    cancellationToken);
                if (summary.HasSplits == false)
                {
                    var existingLaps = await lapsContext.ActivityLaps
                        .Where(item => item.ActivityId == lapsActivity.Id)
                        .ToListAsync(cancellationToken);
                    lapsContext.ActivityLaps.RemoveRange(existingLaps);
                    lapsActivity.LapsSyncedAt = DateTimeOffset.UtcNow;
                    await lapsContext.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    await TrySyncActivityLapsAsync(lapsContext, lapsActivity, cancellationToken);
                    await DelayEnrichmentAsync(cancellationToken);
                }
            }

            if (activity.HeartRateZonesSyncedAt is null ||
                refreshRecent && activity.HeartRateZonesSyncedAt < refreshThreshold)
            {
                await using var zonesContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var zonesActivity = await zonesContext.Activities.SingleAsync(
                    item => item.Id == activity.Id,
                    cancellationToken);
                if (!summary.AverageHeartRate.HasValue && !summary.MaxHeartRate.HasValue)
                {
                    zonesActivity.HeartRateZonesSyncedAt = DateTimeOffset.UtcNow;
                    await zonesContext.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    await TrySyncActivityHeartRateZonesAsync(zonesContext, zonesActivity, cancellationToken);
                    await DelayEnrichmentAsync(cancellationToken);
                }
            }

            if (activity.StartedAt >= now.AddDays(-ActivityStreamBackfillDays) &&
                (activity.StreamsSyncedAt is null || refreshRecent && activity.StreamsSyncedAt < refreshThreshold))
            {
                await using var streamContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var streamActivity = await streamContext.Activities.SingleAsync(
                    item => item.Id == activity.Id,
                    cancellationToken);
                await TrySyncActivityStreamAsync(streamContext, streamActivity, cancellationToken);
                await DelayEnrichmentAsync(cancellationToken);
            }
        }
    }

    private async Task TrySyncActivityStreamAsync(
        AppDbContext dbContext,
        Activity activity,
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = $"/activity-service/activity/{activity.ExternalId}/details";
            var payload = await FetchPayloadAsync(
                "activity details",
                endpoint,
                () => session.Client.GetActivityDetails(
                    long.Parse(activity.ExternalId, System.Globalization.CultureInfo.InvariantCulture),
                    MaximumActivityStreamSamples,
                    0,
                    cancellationToken),
                cancellationToken);
            if (payload is null)
            {
                activity.StreamsSyncedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            using var document = JsonDocument.Parse(payload.Payload);
            var parsed = GarminTimeSeriesPayloadParser.ParseActivityStream(document.RootElement, activity.StartedAt);
            var stream = await dbContext.ActivityStreams.SingleOrDefaultAsync(
                item => item.ActivityId == activity.Id,
                cancellationToken);
            var now = DateTimeOffset.UtcNow;
            if (stream is null)
            {
                stream = new ActivityStream
                {
                    Activity = activity,
                    ActivityId = activity.Id,
                    Samples = parsed.Samples,
                    UpdatedAt = now
                };
                dbContext.ActivityStreams.Add(stream);
            }
            else
            {
                stream.Samples = parsed.Samples;
                stream.UpdatedAt = now;
            }

            stream.SampleCount = parsed.SampleCount;
            stream.AvailableMetrics = parsed.AvailableMetrics;
            await ReplaceActivitySamplesAsync(dbContext, activity, parsed.Points, now, cancellationToken);
            activity.StreamsSyncedAt = now;
            await UpsertRawAsync(
                dbContext,
                "activity_details",
                activity.ExternalId,
                payload,
                now,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (IsUnsupportedEnrichment(exception))
            {
                activity.StreamsSyncedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            HandleEnrichmentFailure(activity.ExternalId, "streams", exception);
        }
    }

    private async Task TrySyncActivityLapsAsync(
        AppDbContext dbContext,
        Activity activity,
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = $"/activity-service/activity/{activity.ExternalId}/splits";
            var payload = await FetchPayloadAsync(
                "activity splits",
                endpoint,
                () => session.Client.GetActivitySplits(
                    long.Parse(activity.ExternalId, System.Globalization.CultureInfo.InvariantCulture),
                    cancellationToken),
                cancellationToken);
            if (payload is null)
            {
                activity.LapsSyncedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            using var document = JsonDocument.Parse(payload.Payload);
            var laps = GarminActivityPayloadParser.ParseLaps(document.RootElement);
            var existing = await dbContext.ActivityLaps
                .Where(item => item.ActivityId == activity.Id)
                .ToListAsync(cancellationToken);
            dbContext.ActivityLaps.RemoveRange(existing);
            dbContext.ActivityLaps.AddRange(laps.Select(lap => ToEntity(activity, lap)));

            var now = DateTimeOffset.UtcNow;
            activity.LapsSyncedAt = now;
            await UpsertRawAsync(
                dbContext,
                "activity_splits",
                activity.ExternalId,
                payload,
                now,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (IsUnsupportedEnrichment(exception))
            {
                activity.LapsSyncedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            HandleEnrichmentFailure(activity.ExternalId, "splits", exception);
        }
    }

    private async Task TrySyncActivityHeartRateZonesAsync(
        AppDbContext dbContext,
        Activity activity,
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = $"/activity-service/activity/{activity.ExternalId}/hrTimeInZones";
            var payload = await FetchPayloadAsync(
                "activity heart rate zones",
                endpoint,
                () => session.Client.GetActivityHrInTimezones(
                    long.Parse(activity.ExternalId, System.Globalization.CultureInfo.InvariantCulture),
                    cancellationToken),
                cancellationToken);
            if (payload is null)
            {
                activity.HeartRateZonesSyncedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            using var document = JsonDocument.Parse(payload.Payload);
            var zones = GarminActivityPayloadParser.ParseHeartRateZones(document.RootElement);
            var existing = await dbContext.ActivityHeartRateZones
                .Where(item => item.ActivityId == activity.Id)
                .ToListAsync(cancellationToken);
            dbContext.ActivityHeartRateZones.RemoveRange(existing);
            dbContext.ActivityHeartRateZones.AddRange(zones.Select(zone => ToEntity(activity, zone)));

            var now = DateTimeOffset.UtcNow;
            activity.HeartRateZonesSyncedAt = now;
            await UpsertRawAsync(
                dbContext,
                "activity_hr_zones",
                activity.ExternalId,
                payload,
                now,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (IsUnsupportedEnrichment(exception))
            {
                activity.HeartRateZonesSyncedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            HandleEnrichmentFailure(activity.ExternalId, "heart rate zones", exception);
        }
    }

    private void HandleEnrichmentFailure(string externalId, string dataType, Exception exception)
    {
        var safeError = SafeError(exception);
        if (IsFatalProviderFailure(exception))
        {
            throw new InvalidOperationException(safeError);
        }

        logger.LogWarning(
            "Garmin activity {ActivityId} {DataType} enrichment failed and will be retried: {Error}",
            externalId,
            dataType,
            safeError);
    }

    private static bool IsUnsupportedEnrichment(Exception exception) =>
        exception.GetBaseException() is HttpRequestException
        {
            StatusCode: System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound
        };

    private Task DelayEnrichmentAsync(CancellationToken cancellationToken) =>
        options.ActivityEnrichmentDelayMilliseconds == 0
            ? Task.CompletedTask
            : Task.Delay(options.ActivityEnrichmentDelayMilliseconds, cancellationToken);

    private static void ApplySummary(Activity activity, GarminActivitySummaryData summary)
    {
        activity.DurationSeconds = summary.DurationSeconds ?? activity.DurationSeconds;
        activity.ElapsedDurationSeconds = summary.ElapsedDurationSeconds ?? activity.ElapsedDurationSeconds;
        activity.MovingDurationSeconds = summary.MovingDurationSeconds ?? activity.MovingDurationSeconds;
        activity.DistanceMeters = summary.DistanceMeters ?? activity.DistanceMeters;
        activity.Calories = summary.Calories ?? activity.Calories;
        activity.AverageHeartRate = summary.AverageHeartRate ?? activity.AverageHeartRate;
        activity.MaxHeartRate = summary.MaxHeartRate ?? activity.MaxHeartRate;
        activity.ElevationGainMeters = summary.ElevationGainMeters ?? activity.ElevationGainMeters;
        activity.ElevationLossMeters = summary.ElevationLossMeters ?? activity.ElevationLossMeters;
        activity.AverageSpeedMetersPerSecond =
            summary.AverageSpeedMetersPerSecond ?? activity.AverageSpeedMetersPerSecond;
        activity.MaxSpeedMetersPerSecond = summary.MaxSpeedMetersPerSecond ?? activity.MaxSpeedMetersPerSecond;
        activity.AveragePaceSecondsPerKilometer =
            summary.AveragePaceSecondsPerKilometer ?? activity.AveragePaceSecondsPerKilometer;
        activity.Steps = summary.Steps ?? activity.Steps;
        activity.AverageCadence = summary.AverageCadence ?? activity.AverageCadence;
        activity.MaxCadence = summary.MaxCadence ?? activity.MaxCadence;
        activity.CadenceUnit = summary.CadenceUnit ?? activity.CadenceUnit;
        activity.AveragePowerWatts = summary.AveragePowerWatts ?? activity.AveragePowerWatts;
        activity.MaxPowerWatts = summary.MaxPowerWatts ?? activity.MaxPowerWatts;
        activity.NormalizedPowerWatts = summary.NormalizedPowerWatts ?? activity.NormalizedPowerWatts;
        activity.MinTemperatureCelsius = summary.MinTemperatureCelsius ?? activity.MinTemperatureCelsius;
        activity.MaxTemperatureCelsius = summary.MaxTemperatureCelsius ?? activity.MaxTemperatureCelsius;
        activity.AverageRespirationRate = summary.AverageRespirationRate ?? activity.AverageRespirationRate;
        activity.MinRespirationRate = summary.MinRespirationRate ?? activity.MinRespirationRate;
        activity.MaxRespirationRate = summary.MaxRespirationRate ?? activity.MaxRespirationRate;
        activity.AverageSwolf = summary.AverageSwolf ?? activity.AverageSwolf;
        activity.ActiveLengths = summary.ActiveLengths ?? activity.ActiveLengths;
        activity.AerobicTrainingEffect = summary.AerobicTrainingEffect ?? activity.AerobicTrainingEffect;
        activity.AnaerobicTrainingEffect = summary.AnaerobicTrainingEffect ?? activity.AnaerobicTrainingEffect;
        activity.TrainingLoad = summary.TrainingLoad ?? activity.TrainingLoad;
        activity.TrainingStressScore = summary.TrainingStressScore ?? activity.TrainingStressScore;
        activity.IntensityFactor = summary.IntensityFactor ?? activity.IntensityFactor;
        activity.Vo2Max = summary.Vo2Max ?? activity.Vo2Max;
    }

    private static ActivityLap ToEntity(Activity activity, GarminActivityLapData lap) => new()
    {
        Activity = activity,
        ActivityId = activity.Id,
        LapIndex = lap.LapIndex,
        StartedAt = lap.StartedAt,
        DurationSeconds = lap.DurationSeconds,
        ElapsedDurationSeconds = lap.ElapsedDurationSeconds,
        MovingDurationSeconds = lap.MovingDurationSeconds,
        DistanceMeters = lap.DistanceMeters,
        AverageSpeedMetersPerSecond = lap.AverageSpeedMetersPerSecond,
        MaxSpeedMetersPerSecond = lap.MaxSpeedMetersPerSecond,
        AveragePaceSecondsPerKilometer = lap.AveragePaceSecondsPerKilometer,
        Calories = lap.Calories,
        AverageHeartRate = lap.AverageHeartRate,
        MaxHeartRate = lap.MaxHeartRate,
        ElevationGainMeters = lap.ElevationGainMeters,
        ElevationLossMeters = lap.ElevationLossMeters,
        MinElevationMeters = lap.MinElevationMeters,
        MaxElevationMeters = lap.MaxElevationMeters,
        AverageCadence = lap.AverageCadence,
        MaxCadence = lap.MaxCadence,
        CadenceUnit = lap.CadenceUnit,
        AverageTemperatureCelsius = lap.AverageTemperatureCelsius,
        MinTemperatureCelsius = lap.MinTemperatureCelsius,
        MaxTemperatureCelsius = lap.MaxTemperatureCelsius,
        AverageRespirationRate = lap.AverageRespirationRate,
        MaxRespirationRate = lap.MaxRespirationRate,
        IntensityType = lap.IntensityType
    };

    private static ActivityHeartRateZone ToEntity(Activity activity, GarminHeartRateZoneData zone) => new()
    {
        Activity = activity,
        ActivityId = activity.Id,
        ZoneNumber = zone.ZoneNumber,
        TimeSeconds = zone.TimeSeconds,
        Percentage = zone.Percentage,
        LowBoundaryBpm = zone.LowBoundaryBpm
    };

    private async Task UpsertDailySegmentAsync(
        DateOnly date,
        string dataType,
        CapturedGarminPayload payload,
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
            payload,
            now,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertTimelineAsync(
        DateOnly date,
        string metric,
        string unit,
        ParsedTimeline timeline,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await UpsertTimelineAsync(
            dbContext,
            date,
            metric,
            unit,
            timeline,
            DateTimeOffset.UtcNow,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertTimelineAsync(
        AppDbContext dbContext,
        DateOnly date,
        string metric,
        string unit,
        ParsedTimeline timeline,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var stored = await dbContext.DailyTimelines.SingleOrDefaultAsync(
            item => item.Source == SourceName && item.Date == date && item.Metric == metric,
            cancellationToken);
        if (stored is null)
        {
            stored = new DailyTimeline
            {
                Source = SourceName,
                Date = date,
                Metric = metric,
                Samples = timeline.Samples,
                UpdatedAt = updatedAt
            };
            dbContext.DailyTimelines.Add(stored);
        }
        else
        {
            stored.Samples = timeline.Samples;
            stored.UpdatedAt = updatedAt;
        }

        stored.SampleCount = timeline.SampleCount;
        await ReplaceHealthMetricSamplesAsync(
            dbContext,
            date,
            metric,
            unit,
            timeline.Points,
            updatedAt,
            cancellationToken);
    }

    private static async Task UpsertRawAsync(
        AppDbContext dbContext,
        string dataType,
        string externalId,
        CapturedGarminPayload payload,
        DateTimeOffset fetchedAt,
        CancellationToken cancellationToken)
    {
        var payloadHash = Convert.ToHexString(SHA256.HashData(payload.Payload)).ToLowerInvariant();
        var raw = await dbContext.RawProviderData.SingleOrDefaultAsync(
            item => item.Source == SourceName &&
                    item.DataType == dataType &&
                    item.ExternalId == externalId &&
                    item.PayloadHash == payloadHash,
            cancellationToken);

        if (raw is null)
        {
            dbContext.RawProviderData.Add(new RawProviderData
            {
                Source = SourceName,
                DataType = dataType,
                ExternalId = externalId,
                Endpoint = payload.RequestUri.AbsolutePath,
                HttpStatusCode = (int)payload.StatusCode,
                FetchedAt = fetchedAt,
                PayloadHash = payloadHash,
                ParserVersion = ParserVersion,
                Payload = JsonDocument.Parse(payload.Payload)
            });
            return;
        }

        raw.FetchedAt = fetchedAt;
        raw.ParserVersion = ParserVersion;
    }

    private static async Task ReplaceHealthMetricSamplesAsync(
        AppDbContext dbContext,
        DateOnly date,
        string metric,
        string unit,
        IReadOnlyList<TimelinePoint> points,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.HealthMetricSamples
            .Where(item => item.Source == SourceName && item.LocalDate == date && item.Metric == metric)
            .ToListAsync(cancellationToken);
        dbContext.HealthMetricSamples.RemoveRange(existing);
        dbContext.HealthMetricSamples.AddRange(points.Select(point => new HealthMetricSample
        {
            Source = SourceName,
            Metric = metric,
            LocalDate = date,
            TimestampUtc = point.Timestamp,
            ValueNumeric = point.Value,
            Unit = unit,
            SourceType = "garmin_api",
            UpdatedAt = updatedAt
        }));
    }

    private static async Task ReplaceActivitySamplesAsync(
        AppDbContext dbContext,
        Activity activity,
        IReadOnlyList<ParsedActivityPoint> points,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ActivitySamples
            .Where(item => item.ActivityId == activity.Id)
            .ToListAsync(cancellationToken);
        dbContext.ActivitySamples.RemoveRange(existing);
        dbContext.ActivitySamples.AddRange(points.Select(point => new ActivitySample
        {
            Activity = activity,
            ActivityId = activity.Id,
            TimestampUtc = point.TimestampUtc,
            ElapsedSeconds = point.ElapsedSeconds,
            HeartRateBpm = point.HeartRateBpm,
            DistanceMeters = point.DistanceMeters,
            SpeedMetersPerSecond = point.SpeedMetersPerSecond,
            PaceSecondsPerKilometer = point.PaceSecondsPerKilometer,
            ElevationMeters = point.ElevationMeters,
            Cadence = point.Cadence,
            PowerWatts = point.PowerWatts,
            TemperatureCelsius = point.TemperatureCelsius,
            RespirationRate = point.RespirationRate,
            SourceType = "garmin_api",
            UpdatedAt = updatedAt
        }));
    }

    private async Task ReplaceSleepSeriesAsync(
        DateOnly date,
        JsonElement root,
        CancellationToken cancellationToken)
    {
        var respiration = GarminDailyPayloadParser.ParseSleepRespiration(root);
        var stages = GarminDailyPayloadParser.ParseSleepStages(root);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var existing = await dbContext.HealthMetricSamples
            .Where(item => item.Source == SourceName &&
                           item.LocalDate == date &&
                           (item.Metric == "sleep_stage" || item.Metric == "sleep_respiration"))
            .ToListAsync(cancellationToken);
        dbContext.HealthMetricSamples.RemoveRange(existing);
        dbContext.HealthMetricSamples.AddRange(respiration.Select(point => new HealthMetricSample
        {
            Source = SourceName,
            Metric = "sleep_respiration",
            LocalDate = date,
            TimestampUtc = point.Timestamp,
            ValueNumeric = point.Value,
            Unit = "breaths_per_minute",
            SourceType = "garmin_api",
            UpdatedAt = now
        }));
        dbContext.HealthMetricSamples.AddRange(stages.Select(interval => new HealthMetricSample
        {
            Source = SourceName,
            Metric = "sleep_stage",
            LocalDate = date,
            TimestampUtc = interval.StartUtc!.Value,
            EndTimestampUtc = interval.EndUtc,
            ValueNumeric = interval.NumericValue,
            ValueText = interval.TextValue,
            Unit = "provider_stage_code",
            SourceType = "garmin_api",
            Quality = interval.TextValue is null ? "provider_code_unmapped" : null,
            UpdatedAt = now
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
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
            var safeError = SafeError(exception);
            logger.LogWarning("Garmin synchronization unit failed: {Unit}. {Error}", unit, safeError);

            if (IsFatalProviderFailure(exception))
            {
                throw new InvalidOperationException(safeError);
            }

            failures.Add(new InvalidOperationException($"Failed to synchronize {unit}: {safeError}"));
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

    private async Task<CapturedGarminPayload?> FetchPayloadAsync<T>(
        string dataType,
        string endpointPath,
        Func<Task<T>> request,
        CancellationToken cancellationToken)
    {
        using var capture = payloadCollector.BeginCapture();

        try
        {
            await request();
            LogAuthenticationSucceeded();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var capturedAfterFailure = capture.FindLast(endpointPath);
            if (capturedAfterFailure is null ||
                !capturedAfterFailure.HasContent ||
                !IsValidJson(capturedAfterFailure.Payload) ||
                !IsRecoverableMappingFailure(exception))
            {
                throw;
            }

            logger.LogInformation(
                "Using captured Garmin {DataType} JSON because the unofficial client could not map optional fields",
                dataType);
            LogAuthenticationSucceeded();
            return capturedAfterFailure;
        }

        var captured = capture.FindLast(endpointPath) ?? throw new InvalidOperationException(
            $"Garmin returned {dataType} without a capturable HTTP response.");

        if (!captured.HasContent)
        {
            logger.LogDebug("Garmin returned no {DataType} data", dataType);
            return null;
        }

        if (!IsValidJson(captured.Payload))
        {
            throw new JsonException($"Garmin returned invalid {dataType} JSON.");
        }

        return captured;
    }

    private static bool IsValidJson(byte[] payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsRecoverableMappingFailure(Exception exception)
    {
        var root = exception.GetBaseException();
        return root is JsonException or FormatException ||
               root is InvalidOperationException &&
               root.Message.Contains("token type", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeError(Exception exception)
    {
        var root = exception.GetBaseException();
        var typeName = root.GetType().Name;

        if (typeName.Contains("Authentication", StringComparison.OrdinalIgnoreCase))
        {
            return "Garmin authentication failed. Verify credentials, MFA code, and persisted session state.";
        }

        if (typeName.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase))
        {
            return "Garmin rate limit was reached. Retry later.";
        }

        if (root is HttpRequestException or TimeoutException or TaskCanceledException)
        {
            return "Garmin network request failed. Verify connectivity and retry.";
        }

        if (root is JsonException or FormatException)
        {
            return "Garmin returned a malformed or unsupported response.";
        }

        return root.Message.Length <= 500 ? root.Message : root.Message[..500];
    }

    private static bool IsFatalProviderFailure(Exception exception)
    {
        var root = exception.GetBaseException();
        var typeName = root.GetType().Name;
        return typeName.Contains("Authentication", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
               root is HttpRequestException or TimeoutException or TaskCanceledException;
    }

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

    private static int? GetAverageHeartRate(JsonElement root)
    {
        if (!TryGetProperty(root, "heartRateValues", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var measuredValues = new List<double>();
        foreach (var sample in values.EnumerateArray())
        {
            if (sample.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var parts = sample.EnumerateArray().ToArray();
            if (parts.Length > 1 && parts[1].ValueKind == JsonValueKind.Number)
            {
                var value = parts[1].GetDouble();
                if (value > 0)
                {
                    measuredValues.Add(value);
                }
            }
        }

        return measuredValues.Count == 0
            ? null
            : Convert.ToInt32(Math.Round(measuredValues.Average()));
    }

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

    private sealed record RawActivityPayload(string Json, CapturedGarminPayload Response);

}