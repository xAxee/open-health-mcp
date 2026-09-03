using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data;
using OpenHealthMCP.Data.Entities;

namespace OpenHealthMCP.Tests;

public sealed class AppDbContextModelTests
{
    [Fact]
    public void Model_DefinesCanonicalSeriesAndRawRevisionIndexes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only;Password=model_only")
            .Options;
        using var context = new AppDbContext(options);

        var healthIndexes = context.Model.FindEntityType(typeof(HealthMetricSample))!
            .GetIndexes()
            .Select(index => (Properties: string.Join(",", index.Properties.Select(property => property.Name)), index.IsUnique))
            .ToArray();
        var activityIndexes = context.Model.FindEntityType(typeof(ActivitySample))!
            .GetIndexes()
            .Select(index => (Properties: string.Join(",", index.Properties.Select(property => property.Name)), index.IsUnique))
            .ToArray();
        var rawIndexes = context.Model.FindEntityType(typeof(RawProviderData))!
            .GetIndexes()
            .Select(index => (Properties: string.Join(",", index.Properties.Select(property => property.Name)), index.IsUnique))
            .ToArray();

        Assert.Contains(("Source,Metric,TimestampUtc", true), healthIndexes);
        Assert.Contains(("Source,LocalDate,Metric", false), healthIndexes);
        Assert.Contains(("ActivityId,ElapsedSeconds", true), activityIndexes);
        Assert.Contains(("ActivityId,TimestampUtc", false), activityIndexes);
        Assert.Contains(("Source,DataType,ExternalId,PayloadHash", true), rawIndexes);
    }
}