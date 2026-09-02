namespace OpenHealthMCP.OAuth;

public sealed class OAuthOptions
{
    public const string ReadScope = "health.read";

    public required string BaseUrl { get; init; }
    public required string ResourceUrl { get; init; }
    public required string OwnerPassword { get; init; }

    public static OAuthOptions FromConfiguration(IConfiguration configuration)
    {
        var configuredBaseUrl = configuration["OAUTH_BASE_URL"];
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri) ||
            !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            baseUri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new InvalidOperationException("OAUTH_BASE_URL must be an HTTPS origin without a path, query, or fragment.");
        }

        var ownerPassword = configuration["OAUTH_OWNER_PASSWORD"];
        if (string.IsNullOrWhiteSpace(ownerPassword) || ownerPassword.Length < 32)
        {
            throw new InvalidOperationException("OAUTH_OWNER_PASSWORD must be configured with at least 32 characters.");
        }

        var baseUrl = baseUri.GetLeftPart(UriPartial.Authority);
        return new OAuthOptions
        {
            BaseUrl = baseUrl,
            ResourceUrl = $"{baseUrl}/mcp",
            OwnerPassword = ownerPassword
        };
    }
}