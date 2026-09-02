namespace OpenHealthMCP.Data.Entities;

public sealed class ActivitySample
{
    public long Id { get; set; }
    public long ActivityId { get; set; }
    public required Activity Activity { get; set; }
    public DateTimeOffset? TimestampUtc { get; set; }
    public double ElapsedSeconds { get; set; }
    public double? HeartRateBpm { get; set; }
    public double? DistanceMeters { get; set; }
    public double? SpeedMetersPerSecond { get; set; }
    public double? PaceSecondsPerKilometer { get; set; }
    public double? ElevationMeters { get; set; }
    public double? Cadence { get; set; }
    public double? PowerWatts { get; set; }
    public double? TemperatureCelsius { get; set; }
    public double? RespirationRate { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public required string SourceType { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}