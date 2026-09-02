namespace OpenHealthMCP.Data.Entities;

public sealed class OAuthAuthorizationCode
{
    public long Id { get; set; }
    public required string CodeHash { get; set; }
    public required string ClientId { get; set; }
    public required string RedirectUri { get; set; }
    public required string CodeChallenge { get; set; }
    public required string Scope { get; set; }
    public required string Resource { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}