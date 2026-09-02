namespace OpenHealthMCP.Admin;

public sealed record AdminSyncRequest(DateOnly From, DateOnly To);