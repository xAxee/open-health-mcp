using System.Text.Json;

namespace OpenHealthMCP.Providers.Garmin;

internal static class GarminActivityPayloadParser
{
    public static GarminActivitySummaryData ParseSummary(JsonElement activity)
    {
        var (averageCadence, maxCadence, cadenceUnit) = GetCadence(activity);
        var averageSpeed = GetDouble(activity, "averageSpeed");

        return new GarminActivitySummaryData(
            GetDouble(activity, "duration"),
            GetDouble(activity, "elapsedDuration"),
            GetDouble(activity, "movingDuration"),
            GetDouble(activity, "distance"),
            GetInt32(activity, "calories"),
            GetInt32(activity, "averageHR"),
            GetInt32(activity, "maxHR"),
            GetDouble(activity, "elevationGain"),
            GetDouble(activity, "elevationLoss"),
            averageSpeed,
            GetDouble(activity, "maxSpeed"),
            PaceFromSpeed(averageSpeed),
            GetInt32(activity, "steps"),
            averageCadence,
            maxCadence,
            cadenceUnit,
            GetDouble(activity, "avgPower"),
            GetDouble(activity, "maxPower"),
            GetDouble(activity, "normPower"),
            GetDouble(activity, "minTemperature"),
            GetDouble(activity, "maxTemperature"),
            GetDouble(activity, "avgRespirationRate"),
            GetDouble(activity, "minRespirationRate"),
            GetDouble(activity, "maxRespirationRate"),
            GetDouble(activity, "averageSwolf"),
            GetInt32(activity, "activeLengths"),
            GetDouble(activity, "aerobicTrainingEffect"),
            GetDouble(activity, "anaerobicTrainingEffect"),
            GetDouble(activity, "activityTrainingLoad"),
            GetDouble(activity, "trainingStressScore"),
            GetDouble(activity, "intensityFactor"),
            GetDouble(activity, "vO2MaxValue"),
            GetBoolean(activity, "hasSplits"));
    }

    public static IReadOnlyList<GarminActivityLapData> ParseLaps(JsonElement response)
    {
        if (!TryGetProperty(response, "lapDTOs", out var laps) || laps.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<GarminActivityLapData>();
        foreach (var lap in laps.EnumerateArray())
        {
            if (lap.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var lapIndex = GetInt32(lap, "lapIndex");
            if (!lapIndex.HasValue)
            {
                continue;
            }

            var averageSpeed = GetDouble(lap, "averageSpeed");
            var (averageCadence, maxCadence, cadenceUnit) = GetLapCadence(lap);
            result.Add(new GarminActivityLapData(
                lapIndex.Value,
                GetDateTimeOffset(lap, "startTimeGMT"),
                GetDouble(lap, "duration"),
                GetDouble(lap, "elapsedDuration"),
                GetDouble(lap, "movingDuration"),
                GetDouble(lap, "distance"),
                averageSpeed,
                GetDouble(lap, "maxSpeed"),
                PaceFromSpeed(averageSpeed),
                GetInt32(lap, "calories"),
                GetInt32(lap, "averageHR"),
                GetInt32(lap, "maxHR"),
                GetDouble(lap, "elevationGain"),
                GetDouble(lap, "elevationLoss"),
                GetDouble(lap, "minElevation"),
                GetDouble(lap, "maxElevation"),
                averageCadence,
                maxCadence,
                cadenceUnit,
                GetDouble(lap, "averageTemperature"),
                GetDouble(lap, "minTemperature"),
                GetDouble(lap, "maxTemperature"),
                GetDouble(lap, "avgRespirationRate"),
                GetDouble(lap, "maxRespirationRate"),
                GetString(lap, "intensityType")));
        }

        return result;
    }

    public static IReadOnlyList<GarminHeartRateZoneData> ParseHeartRateZones(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var rawZones = response.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new
            {
                ZoneNumber = GetInt32(item, "zoneNumber"),
                TimeSeconds = GetDouble(item, "secsInZone"),
                LowBoundaryBpm = GetInt32(item, "zoneLowBoundary")
            })
            .Where(item => item.ZoneNumber.HasValue && item.TimeSeconds is >= 0)
            .ToArray();
        var totalSeconds = rawZones.Sum(item => item.TimeSeconds!.Value);

        return rawZones
            .Select(item => new GarminHeartRateZoneData(
                item.ZoneNumber!.Value,
                item.TimeSeconds!.Value,
                totalSeconds > 0 ? item.TimeSeconds.Value / totalSeconds * 100 : null,
                item.LowBoundaryBpm))
            .ToArray();
    }

    private static (double? Average, double? Max, string? Unit) GetCadence(JsonElement activity)
    {
        if (HasNumber(activity, "averageRunningCadenceInStepsPerMinute") ||
            HasNumber(activity, "maxRunningCadenceInStepsPerMinute"))
        {
            return (
                GetDouble(activity, "averageRunningCadenceInStepsPerMinute"),
                GetDouble(activity, "maxRunningCadenceInStepsPerMinute"),
                "steps_per_minute");
        }

        if (HasNumber(activity, "averageBikingCadenceInRevPerMinute") ||
            HasNumber(activity, "maxBikingCadenceInRevPerMinute"))
        {
            return (
                GetDouble(activity, "averageBikingCadenceInRevPerMinute"),
                GetDouble(activity, "maxBikingCadenceInRevPerMinute"),
                "revolutions_per_minute");
        }

        if (HasNumber(activity, "averageSwimCadenceInStrokesPerMinute") ||
            HasNumber(activity, "maxSwimCadenceInStrokesPerMinute"))
        {
            return (
                GetDouble(activity, "averageSwimCadenceInStrokesPerMinute"),
                GetDouble(activity, "maxSwimCadenceInStrokesPerMinute"),
                "strokes_per_minute");
        }

        return (null, null, null);
    }

    private static (double? Average, double? Max, string? Unit) GetLapCadence(JsonElement lap)
    {
        var average = GetDouble(lap, "averageRunCadence");
        var max = GetDouble(lap, "maxRunCadence");
        if (!average.HasValue && !max.HasValue)
        {
            return (null, null, null);
        }

        return (average, max, "steps_per_minute");
    }

    private static double? PaceFromSpeed(double? metersPerSecond) =>
        metersPerSecond is > 0 ? 1000 / metersPerSecond.Value : null;

    private static bool HasNumber(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Number;

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            return element.TryGetProperty(propertyName, out value);
        }

        value = default;
        return false;
    }

    private static double? GetDouble(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var result)
            ? result
            : null;

    private static int? GetInt32(JsonElement element, string propertyName)
    {
        var number = GetDouble(element, propertyName);
        return number.HasValue ? Convert.ToInt32(Math.Round(number.Value)) : null;
    }

    private static bool? GetBoolean(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static string? GetString(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var timestamp)
            ? timestamp
            : null;
    }
}

internal sealed record GarminActivitySummaryData(
    double? DurationSeconds,
    double? ElapsedDurationSeconds,
    double? MovingDurationSeconds,
    double? DistanceMeters,
    int? Calories,
    int? AverageHeartRate,
    int? MaxHeartRate,
    double? ElevationGainMeters,
    double? ElevationLossMeters,
    double? AverageSpeedMetersPerSecond,
    double? MaxSpeedMetersPerSecond,
    double? AveragePaceSecondsPerKilometer,
    int? Steps,
    double? AverageCadence,
    double? MaxCadence,
    string? CadenceUnit,
    double? AveragePowerWatts,
    double? MaxPowerWatts,
    double? NormalizedPowerWatts,
    double? MinTemperatureCelsius,
    double? MaxTemperatureCelsius,
    double? AverageRespirationRate,
    double? MinRespirationRate,
    double? MaxRespirationRate,
    double? AverageSwolf,
    int? ActiveLengths,
    double? AerobicTrainingEffect,
    double? AnaerobicTrainingEffect,
    double? TrainingLoad,
    double? TrainingStressScore,
    double? IntensityFactor,
    double? Vo2Max,
    bool? HasSplits);

internal sealed record GarminActivityLapData(
    int LapIndex,
    DateTimeOffset? StartedAt,
    double? DurationSeconds,
    double? ElapsedDurationSeconds,
    double? MovingDurationSeconds,
    double? DistanceMeters,
    double? AverageSpeedMetersPerSecond,
    double? MaxSpeedMetersPerSecond,
    double? AveragePaceSecondsPerKilometer,
    int? Calories,
    int? AverageHeartRate,
    int? MaxHeartRate,
    double? ElevationGainMeters,
    double? ElevationLossMeters,
    double? MinElevationMeters,
    double? MaxElevationMeters,
    double? AverageCadence,
    double? MaxCadence,
    string? CadenceUnit,
    double? AverageTemperatureCelsius,
    double? MinTemperatureCelsius,
    double? MaxTemperatureCelsius,
    double? AverageRespirationRate,
    double? MaxRespirationRate,
    string? IntensityType);

internal sealed record GarminHeartRateZoneData(
    int ZoneNumber,
    double TimeSeconds,
    double? Percentage,
    int? LowBoundaryBpm);