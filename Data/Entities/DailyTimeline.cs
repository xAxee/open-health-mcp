using System.Text.Json;

namespace OpenHealthMCP.Data.Entities;

public sealed class DailyTimeline
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public DateOnly Date { get; set; }
    public required string Metric { get; set; }
    public int SampleCount { get; set; }
    public required JsonDocument Samples { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}