using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenHealthMCP.Data;
using OpenHealthMCP.Mcp;
using OpenHealthMCP.Providers;
using OpenHealthMCP.Sync;

namespace OpenHealthMCP.Tests;

public sealed class SyncToolsTests
{
    [Fact]
    public async Task StatusBeforeFirstSyncHasNullTimestamps()
    {
        var status = await SyncTools.GetSyncStatusAsync(new TestDbFactory(), CancellationToken.None);
        Assert.Equal("garmin", status.Source);
        Assert.Null(status.LastSuccessfulSyncAt);
        Assert.Null(status.LastAttemptAt);
        Assert.Null(status.LastError);
    }

    [Fact]
    public async Task SyncRefreshesLookbackAndUpdatesStatus()
    {
        var factory = new TestDbFactory();
        var provider = new TestProvider();
        using var service = CreateService(factory, provider);

        var response = await SyncTools.SyncRecentAsync(service, CancellationToken.None);
        Assert.Equal("completed", response.Status);
        var result = Assert.IsType<SyncResult>(response.Result);

        Assert.Equal(new DateOnly(2026, 11, 15), result.From);
        Assert.Equal(new DateOnly(2026, 11, 17), result.To);
        Assert.Equal((result.From, result.To), provider.Range);
        Assert.Equal(1, result.ProvidersCompleted);
        Assert.Equal(1, result.ChunksCompleted);
        var status = await SyncTools.GetSyncStatusAsync(factory, CancellationToken.None);
        Assert.Equal(result.CompletedAt, status.LastSuccessfulSyncAt);
        Assert.Equal(result.CompletedAt, status.LastAttemptAt);
        Assert.Null(status.LastError);
    }

    [Fact]
    public async Task ProviderFailurePropagatesAndPreservesPreviousSuccess()
    {
        var factory = new TestDbFactory();
        var provider = new TestProvider();
        using var service = CreateService(factory, provider);
        var success = await service.SyncRecentAsync(CancellationToken.None);
        provider.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SyncRecentAsync(CancellationToken.None));

        var status = await SyncTools.GetSyncStatusAsync(factory, CancellationToken.None);
        Assert.Equal(success.CompletedAt, status.LastSuccessfulSyncAt);
        Assert.Contains("Provider unavailable", status.LastError);
    }

    [Fact]
    public async Task CancelledCallDoesNotInvokeProvider()
    {
        var provider = new TestProvider();
        using var service = CreateService(new TestDbFactory(), provider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SyncTools.SyncRecentAsync(service, cancellation.Token));
        Assert.Null(provider.Range);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CooldownSkipsProviderAndAllowsRetryAfterFiveMinutes(bool fail)
    {
        var factory = new TestDbFactory();
        var provider = new TestProvider { Fail = fail };
        var clock = new FixedTimeProvider();
        using var service = new HealthSyncService([provider], factory, new SyncOptions(),
            clock, NullLogger<HealthSyncService>.Instance);
        if (fail)
            await Assert.ThrowsAsync<InvalidOperationException>(() => SyncTools.SyncRecentAsync(service, CancellationToken.None));
        else
            await SyncTools.SyncRecentAsync(service, CancellationToken.None);

        clock.Now = clock.Now.AddMinutes(5).AddTicks(-1);
        var skipped = await SyncTools.SyncRecentAsync(service, CancellationToken.None);
        Assert.Equal("cooldown", skipped.Status);
        Assert.Null(skipped.Result);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(clock.Now.AddTicks(1), skipped.NextAllowedAt);

        provider.Fail = false;
        clock.Now = clock.Now.AddTicks(1);
        var completed = await SyncTools.SyncRecentAsync(service, CancellationToken.None);
        Assert.Equal("completed", completed.Status);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task ScheduledAttemptEnforcesCooldownAfterServiceRecreation()
    {
        var factory = new TestDbFactory();
        var provider = new TestProvider();
        using (var scheduledService = CreateService(factory, provider))
            await scheduledService.SyncRecentAsync(CancellationToken.None);
        using var recreatedService = CreateService(factory, provider);
        var response = await SyncTools.SyncRecentAsync(recreatedService, CancellationToken.None);
        Assert.Equal("cooldown", response.Status);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ConcurrentRequestsOnlySynchronizeOnce()
    {
        var provider = new TestProvider();
        using var service = CreateService(new TestDbFactory(), provider);
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => SyncTools.SyncRecentAsync(service, CancellationToken.None))));
        Assert.Single(responses, response => response.Status == "completed");
        Assert.Equal(9, responses.Count(response => response.Status == "cooldown"));
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task CooldownQueryWorksOnPostgres()
    {
        var connection = Environment.GetEnvironmentVariable("OPENHEALTH_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var factory = new TestDbFactory(connection);
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            await db.SyncStates.ExecuteDeleteAsync();
        }
        var provider = new TestProvider();
        using var service = CreateService(factory, provider);
        var completed = await SyncTools.SyncRecentAsync(service, CancellationToken.None);
        var skipped = await SyncTools.SyncRecentAsync(service, CancellationToken.None);
        Assert.Equal("completed", completed.Status);
        Assert.Equal("cooldown", skipped.Status);
        Assert.Equal(1, provider.Calls);
    }

    private static HealthSyncService CreateService(TestDbFactory factory, TestProvider provider) =>
        new([provider], factory, new SyncOptions { LookbackDays = 3 },
            new FixedTimeProvider(), NullLogger<HealthSyncService>.Instance);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TestDbFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> options;

        public TestDbFactory(string? postgresConnection = null)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>();
            options = postgresConnection is null
                ? builder.UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .UseModel(CreateSyncStateModel()).Options
                : builder.UseNpgsql(postgresConnection).Options;
        }

        // These tests exercise synchronization state only; Garmin payload mappings
        // require PostgreSQL and are covered by the model/PostgreSQL tests.
        private static Microsoft.EntityFrameworkCore.Metadata.IModel CreateSyncStateModel()
        {
            var builder = new ModelBuilder();
            var state = builder.Entity<OpenHealthMCP.Data.Entities.SyncState>();
            state.HasKey(item => item.Id);
            state.Property(item => item.Source);
            state.Property(item => item.LastAttemptAt);
            state.Property(item => item.LastSuccessfulSyncAt);
            state.Property(item => item.LastError);
            return builder.FinalizeModel();
        }
        public AppDbContext CreateDbContext() => new(options);
    }

    private sealed class TestProvider : IHealthDataProvider
    {
        public string Name => "garmin";
        public bool Fail { get; set; }
        public int Calls { get; private set; }
        public (DateOnly From, DateOnly To)? Range { get; private set; }
        public Task SyncAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            Calls++;
            Range = (from, to);
            if (Fail) throw new InvalidOperationException("Provider unavailable");
            return Task.CompletedTask;
        }
    }
}
