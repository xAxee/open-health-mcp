using System.Text.Json;
using OpenHealthMCP.Providers.Garmin;

namespace OpenHealthMCP.Tests;

public sealed class GarminTimeSeriesPayloadParserTests
{
    [Fact]
    public void ParseActivityStream_PreservesMeasuredValuesAndNullSamples()
    {
        using var document = FixtureLoader.Load("activity-details.json");
        var startedAt = DateTimeOffset.FromUnixTimeMilliseconds(1787984100000);

        var parsed = GarminTimeSeriesPayloadParser.ParseActivityStream(document.RootElement, startedAt);
        using var samplesDocument = parsed.Samples;
        var samples = parsed.Samples.RootElement.EnumerateArray().ToArray();

        Assert.Equal(3, parsed.SampleCount);
        Assert.Contains("heartRateBpm", parsed.AvailableMetrics);
        Assert.Contains("powerWatts", parsed.AvailableMetrics);
        Assert.Equal(112, samples[0].GetProperty("heartRateBpm").GetDouble());
        Assert.False(samples[0].TryGetProperty("powerWatts", out _));
        Assert.False(samples[2].TryGetProperty("heartRateBpm", out _));
        Assert.Equal(218, samples[2].GetProperty("powerWatts").GetDouble());
    }

    [Fact]
    public void ParseDescriptorTimeline_DropsProviderSentinelButDoesNotInterpolate()
    {
        using var document = FixtureLoader.Load("stress-body-battery.json");

        var result = GarminTimeSeriesPayloadParser.ParseDescriptorTimeline(
            document.RootElement,
            "stressValueDescriptorsDTOList",
            "stressValuesArray",
            "timestamp",
            "stressLevel");
        using var samplesDocument = result.Samples;

        var samples = result.Samples.RootElement.EnumerateArray().ToArray();
        Assert.Equal(3, result.SampleCount);
        Assert.Equal([18d, 21d, 35d], samples.Select(GetValue).ToArray());
    }

    [Fact]
    public void ParseDescriptorTimeline_PreservesBodyBatteryMeasurementsOnly()
    {
        using var document = FixtureLoader.Load("stress-body-battery.json");

        var result = GarminTimeSeriesPayloadParser.ParseDescriptorTimeline(
            document.RootElement,
            "bodyBatteryValueDescriptorsDTOList",
            "bodyBatteryValuesArray",
            "timestamp",
            "bodyBatteryLevel");
        using var samplesDocument = result.Samples;

        var samples = result.Samples.RootElement.EnumerateArray().ToArray();
        Assert.Equal(3, result.SampleCount);
        Assert.Equal([80d, 79d, 76d], samples.Select(GetValue).ToArray());
    }

    private static double GetValue(JsonElement sample) =>
        sample.TryGetProperty("Value", out var pascalCase)
            ? pascalCase.GetDouble()
            : sample.GetProperty("value").GetDouble();
}