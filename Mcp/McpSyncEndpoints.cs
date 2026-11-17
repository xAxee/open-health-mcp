using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data;
using OpenHealthMCP.Sync;

namespace OpenHealthMCP.Mcp;

public static class McpSyncEndpoints
{
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
            return Results.Ok(await syncService.RequestRecentSyncAsync(cancellationToken));
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
        return Results.Ok(await SyncTools.GetSyncStatusAsync(dbContextFactory, cancellationToken));
    }
}

public sealed record LastSynchronizationResult(
    string Source,
    DateTimeOffset? LastSuccessfulSyncAt,
    DateTimeOffset? LastAttemptAt,
    string? LastError);
