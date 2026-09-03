namespace OpenHealthMCP.Mcp;

internal static class ActivityZoneMapper
{
    public static IReadOnlyList<ActivityHeartRateZoneResult> Map(
        IReadOnlyList<ActivityZoneRow> zones) => zones.Select((zone, index) =>
    {
        var nextLow = index + 1 < zones.Count ? zones[index + 1].LowBoundaryBpm : null;
        return new ActivityHeartRateZoneResult(
            zone.ZoneNumber,
            zone.TimeSeconds,
            zone.Percentage,
            zone.LowBoundaryBpm,
            nextLow.HasValue ? nextLow.Value - 1 : null,
            new MetricSourceMetadata(
                nextLow.HasValue ? "garmin_api_and_derived_by_openhealth" : "garmin_api",
                nextLow.HasValue ? "next-garmin-zone-floor-minus-one-v1" : null),
            new MetricSourceMetadata("derived_by_openhealth", "garmin-zone-duration-percentage-v1"));
    }).ToArray();
}

internal sealed record ActivityZoneRow(
    int ZoneNumber,
    double TimeSeconds,
    double? Percentage,
    int? LowBoundaryBpm);