namespace OpenHealthMCP.Data.Entities;

public sealed class OAuthToken
{
    public long Id { get; set; }
    public required string TokenHash { get; set; }
    public required string TokenType { get; set; }
    public required string ClientId { get; set; }
    public required string Scope { get; set; }
    public required string Resource { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}