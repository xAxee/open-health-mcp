namespace OpenHealthMCP.Data.Entities;

public sealed class ActivityLap
{
    public long Id { get; set; }
    public long ActivityId { get; set; }
    public required Activity Activity { get; set; }
    public int LapIndex { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public double? DurationSeconds { get; set; }
    public double? ElapsedDurationSeconds { get; set; }
    public double? MovingDurationSeconds { get; set; }
    public double? DistanceMeters { get; set; }
    public double? AverageSpeedMetersPerSecond { get; set; }
    public double? MaxSpeedMetersPerSecond { get; set; }
    public double? AveragePaceSecondsPerKilometer { get; set; }
    public int? Calories { get; set; }
    public int? AverageHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    public double? ElevationGainMeters { get; set; }
    public double? ElevationLossMeters { get; set; }
    public double? MinElevationMeters { get; set; }
    public double? MaxElevationMeters { get; set; }
    public double? AverageCadence { get; set; }
    public double? MaxCadence { get; set; }
    public string? CadenceUnit { get; set; }
    public double? AverageTemperatureCelsius { get; set; }
    public double? MinTemperatureCelsius { get; set; }
    public double? MaxTemperatureCelsius { get; set; }
    public double? AverageRespirationRate { get; set; }
    public double? MaxRespirationRate { get; set; }
    public string? IntensityType { get; set; }
}