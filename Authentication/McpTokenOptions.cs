namespace OpenHealthMCP.Authentication;

public sealed class McpTokenOptions
{
    public const string Scheme = "McpBearer";

    public required string Token { get; init; }

    public static McpTokenOptions FromConfiguration(IConfiguration configuration)
    {
        var token = configuration["MCP_AUTH_TOKEN"];
        if (string.IsNullOrWhiteSpace(token) || token.Length < 32)
        {
            throw new InvalidOperationException(
                "MCP_AUTH_TOKEN must be configured with at least 32 characters.");
        }

        return new McpTokenOptions { Token = token };
    }
}