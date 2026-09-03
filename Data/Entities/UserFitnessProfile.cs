namespace OpenHealthMCP.Data.Entities;

public sealed class UserFitnessProfile
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public string? ProviderProfileId { get; set; }
    public double? Vo2MaxRunning { get; set; }
    public double? Vo2MaxCycling { get; set; }
    public double? FitnessAge { get; set; }
    public double? AchievableFitnessAge { get; set; }
    public DateTimeOffset? FitnessAgeUpdatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}