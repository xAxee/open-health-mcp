using System.Text.Json;

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
    int? Calories,
    int? ActiveCalories,
    int? ModerateIntensityMinutes,
    int? VigorousIntensityMinutes,
    int? SleepDurationSeconds,
    int? DeepSleepSeconds,
    int? LightSleepSeconds,
    int? RemSleepSeconds,
    int? AwakeSleepSeconds,
    double? AverageRespirationRate,
    double? AverageSpo2,
    double? DistanceMeters,
    int? ActiveSeconds,
    int? UtcOffsetMinutes,
    string? Timezone,
    DayCaloriesResult CaloriesDetails,
    DayGoalsResult Goals,
    DayIntensityResult Intensity,
    DayStressResult StressDetails,
    DayBodyBatteryResult BodyBatteryDetails,
    DayHrvResult HrvDetails,
    DaySleepResult SleepDetails,
    DaySpo2Result Spo2Details,
    DayRespirationResult RespirationDetails,
    DaySourceMetadata Sources);

public sealed record DayCaloriesResult(int? TotalCalories, int? ActiveCalories, int? BmrCalories);

public sealed record DayGoalsResult(int? StepsGoal, int? FloorsGoal, int? IntensityGoal);

public sealed record DayIntensityResult(
    int? ModerateMinutes,
    int? VigorousMinutes,
    int? TotalMinutes,
    string VigorousCounting,
    MetricSourceMetadata TotalSource);

public sealed record DayStressResult(
    double? Average,
    double? Maximum,
    string? Qualifier,
    int? RestSeconds,
    int? LowSeconds,
    int? MediumSeconds,
    int? HighSeconds,
    int? ActivitySeconds,
    double? RestPercentage,
    double? LowPercentage,
    double? MediumPercentage,
    double? HighPercentage);

public sealed record DayBodyBatteryResult(
    int? Minimum,
    int? Maximum,
    int? Charged,
    int? Drained,
    int? MostRecent);

public sealed record DayHrvResult(
    double? LastNightAverageMs,
    double? FiveMinuteHighMs,
    string? Status,
    DateTimeOffset? CreatedAt,
    int? ReadingsCount,
    MetricSourceMetadata Source);

public sealed record DaySleepResult(
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    DateTime? StartLocal,
    DateTime? EndLocal,
    int? DurationSeconds,
    int? NapDurationSeconds,
    int? DeepSeconds,
    int? LightSeconds,
    int? RemSeconds,
    int? AwakeSeconds,
    int? UnmeasurableSeconds,
    double? Score,
    string? Qualifier,
    int? AwakeCount,
    double? AverageStress,
    JsonElement? SubScores);

public sealed record DaySpo2Result(
    double? Average,
    double? Minimum,
    double? Maximum,
    double? Latest,
    double? SleepAverage,
    int? ReadingsCount,
    DateTimeOffset? WindowStartUtc,
    DateTimeOffset? WindowEndUtc);

public sealed record DayRespirationResult(
    double? WakingAverage,
    double? SleepAverage,
    double? Minimum,
    double? Maximum);

public sealed record DaySourceMetadata(
    string Daily,
    MetricSourceMetadata AverageHeartRate,
    string Sleep,
    string Hrv,
    string Stress,
    string BodyBattery,
    string Spo2,
    string Respiration);

public sealed record MetricSourceMetadata(string Source, string? Algorithm = null);

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
    bool HeartRateZonesSynchronized,
    bool StreamsSynchronized);

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

public sealed record ActivityStreamPoint(
    DateTimeOffset? Timestamp,
    double? ElapsedTimeSeconds,
    IReadOnlyDictionary<string, double> Values);

public sealed record ActivityStreamsResult(
    bool Found,
    string Source,
    string ActivityId,
    bool Synchronized,
    int TotalSampleCount,
    int ReturnedSampleCount,
    IReadOnlyList<string> AvailableMetrics,
    IReadOnlyList<string> SelectedMetrics,
    IReadOnlyList<ActivityStreamPoint> Samples);

public sealed record DailyTimelinePoint(DateTimeOffset Timestamp, double Value);

public sealed record DailyTimelineResult(
    string Source,
    DateOnly Date,
    string Metric,
    bool Synchronized,
    int TotalSampleCount,
    int ReturnedSampleCount,
    IReadOnlyList<DailyTimelinePoint> Samples);

public sealed record ActivitySummaryHeartRateZone(
    int ZoneNumber,
    double TimeSeconds);

public sealed record ActivitySummaryValues(
    int ActivityCount,
    double? DurationSeconds,
    double? MovingDurationSeconds,
    double? DistanceMeters,
    double? ElevationGainMeters,
    double? ElevationLossMeters,
    int? Calories,
    int? Steps,
    double? AverageHeartRate,
    IReadOnlyList<ActivitySummaryHeartRateZone> HeartRateZones);

public sealed record ActivityTypeSummary(string ActivityType, ActivitySummaryValues Values);

public sealed record ActivitySummaryGroup(
    DateOnly From,
    DateOnly To,
    ActivitySummaryValues Values,
    IReadOnlyList<ActivityTypeSummary> ByActivityType);

public sealed record ActivitySummaryResult(
    string Source,
    DateOnly From,
    DateOnly To,
    string? ActivityType,
    string GroupBy,
    ActivitySummaryValues Total,
    IReadOnlyList<ActivityTypeSummary> ByActivityType,
    IReadOnlyList<ActivitySummaryGroup> Groups);

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