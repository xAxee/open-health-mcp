using System.Text.Json;

namespace OpenHealthMCP.Data.Entities;

public sealed class ActivityStream
{
    public long Id { get; set; }
    public long ActivityId { get; set; }
    public required Activity Activity { get; set; }
    public int SampleCount { get; set; }
    public string[] AvailableMetrics { get; set; } = [];
    public required JsonDocument Samples { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}