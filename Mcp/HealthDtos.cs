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

public sealed record ActivityListResult(
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

public sealed record ActivityResult(
    string Source,
    string ActivityId,
    string Name,
    string ActivityType,
    DateTimeOffset StartedAt,
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
    bool LapsSynchronized,
    bool HeartRateZonesSynchronized);

public sealed record ActivityLookupResult(bool Found, ActivityResult? Data);

public sealed record ActivityLapResult(
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

public sealed record ActivityLapsResult(
    bool Found,
    string Source,
    string ActivityId,
    bool Synchronized,
    IReadOnlyList<ActivityLapResult> Laps);

public sealed record ActivityHeartRateZoneResult(
    int ZoneNumber,
    double TimeSeconds,
    double? Percentage,
    int? LowBoundaryBpm);

public sealed record ActivityHeartRateZonesResult(
    bool Found,
    string Source,
    string ActivityId,
    bool Synchronized,
    IReadOnlyList<ActivityHeartRateZoneResult> Zones);

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