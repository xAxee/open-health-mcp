using OpenHealthMCP.Providers.Garmin;

namespace OpenHealthMCP.Tests;

public sealed class GarminProfilePayloadParserTests
{
    [Fact]
    public void ParseProfile_UsesConfirmedGarminValuesWithoutInference()
    {
        using var settingsDocument = FixtureLoader.Load("user-settings.json");
        using var fitnessAgeDocument = FixtureLoader.Load("fitness-age.json");
        using var zonesDocument = FixtureLoader.Load("configured-hr-zones.json");

        var settings = GarminProfilePayloadParser.ParseSettings(settingsDocument.RootElement);
        var fitnessAge = GarminProfilePayloadParser.ParseFitnessAge(fitnessAgeDocument.RootElement);
        var zone = Assert.Single(GarminProfilePayloadParser.ParseConfiguredZones(zonesDocument.RootElement));

        Assert.Equal(52, settings.Vo2MaxRunning);
        Assert.Equal(49, settings.Vo2MaxCycling);
        Assert.Equal(35.5, fitnessAge.FitnessAge);
        Assert.Equal(196, zone.MaxHeartRateUsed);
        Assert.Equal([96d, 116d, 136d, 156d, 176d],
            new[] { zone.Zone1Floor, zone.Zone2Floor, zone.Zone3Floor, zone.Zone4Floor, zone.Zone5Floor });
    }

    [Fact]
    public void ParseBodyComposition_ConvertsConfirmedGarminGramFieldsToKilograms()
    {
        using var document = FixtureLoader.Load("body-composition.json");

        var result = Assert.Single(GarminProfilePayloadParser.ParseBodyComposition(document.RootElement));

        Assert.Equal("700001", result.ExternalId);
        Assert.Equal(new DateOnly(2026, 8, 31), result.LocalDate);
        Assert.Equal(74.2, result.WeightKilograms);
        Assert.Equal(59.8, result.MuscleMassKilograms);
        Assert.Equal(3.4, result.BoneMassKilograms);
        Assert.Equal(14.8, result.BodyFatPercent);
        Assert.Equal(61.2, result.BodyWaterPercent);
    }

    [Fact]
    public void ParseBloodPressure_PreservesSparseMeasurementAndLocalTime()
    {
        using var document = FixtureLoader.Load("blood-pressure.json");

        var result = Assert.Single(GarminProfilePayloadParser.ParseBloodPressure(document.RootElement));

        Assert.Equal(118, result.Systolic);
        Assert.Equal(76, result.Diastolic);
        Assert.Equal(54, result.Pulse);
        Assert.Equal(DateTimeKind.Unspecified, result.TimestampLocal?.Kind);
        Assert.Equal("MANUAL", result.ProviderSourceType);
    }
}