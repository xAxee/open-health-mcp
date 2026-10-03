using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data;
using OpenHealthMCP.Sync;

namespace OpenHealthMCP.Mcp;

public static class McpSyncEndpoints
{
    private const string GarminSource = "garmin";

    public static IEndpointRouteBuilder MapMcpSyncEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/mcp/sync")
            .RequireAuthorization("McpAccess");

        group.MapPost("", SynchronizeAsync);
        group.MapGet("", GetLastSynchronizationAsync);

        return endpoints;
    }

    private static async Task<IResult> SynchronizeAsync(
        HealthSyncService syncService,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await syncService.SyncRecentAsync(cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Synchronization failed",
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> GetLastSynchronizationAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await dbContext.SyncStates.AsNoTracking()
            .Where(item => item.Source == GarminSource)
            .Select(item => new LastSynchronizationResult(
                item.Source,
                item.LastSuccessfulSyncAt,
                item.LastAttemptAt,
                item.LastError))
            .SingleOrDefaultAsync(cancellationToken);

        return Results.Ok(state ?? new LastSynchronizationResult(GarminSource, null, null, null));
    }
}

public sealed record LastSynchronizationResult(
    string Source,
    DateTimeOffset? LastSuccessfulSyncAt,
    DateTimeOffset? LastAttemptAt,
    string? LastError);
