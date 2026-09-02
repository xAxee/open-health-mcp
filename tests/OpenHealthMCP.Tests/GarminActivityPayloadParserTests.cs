using OpenHealthMCP.Providers.Garmin;

namespace OpenHealthMCP.Tests;

public sealed class GarminActivityPayloadParserTests
{
    [Theory]
    [InlineData("activity-hiking.json", 14400, 16200, 13820, 1320.2, "steps_per_minute")]
    [InlineData("activity-walking.json", 2700, 2820, 2600, null, "steps_per_minute")]
    [InlineData("activity-strength.json", 3300, 3600, 2050, null, null)]
    [InlineData("activity-climbing.json", 5400, 5700, 2900, 360.0, null)]
    public void ParseSummary_PreservesActivitySpecificOptionalValues(
        string fixture,
        double duration,
        double elapsedDuration,
        double movingDuration,
        double? elevationGain,
        string? cadenceUnit)
    {
        using var document = FixtureLoader.Load(fixture);

        var result = GarminActivityPayloadParser.ParseSummary(document.RootElement);

        Assert.Equal(duration, result.DurationSeconds);
        Assert.Equal(elapsedDuration, result.ElapsedDurationSeconds);
        Assert.Equal(movingDuration, result.MovingDurationSeconds);
        Assert.Equal(elevationGain, result.ElevationGainMeters);
        Assert.Equal(cadenceUnit, result.CadenceUnit);
    }

    [Fact]
    public void ParseSummary_PreservesGarminTrainingAndPowerValues()
    {
        using var document = FixtureLoader.Load("activity-hiking.json");

        var result = GarminActivityPayloadParser.ParseSummary(document.RootElement);

        Assert.Equal(228, result.AveragePowerWatts);
        Assert.Equal(615, result.MaxPowerWatts);
        Assert.Equal(251, result.NormalizedPowerWatts);
        Assert.Equal(4.1, result.AerobicTrainingEffect);
        Assert.Equal(1.2, result.AnaerobicTrainingEffect);
        Assert.Equal(286, result.TrainingLoad);
        Assert.Equal(52, result.Vo2Max);
    }

    [Fact]
    public void ParseLaps_PreservesTimingElevationAndCadence()
    {
        using var document = FixtureLoader.Load("activity-laps.json");

        var lap = Assert.Single(GarminActivityPayloadParser.ParseLaps(document.RootElement));

        Assert.Equal(1, lap.LapIndex);
        Assert.Equal(1800, lap.DurationSeconds);
        Assert.Equal(1950, lap.ElapsedDurationSeconds);
        Assert.Equal(1740, lap.MovingDurationSeconds);
        Assert.Equal(310, lap.ElevationGainMeters);
        Assert.Equal(15, lap.ElevationLossMeters);
        Assert.Equal(104, lap.AverageCadence);
        Assert.Equal("steps_per_minute", lap.CadenceUnit);
    }

    [Fact]
    public void ParseHeartRateZones_UsesGarminBoundariesWithoutAgeBasedReplacement()
    {
        using var document = FixtureLoader.Load("activity-hr-zones.json");

        var zones = GarminActivityPayloadParser.ParseHeartRateZones(document.RootElement);

        Assert.Equal(5, zones.Count);
        Assert.Collection(
            zones,
            zone => Assert.Equal(95, zone.LowBoundaryBpm),
            zone => Assert.Equal(115, zone.LowBoundaryBpm),
            zone => Assert.Equal(135, zone.LowBoundaryBpm),
            zone => Assert.Equal(155, zone.LowBoundaryBpm),
            zone => Assert.Equal(175, zone.LowBoundaryBpm));
        Assert.Equal(10, zones[0].Percentage);
        Assert.Equal(30, zones[1].Percentage);
        Assert.Equal(40, zones[2].Percentage);
    }
}