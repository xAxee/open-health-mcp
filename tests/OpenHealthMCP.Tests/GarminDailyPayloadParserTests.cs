using OpenHealthMCP.Providers.Garmin;

namespace OpenHealthMCP.Tests;

public sealed class GarminDailyPayloadParserTests
{
    [Fact]
    public void ParseSummary_MapsConfirmedProviderValuesAndDerivedIntensityTotal()
    {
        using var document = FixtureLoader.Load("daily-full.json");

        var result = GarminDailyPayloadParser.ParseSummary(document.RootElement);

        Assert.Equal(new DateOnly(2026, 8, 31), result.CalendarDate);
        Assert.Equal(120, result.UtcOffsetMinutes);
        Assert.Equal(9876, result.DistanceMeters);
        Assert.Equal(5400, result.ActiveSeconds);
        Assert.Equal(1900, result.BmrCalories);
        Assert.Equal(12, result.FloorsClimbed);
        Assert.Equal(10000, result.StepsGoal);
        Assert.Equal(10, result.FloorsGoal);
        Assert.Equal(150, result.IntensityGoal);
        Assert.Equal(40, result.TotalIntensityMinutes);
        Assert.Equal(78, result.StressMax);
        Assert.Equal("BALANCED", result.StressQualifier);
        Assert.Equal(61, result.BodyBatteryCharged);
        Assert.Equal(68, result.BodyBatteryDrained);
        Assert.Equal(91, result.MinimumSpo2);
        Assert.Equal(19.1, result.MaximumRespirationRate);
    }

    [Fact]
    public void ParseSummary_PreservesMissingAndNullValues()
    {
        using var document = FixtureLoader.Load("daily-partial.json");

        var result = GarminDailyPayloadParser.ParseSummary(document.RootElement);

        Assert.Equal(new DateOnly(2026, 8, 30), result.CalendarDate);
        Assert.Null(result.DistanceMeters);
        Assert.Null(result.BmrCalories);
        Assert.Null(result.StressMax);
        Assert.Null(result.TotalIntensityMinutes);
        Assert.Null(result.MinimumSpo2);
    }

    [Fact]
    public void ParseSleep_MapsTimeBasesSubscoresAndMeasuredSeries()
    {
        using var document = FixtureLoader.Load("sleep-full.json");

        var summary = GarminDailyPayloadParser.ParseSleep(document.RootElement);
        var stages = GarminDailyPayloadParser.ParseSleepStages(document.RootElement);
        var respiration = GarminDailyPayloadParser.ParseSleepRespiration(document.RootElement);

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1788125400000), summary.SleepStartUtc);
        Assert.Equal(DateTimeKind.Unspecified, summary.SleepStartLocal?.Kind);
        Assert.Equal("GOOD", summary.SleepQualifier);
        Assert.Contains("deepPercentage", summary.SubScoresJson);
        Assert.Equal(4, stages.Count);
        Assert.All(stages, stage => Assert.Null(stage.TextValue));
        Assert.Equal([0d, 1d, 2d, 3d], stages.Select(stage => stage.NumericValue).ToArray());
        Assert.Equal(3, respiration.Count);
        Assert.Equal(13.2, respiration[0].Value);
    }

    [Fact]
    public void ParseHrv_MapsSummaryWithoutFabricatingReadings()
    {
        using var document = FixtureLoader.Load("hrv-full.json");

        var result = GarminDailyPayloadParser.ParseHrv(document.RootElement, new DateOnly(2026, 8, 31));

        Assert.Equal(48, result.LastNightAverage);
        Assert.Equal(71, result.FiveMinuteHigh);
        Assert.Equal("BALANCED", result.Status);
    }

    [Fact]
    public void ParseSpo2AndRespiration_MapConfirmedAggregatesAndMeasuredPoints()
    {
        using var spo2Document = FixtureLoader.Load("spo2-full.json");
        using var respirationDocument = FixtureLoader.Load("respiration-full.json");

        var spo2 = GarminDailyPayloadParser.ParseSpo2(spo2Document.RootElement);
        var respiration = GarminDailyPayloadParser.ParseRespiration(respirationDocument.RootElement);
        var series = GarminTimeSeriesPayloadParser.ParseDescriptorTimeline(
            respirationDocument.RootElement,
            "respirationValueDescriptorsDTOList",
            "respirationValuesArray",
            "timestamp",
            "respirationValue");
        using var seriesDocument = series.Samples;

        Assert.Equal(96.4, spo2.Average);
        Assert.Equal(91, spo2.Minimum);
        Assert.NotNull(spo2.WindowStartUtc);
        Assert.Equal(12.9, respiration.SleepAverage);
        Assert.Equal(3, series.SampleCount);
        Assert.Equal([13.8, 14.1, 14.4], series.Points.Select(point => point.Value).ToArray());
    }

    [Theory]
    [InlineData("2026-08-31T04:00:00Z", "2026-08-31T06:00:00", 120)]
    [InlineData("2026-08-31T04:00:00Z", "2026-08-30T23:00:00", -300)]
    public void CalculateUtcOffsetMinutes_SupportsPositiveAndNegativeOffsets(
        string utc,
        string local,
        int expected)
    {
        var utcTimestamp = DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture);
        var localTimestamp = DateTime.SpecifyKind(
            DateTime.Parse(local, System.Globalization.CultureInfo.InvariantCulture),
            DateTimeKind.Unspecified);

        Assert.Equal(expected, GarminDailyPayloadParser.CalculateUtcOffsetMinutes(utcTimestamp, localTimestamp));
    }
}