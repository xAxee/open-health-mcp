using OpenHealthMCP.Mcp;

namespace OpenHealthMCP.Tests;

public sealed class ActivityZoneMapperTests
{
    [Fact]
    public void Map_DerivesHighBoundaryOnlyFromNextGarminBoundary()
    {
        var result = ActivityZoneMapper.Map(
        [
            new ActivityZoneRow(1, 600, 10, 95),
            new ActivityZoneRow(2, 1800, 30, 115),
            new ActivityZoneRow(3, 2400, 40, 135),
            new ActivityZoneRow(4, 900, 15, 155),
            new ActivityZoneRow(5, 300, 5, 175)
        ]);

        Assert.Equal(114, result[0].HighBoundaryBpm);
        Assert.Equal(134, result[1].HighBoundaryBpm);
        Assert.Null(result[^1].HighBoundaryBpm);
        Assert.Equal("next-garmin-zone-floor-minus-one-v1", result[0].BoundariesSource.Algorithm);
        Assert.Equal("garmin_api", result[^1].BoundariesSource.Source);
        Assert.Equal("derived_by_openhealth", result[0].PercentageSource.Source);
    }

    [Fact]
    public void Map_DoesNotGuessWhenNextGarminBoundaryIsMissing()
    {
        var result = ActivityZoneMapper.Map(
        [
            new ActivityZoneRow(1, 600, 50, 95),
            new ActivityZoneRow(2, 600, 50, null)
        ]);

        Assert.Null(result[0].HighBoundaryBpm);
        Assert.Null(result[1].LowBoundaryBpm);
        Assert.Null(result[1].HighBoundaryBpm);
    }
}