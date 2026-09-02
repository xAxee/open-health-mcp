namespace OpenHealthMCP.Data.Entities;

public sealed class Activity
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public required string ActivityType { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public double? DurationSeconds { get; set; }
    public double? ElapsedDurationSeconds { get; set; }
    public double? MovingDurationSeconds { get; set; }
    public double? DistanceMeters { get; set; }
    public int? Calories { get; set; }
    public int? AverageHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    public double? ElevationGainMeters { get; set; }
    public double? ElevationLossMeters { get; set; }
    public double? AverageSpeedMetersPerSecond { get; set; }
    public double? MaxSpeedMetersPerSecond { get; set; }
    public double? AveragePaceSecondsPerKilometer { get; set; }
    public int? Steps { get; set; }
    public double? AverageCadence { get; set; }
    public double? MaxCadence { get; set; }
    public string? CadenceUnit { get; set; }
    public double? AveragePowerWatts { get; set; }
    public double? MaxPowerWatts { get; set; }
    public double? NormalizedPowerWatts { get; set; }
    public double? MinTemperatureCelsius { get; set; }
    public double? MaxTemperatureCelsius { get; set; }
    public double? AverageRespirationRate { get; set; }
    public double? MinRespirationRate { get; set; }
    public double? MaxRespirationRate { get; set; }
    public double? AverageSwolf { get; set; }
    public int? ActiveLengths { get; set; }
    public double? AerobicTrainingEffect { get; set; }
    public double? AnaerobicTrainingEffect { get; set; }
    public double? TrainingLoad { get; set; }
    public double? TrainingStressScore { get; set; }
    public double? IntensityFactor { get; set; }
    public double? Vo2Max { get; set; }
    public DateTimeOffset? LapsSyncedAt { get; set; }
    public DateTimeOffset? HeartRateZonesSyncedAt { get; set; }
    public DateTimeOffset? StreamsSyncedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ActivityLap> Laps { get; set; } = [];
    public ICollection<ActivityHeartRateZone> HeartRateZones { get; set; } = [];
    public ICollection<ActivitySample> Samples { get; set; } = [];
    public ActivityStream? Stream { get; set; }
}