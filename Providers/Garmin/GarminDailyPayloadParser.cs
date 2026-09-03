using System.Globalization;
using System.Text.Json;

namespace OpenHealthMCP.Providers.Garmin;

internal static class GarminDailyPayloadParser
{
    public static GarminDailySummaryData ParseSummary(JsonElement root)
    {
        var moderate = GetInt32(root, "moderateIntensityMinutes");
        var vigorous = GetInt32(root, "vigorousIntensityMinutes");
        var wellnessStartUtc = GetUtcTimestamp(root, "wellnessStartTimeGmt");
        var wellnessStartLocal = GetLocalTimestamp(root, "wellnessStartTimeLocal");
        return new GarminDailySummaryData(
            GetDateOnly(root, "calendarDate"),
            CalculateUtcOffsetMinutes(wellnessStartUtc, wellnessStartLocal),
            GetDouble(root, "totalDistanceMeters"),
            GetInt32(root, "activeSeconds"),
            GetInt32(root, "bmrKilocalories"),
            GetDouble(root, "floorsAscended"),
            GetInt32(root, "dailyStepGoal"),
            GetInt32(root, "userFloorsAscendedGoal"),
            GetInt32(root, "intensityMinutesGoal"),
            moderate.HasValue || vigorous.HasValue
                ? moderate.GetValueOrDefault() + 2 * vigorous.GetValueOrDefault()
                : null,
            GetDouble(root, "maxStressLevel"),
            GetString(root, "stressQualifier"),
            GetInt32(root, "restStressDuration"),
            GetInt32(root, "lowStressDuration"),
            GetInt32(root, "mediumStressDuration"),
            GetInt32(root, "highStressDuration"),
            GetInt32(root, "activityStressDuration"),
            GetDouble(root, "restStressPercentage"),
            GetDouble(root, "lowStressPercentage"),
            GetDouble(root, "mediumStressPercentage"),
            GetDouble(root, "highStressPercentage"),
            GetInt32(root, "bodyBatteryChargedValue"),
            GetInt32(root, "bodyBatteryDrainedValue"),
            GetInt32(root, "bodyBatteryMostRecentValue"),
            GetDouble(root, "lowestSpo2"),
            GetDouble(root, "latestSpo2"),
            GetDouble(root, "highestRespirationValue"),
            GetDouble(root, "lowestRespirationValue"),
            wellnessStartUtc,
            GetUtcTimestamp(root, "wellnessEndTimeGmt"),
            wellnessStartLocal,
            GetLocalTimestamp(root, "wellnessEndTimeLocal"));
    }

    public static GarminSleepSummaryData ParseSleep(JsonElement root)
    {
        if (!TryGetProperty(root, "dailySleepDTO", out var sleep))
        {
            return GarminSleepSummaryData.Empty;
        }

        JsonElement scores = default;
        JsonElement overall = default;
        var hasScores = TryGetProperty(sleep, "sleepScores", out scores);
        var hasOverall = hasScores && TryGetProperty(scores, "overall", out overall);
        var sleepStartUtc = GetUtcTimestamp(sleep, "sleepStartTimestampGMT");
        var sleepStartLocal = GetLocalTimestamp(sleep, "sleepStartTimestampLocal");
        return new GarminSleepSummaryData(
            CalculateUtcOffsetMinutes(sleepStartUtc, sleepStartLocal),
            GetInt32(sleep, "napTimeSeconds"),
            GetInt32(sleep, "unmeasurableSleepSeconds"),
            sleepStartUtc,
            GetUtcTimestamp(sleep, "sleepEndTimestampGMT"),
            sleepStartLocal,
            GetLocalTimestamp(sleep, "sleepEndTimestampLocal"),
            hasOverall ? GetString(overall, "qualifierKey") : null,
            GetInt32(sleep, "awakeCount"),
            GetDouble(sleep, "avgSleepStress"),
            GetDouble(sleep, "averageRespirationValue"),
            GetDouble(sleep, "lowestRespirationValue"),
            GetDouble(sleep, "highestRespirationValue"),
            hasScores ? scores.GetRawText() : null);
    }

    public static GarminHrvSummaryData ParseHrv(JsonElement root, DateOnly date)
    {
        if (!TryGetProperty(root, "hrvSummaries", out var summaries) || summaries.ValueKind != JsonValueKind.Array)
        {
            return GarminHrvSummaryData.Empty;
        }

        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var summary = summaries.EnumerateArray().FirstOrDefault(item =>
            GetString(item, "calendarDate") == dateText);
        return summary.ValueKind == JsonValueKind.Object
            ? new GarminHrvSummaryData(
                GetDouble(summary, "lastNightAvg"),
                GetDouble(summary, "lastNight5MinHigh"),
                GetString(summary, "status"),
                GetUtcTimestamp(summary, "createTimeStamp"))
            : GarminHrvSummaryData.Empty;
    }

    public static GarminSpo2SummaryData ParseSpo2(JsonElement root) => new(
        GetDouble(root, "averageSpO2"),
        GetDouble(root, "lowestSpO2"),
        GetDouble(root, "latestSpO2"),
        GetUtcTimestamp(root, "startTimestampGMT"),
        GetUtcTimestamp(root, "endTimestampGMT"),
        GetDouble(root, "avgSleepSpO2"));

    public static GarminRespirationSummaryData ParseRespiration(JsonElement root) => new(
        GetDouble(root, "avgWakingRespirationValue"),
        GetDouble(root, "avgSleepRespirationValue"),
        GetDouble(root, "lowestRespirationValue"),
        GetDouble(root, "highestRespirationValue"));

    public static IReadOnlyList<TimelinePoint> ParseSleepRespiration(JsonElement root)
    {
        if (!TryGetProperty(root, "wellnessEpochRespirationDataDTOList", out var values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray()
            .Select(item => new
            {
                Timestamp = GetUtcTimestamp(item, "startTimeGMT"),
                Value = GetDouble(item, "respirationValue")
            })
            .Where(item => item.Timestamp.HasValue && item.Value is >= 0)
            .Select(item => new TimelinePoint(item.Timestamp!.Value, item.Value!.Value))
            .OrderBy(item => item.Timestamp)
            .GroupBy(item => item.Timestamp)
            .Select(group => group.Last())
            .ToArray();
    }

    public static IReadOnlyList<TextTimelineInterval> ParseSleepStages(JsonElement root)
    {
        if (!TryGetProperty(root, "sleepLevels", out var levels) || levels.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return levels.EnumerateArray()
            .Select(item => new TextTimelineInterval(
                GetUtcTimestamp(item, "startGMT"),
                GetUtcTimestamp(item, "endGMT"),
                GetDouble(item, "activityLevel"),
                null))
            .Where(item => item.StartUtc.HasValue && item.EndUtc.HasValue && item.NumericValue.HasValue)
            .Select(item => item with { })
            .OrderBy(item => item.StartUtc)
            .ToArray();
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

    private static string? GetString(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
               double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
               double.IsFinite(number)
            ? number
            : null;
    }

    private static int? GetInt32(JsonElement element, string propertyName)
    {
        var value = GetDouble(element, propertyName);
        return value.HasValue ? Convert.ToInt32(Math.Round(value.Value)) : null;
    }

    private static DateOnly? GetDateOnly(JsonElement element, string propertyName) =>
        DateOnly.TryParseExact(GetString(element, propertyName), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;

    private static DateTimeOffset? GetUtcTimestamp(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var epoch))
        {
            try
            {
                return epoch >= 100_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                    : DateTimeOffset.FromUnixTimeSeconds(epoch);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(
            value.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp)
            ? timestamp
            : null;
    }

    private static DateTime? GetLocalTimestamp(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var epoch))
        {
            try
            {
                return DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime, DateTimeKind.Unspecified);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return value.ValueKind == JsonValueKind.String && DateTime.TryParse(
            value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)
            ? DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified)
            : null;
    }

    internal static int? CalculateUtcOffsetMinutes(DateTimeOffset? utc, DateTime? local)
    {
        if (!utc.HasValue || !local.HasValue)
        {
            return null;
        }

        var localWallClockAsUtc = DateTime.SpecifyKind(local.Value, DateTimeKind.Utc);
        var minutes = (localWallClockAsUtc - utc.Value.UtcDateTime).TotalMinutes;
        return minutes is >= -14 * 60 and <= 14 * 60
            ? Convert.ToInt32(Math.Round(minutes))
            : null;
    }
}

internal sealed record GarminDailySummaryData(
    DateOnly? CalendarDate,
    int? UtcOffsetMinutes,
    double? DistanceMeters,
    int? ActiveSeconds,
    int? BmrCalories,
    double? FloorsClimbed,
    int? StepsGoal,
    int? FloorsGoal,
    int? IntensityGoal,
    int? TotalIntensityMinutes,
    double? StressMax,
    string? StressQualifier,
    int? RestStressSeconds,
    int? LowStressSeconds,
    int? MediumStressSeconds,
    int? HighStressSeconds,
    int? ActivityStressSeconds,
    double? RestStressPercentage,
    double? LowStressPercentage,
    double? MediumStressPercentage,
    double? HighStressPercentage,
    int? BodyBatteryCharged,
    int? BodyBatteryDrained,
    int? BodyBatteryMostRecent,
    double? MinimumSpo2,
    double? LatestSpo2,
    double? MaximumRespirationRate,
    double? MinimumRespirationRate,
    DateTimeOffset? WellnessStartUtc,
    DateTimeOffset? WellnessEndUtc,
    DateTime? WellnessStartLocal,
    DateTime? WellnessEndLocal);

internal sealed record GarminSleepSummaryData(
    int? UtcOffsetMinutes,
    int? NapDurationSeconds,
    int? UnmeasurableSleepSeconds,
    DateTimeOffset? SleepStartUtc,
    DateTimeOffset? SleepEndUtc,
    DateTime? SleepStartLocal,
    DateTime? SleepEndLocal,
    string? SleepQualifier,
    int? AwakeCount,
    double? AverageSleepStress,
    double? AverageSleepRespirationRate,
    double? MinimumSleepRespirationRate,
    double? MaximumSleepRespirationRate,
    string? SubScoresJson)
{
    public static GarminSleepSummaryData Empty { get; } = new(
        null, null, null, null, null, null, null, null, null, null, null, null, null, null);
}

internal sealed record GarminHrvSummaryData(
    double? LastNightAverage,
    double? FiveMinuteHigh,
    string? Status,
    DateTimeOffset? CreatedAt)
{
    public static GarminHrvSummaryData Empty { get; } = new(null, null, null, null);
}

internal sealed record GarminSpo2SummaryData(
    double? Average,
    double? Minimum,
    double? Latest,
    DateTimeOffset? WindowStartUtc,
    DateTimeOffset? WindowEndUtc,
    double? SleepAverage);

internal sealed record GarminRespirationSummaryData(
    double? WakingAverage,
    double? SleepAverage,
    double? Minimum,
    double? Maximum);

internal sealed record TextTimelineInterval(
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    double? NumericValue,
    string? TextValue);