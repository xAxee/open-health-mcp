using System.Globalization;
using System.Text.Json;

namespace OpenHealthMCP.Providers.Garmin;

internal static class GarminTimeSeriesPayloadParser
{
    private static readonly IReadOnlyDictionary<string, string> ActivityMetricNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["directTimestamp"] = "timestamp",
            ["directElapsedDuration"] = "elapsed_time_seconds",
            ["directHeartRate"] = "heart_rate_bpm",
            ["sumDistance"] = "distance_meters",
            ["directSpeed"] = "speed_meters_per_second",
            ["directVerticalSpeed"] = "vertical_speed_meters_per_second",
            ["directElevation"] = "altitude_meters",
            ["directRunCadence"] = "cadence",
            ["directDoubleCadence"] = "cadence",
            ["directBikeCadence"] = "cadence",
            ["directSwimCadence"] = "cadence",
            ["directPower"] = "power_watts",
            ["directAirTemperature"] = "temperature_celsius",
            ["directTemperature"] = "temperature_celsius",
            ["directRespirationRate"] = "respiration_rate",
            ["directBodyBattery"] = "body_battery"
        };

    public static ParsedActivityStream ParseActivityStream(JsonElement root, DateTimeOffset startedAt)
    {
        if (!TryGetProperty(root, "metricDescriptors", out var descriptors) ||
            descriptors.ValueKind != JsonValueKind.Array ||
            !TryGetProperty(root, "activityDetailMetrics", out var samples) ||
            samples.ValueKind != JsonValueKind.Array)
        {
            return new ParsedActivityStream([], JsonSerializer.SerializeToDocument(Array.Empty<object>()));
        }

        var indexes = new Dictionary<int, string>();
        foreach (var descriptor in descriptors.EnumerateArray())
        {
            var index = GetInt32(descriptor, "metricsIndex");
            var providerKey = GetString(descriptor, "key");
            if (index.HasValue && providerKey is not null && ActivityMetricNames.TryGetValue(providerKey, out var metric))
            {
                indexes[index.Value] = metric;
            }
        }

        var rows = new List<Dictionary<string, object?>>();
        foreach (var sample in samples.EnumerateArray())
        {
            if (!TryGetProperty(sample, "metrics", out var values) || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var valueArray = values.EnumerateArray().ToArray();
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (index, metric) in indexes)
            {
                if (index >= valueArray.Length || !TryGetDouble(valueArray[index], out var value))
                {
                    continue;
                }

                if (metric == "timestamp")
                {
                    var timestamp = ParseTimestamp(value);
                    if (timestamp.HasValue)
                    {
                        row["timestamp"] = timestamp.Value;
                        row.TryAdd("elapsedTimeSeconds", Math.Max(0, (timestamp.Value - startedAt).TotalSeconds));
                    }
                }
                else if (metric == "elapsed_time_seconds")
                {
                    row["elapsedTimeSeconds"] = value;
                }
                else
                {
                    row[ToCamelCase(metric)] = value;
                    if (metric == "speed_meters_per_second" && value > 0)
                    {
                        row["paceSecondsPerKilometer"] = 1000 / value;
                    }
                }
            }

            if (row.Count > 0)
            {
                rows.Add(row);
            }
        }

        var availableMetrics = rows
            .SelectMany(row => row.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        return new ParsedActivityStream(availableMetrics, JsonSerializer.SerializeToDocument(rows));
    }

    public static ParsedTimeline ParseDescriptorTimeline(
        JsonElement root,
        string descriptorsProperty,
        string valuesProperty,
        string timestampKey,
        string valueKey)
    {
        if (!TryGetProperty(root, descriptorsProperty, out var descriptors) ||
            descriptors.ValueKind != JsonValueKind.Array ||
            !TryGetProperty(root, valuesProperty, out var values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            return EmptyTimeline();
        }

        var timestampIndex = FindDescriptorIndex(descriptors, timestampKey);
        var valueIndex = FindDescriptorIndex(descriptors, valueKey);
        if (!timestampIndex.HasValue || !valueIndex.HasValue)
        {
            return EmptyTimeline();
        }

        return ParseTupleTimeline(values, timestampIndex.Value, valueIndex.Value, valueKey);
    }

    private static ParsedTimeline ParseTupleTimeline(
        JsonElement values,
        int timestampIndex,
        int valueIndex,
        string valueKey)
    {
        var result = new List<TimelinePoint>();
        foreach (var tuple in values.EnumerateArray())
        {
            if (tuple.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var fields = tuple.EnumerateArray().ToArray();
            if (timestampIndex >= fields.Length || valueIndex >= fields.Length ||
                !TryGetDouble(fields[timestampIndex], out var timestampValue) ||
                !TryGetDouble(fields[valueIndex], out var value) ||
                !IsValidTimelineValue(valueKey, value) ||
                !ParseTimestamp(timestampValue).HasValue)
            {
                continue;
            }

            result.Add(new TimelinePoint(ParseTimestamp(timestampValue)!.Value, value));
        }

        var ordered = result
            .OrderBy(point => point.Timestamp)
            .GroupBy(point => point.Timestamp)
            .Select(group => group.Last())
            .ToArray();
        return new ParsedTimeline(ordered.Length, JsonSerializer.SerializeToDocument(ordered));
    }

    private static int? FindDescriptorIndex(JsonElement descriptors, string requestedKey)
    {
        foreach (var descriptor in descriptors.EnumerateArray())
        {
            var key = GetString(descriptor, "key") ??
                      GetString(descriptor, "bodyBatteryValueDescriptorKey");
            var index = GetInt32(descriptor, "index") ??
                        GetInt32(descriptor, "bodyBatteryValueDescriptorIndex");
            if (index.HasValue && key is not null &&
                (string.Equals(key, requestedKey, StringComparison.OrdinalIgnoreCase) ||
                 requestedKey == "timestamp" && key.EndsWith("Timestamp", StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return null;
    }

    private static bool IsValidTimelineValue(string valueKey, double value) => valueKey switch
    {
        "heartRate" => value is > 0 and <= 300,
        "stressLevel" => value is >= 1 and <= 100,
        "bodyBatteryLevel" => value is >= 0 and <= 100,
        _ => true
    };

    private static ParsedTimeline EmptyTimeline() =>
        new(0, JsonSerializer.SerializeToDocument(Array.Empty<TimelinePoint>()));

    private static DateTimeOffset? ParseTimestamp(double value)
    {
        try
        {
            return value >= 100_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(value, CultureInfo.InvariantCulture))
                : value >= 1_000_000_000
                    ? DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(value, CultureInfo.InvariantCulture))
                    : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string ToCamelCase(string snakeCase)
    {
        var parts = snakeCase.Split('_');
        return parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
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

    private static int? GetInt32(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && TryGetDouble(value, out var number)
            ? Convert.ToInt32(number, CultureInfo.InvariantCulture)
            : null;

    private static bool TryGetDouble(JsonElement value, out double result)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out result) && double.IsFinite(result))
        {
            return true;
        }

        result = default;
        return false;
    }

    private sealed record TimelinePoint(DateTimeOffset Timestamp, double Value);
}

internal sealed record ParsedActivityStream(string[] AvailableMetrics, JsonDocument Samples)
{
    public int SampleCount => Samples.RootElement.GetArrayLength();
}

internal sealed record ParsedTimeline(int SampleCount, JsonDocument Samples);