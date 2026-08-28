using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data;
using OpenHealthMCP.Data.Entities;
using OpenHealthMCP.Providers;

namespace OpenHealthMCP.Sync;

public sealed class HealthSyncService(
    IEnumerable<IHealthDataProvider> providers,
    IDbContextFactory<AppDbContext> dbContextFactory,
    SyncOptions options,
    TimeProvider timeProvider,
    ILogger<HealthSyncService> logger) : BackgroundService
{
    private readonly IReadOnlyList<IHealthDataProvider> _providers = providers.ToArray();
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task<SyncResult> SyncRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        ValidateRange(from, to);
        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            logger.LogInformation("Health synchronization started for {From} through {To}", from, to);
            var chunksCompleted = 0;
            var providersCompleted = 0;

            foreach (var provider in _providers)
            {
                await RecordAttemptAsync(provider.Name, cancellationToken);

                try
                {
                    var chunkStart = from;
                    while (chunkStart <= to)
                    {
                        var chunkEnd = Min(chunkStart.AddDays(options.HistoricalChunkDays - 1), to);
                        logger.LogInformation(
                            "Synchronizing {Source} range {From} through {To}",
                            provider.Name,
                            chunkStart,
                            chunkEnd);

                        await provider.SyncAsync(chunkStart, chunkEnd, cancellationToken);
                        chunksCompleted++;
                        chunkStart = chunkEnd.AddDays(1);
                    }

                    await RecordSuccessAsync(provider.Name, cancellationToken);
                    providersCompleted++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    var safeError = SafeError(exception);
                    await RecordFailureAsync(provider.Name, safeError, cancellationToken);
                    logger.LogError(
                        "Synchronization failed for {Source}: {Error}",
                        provider.Name,
                        safeError);
                    throw new InvalidOperationException(
                        $"Synchronization failed for provider '{provider.Name}': {safeError}");
                }
            }

            var completedAt = timeProvider.GetUtcNow();
            logger.LogInformation("Health synchronization completed at {CompletedAt}", completedAt);
            return new SyncResult(from, to, providersCompleted, chunksCompleted, completedAt);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Scheduled synchronization enabled every {IntervalHours} hours with {LookbackDays}-day lookback",
            options.IntervalHours,
            options.LookbackDays);

        await RunScheduledSyncAsync(stoppingToken);

        using var timer = new PeriodicTimer(
            TimeSpan.FromHours(options.IntervalHours),
            timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunScheduledSyncAsync(stoppingToken);
        }
    }

    private async Task RunScheduledSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            await SyncRangeAsync(today.AddDays(-(options.LookbackDays - 1)), today, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Scheduled synchronization failed: {Error}", SafeError(exception));
        }
    }

    private async Task RecordAttemptAsync(string source, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await GetOrCreateStateAsync(dbContext, source, cancellationToken);
        state.LastAttemptAt = timeProvider.GetUtcNow();
        state.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordSuccessAsync(string source, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await GetOrCreateStateAsync(dbContext, source, cancellationToken);
        state.LastSuccessfulSyncAt = timeProvider.GetUtcNow();
        state.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(
        string source,
        string error,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await GetOrCreateStateAsync(dbContext, source, cancellationToken);
        state.LastError = error.Length <= 2000 ? error : error[..2000];
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<SyncState> GetOrCreateStateAsync(
        AppDbContext dbContext,
        string source,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.SyncStates.SingleOrDefaultAsync(
            item => item.Source == source,
            cancellationToken);

        if (state is not null)
        {
            return state;
        }

        state = new SyncState { Source = source };
        dbContext.SyncStates.Add(state);
        return state;
    }

    private void ValidateRange(DateOnly from, DateOnly to)
    {
        if (from > to)
        {
            throw new ArgumentException("The synchronization start date must not be after the end date.");
        }

        if (to > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
        {
            throw new ArgumentException("The synchronization range must not extend into the future.");
        }

        if (from < new DateOnly(2000, 1, 1))
        {
            throw new ArgumentException("The synchronization start date must be on or after 2000-01-01.");
        }
    }

    private static DateOnly Min(DateOnly left, DateOnly right) => left <= right ? left : right;

    private static string SafeError(Exception exception) => exception.GetBaseException().Message;
}