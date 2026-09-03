namespace OpenHealthMCP.Mcp;

internal static class SeriesProcessing
{
    private static readonly IReadOnlyDictionary<string, double?> SupportedIntervals =
        new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase)
        {
            ["raw"] = null,
            ["1s"] = 1,
            ["5s"] = 5,
            ["15s"] = 15,
            ["30s"] = 30,
            ["1m"] = 60,
            ["5m"] = 300,
            ["15m"] = 900,
            ["30m"] = 1800,
            ["1h"] = 3600
        };

    public static (string Name, double? Seconds) ParseInterval(string? interval)
    {
        var normalized = string.IsNullOrWhiteSpace(interval) ? "raw" : interval.Trim().ToLowerInvariant();
        return SupportedIntervals.TryGetValue(normalized, out var seconds)
            ? (normalized, seconds)
            : throw new ArgumentException(
                $"interval must be one of: {string.Join(", ", SupportedIntervals.Keys)}.",
                nameof(interval));
    }

    public static IReadOnlyList<T> Downsample<T>(IReadOnlyList<T> values, int maxPoints)
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

    public static string Resolution(IEnumerable<double> positions)
    {
        var ordered = positions.Distinct().Order().ToArray();
        if (ordered.Length < 2)
        {
            return "unknown";
        }

        var differences = ordered.Zip(ordered.Skip(1), (left, right) => right - left)
            .Where(value => value > 0)
            .Order()
            .ToArray();
        if (differences.Length == 0)
        {
            return "unknown";
        }

        var median = differences[differences.Length / 2];
        return $"{Math.Round(median, 3, MidpointRounding.AwayFromZero):0.###}s";
    }

    public static long TimeBucket(DateTimeOffset timestamp, double intervalSeconds) =>
        (long)Math.Floor(timestamp.ToUnixTimeMilliseconds() / (intervalSeconds * 1000));

    public static long ElapsedBucket(double elapsedSeconds, double intervalSeconds) =>
        (long)Math.Floor(elapsedSeconds / intervalSeconds);
}