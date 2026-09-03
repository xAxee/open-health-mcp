using System.Globalization;
using System.Text.Json;

namespace OpenHealthMCP.Providers.Garmin;

internal static class GarminProfilePayloadParser
{
    public static GarminUserSettingsData ParseSettings(JsonElement root)
    {
        if (!TryGet(root, "userData", out var userData))
        {
            return new(null, null);
        }

        return new(GetDouble(userData, "vo2MaxRunning"), GetDouble(userData, "vo2MaxCycling"));
    }

    public static GarminFitnessAgeData ParseFitnessAge(JsonElement root) => new(
        GetDouble(root, "fitnessAge"),
        GetDouble(root, "achievableFitnessAge"),
        GetTimestamp(root, "lastUpdated"));

    public static IReadOnlyList<GarminConfiguredZoneData> ParseConfiguredZones(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return root.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new GarminConfiguredZoneData(
                GetString(item, "sport") ?? "general",
                GetString(item, "trainingMethod"),
                GetDouble(item, "restingHeartRateUsed"),
                GetDouble(item, "lactateThresholdHeartRateUsed"),
                GetDouble(item, "maxHeartRateUsed"),
                GetDouble(item, "zone1Floor"),
                GetDouble(item, "zone2Floor"),
                GetDouble(item, "zone3Floor"),
                GetDouble(item, "zone4Floor"),
                GetDouble(item, "zone5Floor")))
            .ToArray();
    }

    public static IReadOnlyList<GarminBodyCompositionData> ParseBodyComposition(JsonElement root)
    {
        if (!TryGet(root, "dailyWeightSummaries", out var days) || days.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return days.EnumerateArray()
            .SelectMany(day => TryGet(day, "allWeightMetrics", out var values) && values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray().Select(ParseBodyMeasurement).Where(value => value is not null).Select(value => value!)
                : [])
            .GroupBy(value => value.ExternalId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(value => value.TimestampUtc)
            .ToArray();
    }

    public static IReadOnlyList<GarminBloodPressureData> ParseBloodPressure(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return root.EnumerateArray().Select(ParseBloodPressureMeasurement)
            .Where(value => value is not null).Select(value => value!)
            .GroupBy(value => value.ExternalId, StringComparer.Ordinal)
            .Select(group => group.Last()).OrderBy(value => value.TimestampUtc).ToArray();
    }

    private static GarminBodyCompositionData? ParseBodyMeasurement(JsonElement item)
    {
        var id = GetInt64(item, "samplePk");
        var timestamp = GetEpochTimestamp(item, "timestampGMT") ?? GetEpochTimestamp(item, "date");
        var localDate = GetDate(item, "calendarDate");
        if (!id.HasValue || !timestamp.HasValue || !localDate.HasValue)
        {
            return null;
        }

        return new GarminBodyCompositionData(
            id.Value.ToString(CultureInfo.InvariantCulture), localDate.Value, timestamp.Value,
            GramsToKilograms(GetDouble(item, "weight")), GetDouble(item, "bmi"),
            GetDouble(item, "bodyFat"), GramsToKilograms(GetDouble(item, "muscleMass")),
            GramsToKilograms(GetDouble(item, "boneMass")), GetDouble(item, "bodyWater"),
            GetDouble(item, "visceralFat"), GetDouble(item, "metabolicAge"));
    }

    private static GarminBloodPressureData? ParseBloodPressureMeasurement(JsonElement item)
    {
        var version = GetInt64(item, "version");
        var utc = GetTimestamp(item, "measurementTimestampGMT");
        var systolic = GetInt32(item, "systolic");
        var diastolic = GetInt32(item, "diastolic");
        if (!version.HasValue || !utc.HasValue || !systolic.HasValue || !diastolic.HasValue)
        {
            return null;
        }

        var local = GetLocalTimestamp(item, "measurementTimestampLocal");
        return new GarminBloodPressureData(
            $"{utc.Value:O}:{version.Value}",
            local.HasValue ? DateOnly.FromDateTime(local.Value) : DateOnly.FromDateTime(utc.Value.UtcDateTime),
            utc.Value, local, systolic.Value, diastolic.Value, GetInt32(item, "pulse"),
            GetString(item, "sourceType"));
    }

    private static double? GramsToKilograms(double? value) => value / 1000d;

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            return element.TryGetProperty(name, out value);
        }
        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name) =>
        TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? GetDouble(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out number) ? number : null;
    }

    private static long? GetInt64(JsonElement element, string name)
    {
        var value = GetDouble(element, name);
        return value.HasValue ? Convert.ToInt64(Math.Round(value.Value)) : null;
    }

    private static int? GetInt32(JsonElement element, string name)
    {
        var value = GetDouble(element, name);
        return value.HasValue ? Convert.ToInt32(Math.Round(value.Value)) : null;
    }

    private static DateOnly? GetDate(JsonElement element, string name) =>
        DateOnly.TryParseExact(GetString(element, name), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date) ? date : null;

    private static DateTimeOffset? GetEpochTimestamp(JsonElement element, string name)
    {
        var epoch = GetInt64(element, name);
        if (!epoch.HasValue) return null;
        try { return DateTimeOffset.FromUnixTimeMilliseconds(epoch.Value); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static DateTimeOffset? GetTimestamp(JsonElement element, string name) =>
        DateTimeOffset.TryParse(GetString(element, name), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value) ? value : null;

    private static DateTime? GetLocalTimestamp(JsonElement element, string name) =>
        DateTime.TryParse(GetString(element, name), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? DateTime.SpecifyKind(value, DateTimeKind.Unspecified) : null;
}

internal sealed record GarminUserSettingsData(double? Vo2MaxRunning, double? Vo2MaxCycling);
internal sealed record GarminFitnessAgeData(double? FitnessAge, double? AchievableFitnessAge, DateTimeOffset? UpdatedAt);
internal sealed record GarminConfiguredZoneData(string Sport, string? TrainingMethod, double? RestingHeartRateUsed,
    double? LactateThresholdHeartRateUsed, double? MaxHeartRateUsed, double? Zone1Floor, double? Zone2Floor,
    double? Zone3Floor, double? Zone4Floor, double? Zone5Floor);
internal sealed record GarminBodyCompositionData(string ExternalId, DateOnly LocalDate, DateTimeOffset TimestampUtc,
    double? WeightKilograms, double? Bmi, double? BodyFatPercent, double? MuscleMassKilograms,
    double? BoneMassKilograms, double? BodyWaterPercent, double? VisceralFat, double? MetabolicAge);
internal sealed record GarminBloodPressureData(string ExternalId, DateOnly LocalDate, DateTimeOffset TimestampUtc,
    DateTime? TimestampLocal, int Systolic, int Diastolic, int? Pulse, string? ProviderSourceType);