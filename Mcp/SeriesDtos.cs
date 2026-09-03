namespace OpenHealthMCP.Mcp;

public sealed record DaySeriesPoint(
    string Metric,
    DateTimeOffset TimestampUtc,
    DateTimeOffset? EndTimestampUtc,
    double? ValueNumeric,
    string? ValueText,
    string Unit,
    string SourceType,
    string? Quality,
    int MeasurementCount,
    string? Algorithm);

public sealed record DaySeriesResult(
    string Source,
    DateOnly Date,
    IReadOnlyList<string> RequestedMetrics,
    IReadOnlyList<string> AvailableMetrics,
    int OriginalPointCount,
    int ReturnedPointCount,
    string RequestedInterval,
    string EffectiveInterval,
    bool IntervalAggregated,
    bool Downsampled,
    string TimeBasis,
    string? Timezone,
    int? UtcOffsetMinutes,
    DateTimeOffset? EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    IReadOnlyList<DaySeriesPoint> Points);

public sealed record ActivitySeriesPoint(
    DateTimeOffset? TimestampUtc,
    double ElapsedSeconds,
    IReadOnlyDictionary<string, double> Values,
    string SourceType,
    int MeasurementCount,
    string? Algorithm);

public sealed record ActivitySeriesResult(
    bool Found,
    string Source,
    string ActivityId,
    bool Synchronized,
    IReadOnlyList<string> RequestedFields,
    IReadOnlyList<string> AvailableFields,
    int OriginalPointCount,
    int ReturnedPointCount,
    string RequestedInterval,
    string EffectiveInterval,
    string StoredResolution,
    string EffectiveResolution,
    bool IntervalAggregated,
    bool Downsampled,
    string TimeBasis,
    IReadOnlyList<ActivitySeriesPoint> Points);