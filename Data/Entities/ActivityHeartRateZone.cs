namespace OpenHealthMCP.Data.Entities;

public sealed class ActivityHeartRateZone
{
    public long Id { get; set; }
    public long ActivityId { get; set; }
    public required Activity Activity { get; set; }
    public int ZoneNumber { get; set; }
    public double TimeSeconds { get; set; }
    public double? Percentage { get; set; }
    public int? LowBoundaryBpm { get; set; }
}