using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using OpenHealthMCP.Data;
using OpenHealthMCP.Sync;

namespace OpenHealthMCP.Mcp;

[McpServerToolType]
public sealed class SyncTools
{
    [McpServerTool(Name = "sync_recent", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Synchronizes recent Garmin health data, activities and sleep into the local database using the configured lookback window. A fixed five-minute cooldown since the last provider attempt applies, including failed and scheduled attempts. During cooldown returns status=cooldown and nextAllowedAt without contacting Garmin. Otherwise waits for completion and returns status=completed with result. No preflight check for new data is performed. Writes local data; does not modify Garmin or trigger a watch upload.")]
    public static Task<SyncRequestResult> SyncRecentAsync(
        HealthSyncService syncService,
        CancellationToken cancellationToken) => syncService.RequestRecentSyncAsync(cancellationToken);

    [McpServerTool(Name = "get_sync_status", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns the last successful Garmin synchronization time, last attempt time and last synchronization error. A null success time means no successful synchronization yet. Reads local state only; it does not check Garmin for new activities or sleep. Use before sync_recent to decide whether a refresh is needed.")]
    public static async Task<LastSynchronizationResult> GetSyncStatusAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SyncStates.AsNoTracking()
            .Where(item => item.Source == "garmin")
            .Select(item => new LastSynchronizationResult(
                item.Source, item.LastSuccessfulSyncAt, item.LastAttemptAt, item.LastError))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new LastSynchronizationResult("garmin", null, null, null);
    }
}
