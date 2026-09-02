using System.Text.Json.Serialization;

namespace OpenHealthMCP.OAuth;

public sealed record OAuthClientRegistrationRequest(
    [property: JsonPropertyName("redirect_uris")] string[]? RedirectUris,
    [property: JsonPropertyName("client_name")] string? ClientName,
    [property: JsonPropertyName("grant_types")] string[]? GrantTypes,
    [property: JsonPropertyName("response_types")] string[]? ResponseTypes,
    [property: JsonPropertyName("token_endpoint_auth_method")] string? TokenEndpointAuthMethod);

public sealed record OAuthClientRegistrationResponse(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_id_issued_at")] long ClientIdIssuedAt,
    [property: JsonPropertyName("redirect_uris")] string[] RedirectUris,
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("grant_types")] string[] GrantTypes,
    [property: JsonPropertyName("response_types")] string[] ResponseTypes,
    [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod);

public sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("scope")] string Scope);

public sealed record OAuthErrorResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string Description);

public sealed record OAuthAuthorizationRequest(
    string ClientId,
    string RedirectUri,
    string State,
    string CodeChallenge,
    string Scope,
    string Resource);

public sealed record OAuthTokenPair(string AccessToken, string RefreshToken, int ExpiresIn, string Scope);