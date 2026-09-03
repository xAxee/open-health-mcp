using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data;
using OpenHealthMCP.Data.Entities;
using OpenHealthMCP.Mcp;

namespace OpenHealthMCP.Tests;

public sealed class SeriesToolsPostgresTests
{
    [Fact]
    public async Task SeriesTools_QueryCanonicalSamplesOnPostgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("OPENHEALTH_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        var factory = new TestDbContextFactory(options);
        await using (var context = factory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
            await context.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE health_metric_samples, activity_samples, activities, daily_metrics RESTART IDENTITY CASCADE");
            context.DailyMetrics.Add(new DailyMetric
            {
                Source = "garmin",
                Date = new DateOnly(2026, 8, 31),
                UtcOffsetMinutes = 120,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            for (var index = 0; index < 10; index++)
            {
                context.HealthMetricSamples.Add(new HealthMetricSample
                {
                    Source = "garmin",
                    Metric = index % 2 == 0 ? "heart_rate" : "stress",
                    LocalDate = new DateOnly(2026, 8, 31),
                    TimestampUtc = new DateTimeOffset(2026, 8, 31, 0, index, 0, TimeSpan.Zero),
                    ValueNumeric = 50 + index,
                    Unit = index % 2 == 0 ? "bpm" : "score",
                    SourceType = "garmin_api",
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }

            var activity = new Activity
            {
                Source = "garmin",
                ExternalId = "900000001",
                Name = "Sanitized hike",
                ActivityType = "hiking",
                StartedAt = new DateTimeOffset(2026, 8, 31, 6, 0, 0, TimeSpan.Zero),
                StreamsSyncedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.Activities.Add(activity);
            context.ActivitySamples.AddRange(
                ActivityPoint(activity, 0, 100, null),
                ActivityPoint(activity, 1, 110, 200),
                ActivityPoint(activity, 2, null, 220));
            await context.SaveChangesAsync();
        }

        var daily = await SeriesTools.GetDaySeriesAsync(
            "2026-08-31",
            "heart_rate,stress",
            factory,
            CancellationToken.None,
            "2026-08-31T00:02:00Z",
            "2026-08-31T00:08:00Z",
            maxPoints: 3);
        var activitySeries = await SeriesTools.GetActivitySeriesAsync(
            "900000001", factory, CancellationToken.None, "heart_rate,power", interval: "5s");

        Assert.Equal(7, daily.OriginalPointCount);
        Assert.Equal(3, daily.ReturnedPointCount);
        Assert.True(daily.Downsampled);
        Assert.Equal("180s", daily.EffectiveInterval);
        Assert.Equal(120, daily.UtcOffsetMinutes);
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 0, 2, 0, TimeSpan.Zero), daily.Points[0].TimestampUtc);
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 0, 8, 0, TimeSpan.Zero), daily.Points[^1].TimestampUtc);

        Assert.True(activitySeries.Found);
        Assert.True(activitySeries.Synchronized);
        Assert.Equal(3, activitySeries.OriginalPointCount);
        var point = Assert.Single(activitySeries.Points);
        Assert.Equal(105, point.Values["heart_rate"]);
        Assert.Equal(210, point.Values["power"]);
        Assert.Equal(3, point.MeasurementCount);
        Assert.Equal("derived_by_openhealth", point.SourceType);
        Assert.Equal("5s", activitySeries.RequestedInterval);
    }

    private static ActivitySample ActivityPoint(
        Activity activity,
        double elapsed,
        double? heartRate,
        double? power) => new()
    {
        Activity = activity,
        ElapsedSeconds = elapsed,
        HeartRateBpm = heartRate,
        PowerWatts = power,
        SourceType = "garmin_api",
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}