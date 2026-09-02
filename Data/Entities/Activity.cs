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
    public double? DistanceMeters { get; set; }
    public int? Calories { get; set; }
    public int? AverageHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    public double? ElevationGainMeters { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}