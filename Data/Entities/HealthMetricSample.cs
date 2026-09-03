namespace OpenHealthMCP.Data.Entities;

public sealed class HealthMetricSample
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public required string Metric { get; set; }
    public DateOnly LocalDate { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public DateTimeOffset? EndTimestampUtc { get; set; }
    public double? ValueNumeric { get; set; }
    public string? ValueText { get; set; }
    public required string Unit { get; set; }
    public required string SourceType { get; set; }
    public string? Quality { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}