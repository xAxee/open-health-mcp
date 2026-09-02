namespace OpenHealthMCP.Data.Entities;

public sealed class OAuthClient
{
    public long Id { get; set; }
    public required string ClientId { get; set; }
    public required string ClientName { get; set; }
    public required string RedirectUrisJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}