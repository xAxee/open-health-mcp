using OpenHealthMCP.Mcp;

namespace OpenHealthMCP.Tests;

public sealed class SeriesProcessingTests
{
    [Theory]
    [InlineData(null, "raw", null)]
    [InlineData("5m", "5m", 300.0)]
    [InlineData("1H", "1h", 3600.0)]
    public void ParseInterval_NormalizesSupportedValues(string? input, string name, double? seconds)
    {
        var result = SeriesProcessing.ParseInterval(input);

        Assert.Equal(name, result.Name);
        Assert.Equal(seconds, result.Seconds);
    }

    [Fact]
    public void Downsample_IsDeterministicAndRetainsEndpoints()
    {
        var input = Enumerable.Range(0, 101).ToArray();

        var first = SeriesProcessing.Downsample(input, 10);
        var second = SeriesProcessing.Downsample(input, 10);

        Assert.Equal(first, second);
        Assert.Equal(0, first[0]);
        Assert.Equal(100, first[^1]);
        Assert.Equal(10, first.Count);
    }

    [Fact]
    public void Resolution_UsesMeasuredOffsetsWithoutInterpolation()
    {
        Assert.Equal("5s", SeriesProcessing.Resolution([0, 5, 10, 30]));
        Assert.Equal("unknown", SeriesProcessing.Resolution([5]));
    }
}