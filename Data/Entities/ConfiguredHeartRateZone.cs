namespace OpenHealthMCP.Data.Entities;

public sealed class ConfiguredHeartRateZone
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public required string Sport { get; set; }
    public string? TrainingMethod { get; set; }
    public double? RestingHeartRateUsed { get; set; }
    public double? LactateThresholdHeartRateUsed { get; set; }
    public double? MaxHeartRateUsed { get; set; }
    public double? Zone1Floor { get; set; }
    public double? Zone2Floor { get; set; }
    public double? Zone3Floor { get; set; }
    public double? Zone4Floor { get; set; }
    public double? Zone5Floor { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}