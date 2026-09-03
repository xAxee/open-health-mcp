namespace OpenHealthMCP.Data.Entities;

public sealed class BloodPressureMeasurement
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public required string ExternalId { get; set; }
    public DateOnly LocalDate { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public DateTime? TimestampLocal { get; set; }
    public int Systolic { get; set; }
    public int Diastolic { get; set; }
    public int? Pulse { get; set; }
    public string? ProviderSourceType { get; set; }
    public required string SourceType { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}