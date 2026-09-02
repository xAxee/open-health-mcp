namespace OpenHealthMCP.Data.Entities;

public sealed class DailyMetric
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public DateOnly Date { get; set; }
    public int? Steps { get; set; }
    public int? RestingHeartRate { get; set; }
    public int? AverageHeartRate { get; set; }
    public int? MinHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    public double? Hrv { get; set; }
    public double? StressAverage { get; set; }
    public int? BodyBatteryMin { get; set; }
    public int? BodyBatteryMax { get; set; }
    public double? SleepScore { get; set; }
    public int? Calories { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}