namespace OpenHealthMCP.Sync;

public sealed record SyncRequestResult(
    string Status,
    DateTimeOffset? NextAllowedAt,
    SyncResult? Result);

public sealed record SyncResult(
    DateOnly From,
    DateOnly To,
    int ProvidersCompleted,
    int ChunksCompleted,
    DateTimeOffset CompletedAt);
