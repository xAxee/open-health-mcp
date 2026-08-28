namespace OpenHealthMCP.Data.Entities;

public sealed class SyncState
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
}