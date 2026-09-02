namespace OpenHealthMCP.Sync;

public sealed record SyncResult(
    DateOnly From,
    DateOnly To,
    int ProvidersCompleted,
    int ChunksCompleted,
    DateTimeOffset CompletedAt);