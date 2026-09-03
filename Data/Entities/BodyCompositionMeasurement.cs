namespace OpenHealthMCP.Data.Entities;

public sealed class BodyCompositionMeasurement
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public required string ExternalId { get; set; }
    public DateOnly LocalDate { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public double? WeightKilograms { get; set; }
    public double? Bmi { get; set; }
    public double? BodyFatPercent { get; set; }
    public double? MuscleMassKilograms { get; set; }
    public double? BoneMassKilograms { get; set; }
    public double? BodyWaterPercent { get; set; }
    public double? VisceralFat { get; set; }
    public double? MetabolicAge { get; set; }
    public required string SourceType { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}