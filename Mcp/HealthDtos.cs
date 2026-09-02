namespace OpenHealthMCP.Mcp;

public sealed record DayResult(
    string Source,
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
    int? Calories);

public sealed record DayLookupResult(bool Found, DayResult? Data);

public sealed record ActivityResult(
    string Source,
    string ActivityId,
    string Name,
    string ActivityType,
    DateTimeOffset StartedAt,
    double? DurationSeconds,
    double? DistanceMeters,
    int? Calories,
    int? AverageHeartRate,
    int? MaxHeartRate,
    double? ElevationGainMeters);

public sealed record ActivityLookupResult(bool Found, ActivityResult? Data);

public sealed record TrendValue(DateOnly Date, double Value);

public sealed record TrendResult(
    string Metric,
    DateOnly From,
    DateOnly To,
    string Source,
    double? Average,
    double? Min,
    double? Max,
    int Count,
    IReadOnlyList<TrendValue> Values);

public sealed record ComparePeriodsResult(
    string Metric,
    DateOnly PeriodAFrom,
    DateOnly PeriodATo,
    DateOnly PeriodBFrom,
    DateOnly PeriodBTo,
    string Source,
    double? AverageA,
    double? AverageB,
    double? AbsoluteDifference,
    double? PercentageChange,
    int SampleCountA,
    int SampleCountB);