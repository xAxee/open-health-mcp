using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpenHealthMCP.OAuth;

namespace OpenHealthMCP.Authentication;

public sealed class McpTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    McpTokenOptions tokenOptions,
    OAuthService oauthService,
    OAuthOptions oauthOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationMethodClaim = "openhealthmcp:authentication_method";
    public const string StaticAuthenticationMethod = "static_token";
    public const string OAuthAuthenticationMethod = "oauth";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var suppliedToken = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(suppliedToken))
        {
            return AuthenticateResult.Fail("The bearer token is missing.");
        }

        var configuredBytes = Encoding.UTF8.GetBytes(tokenOptions.Token);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        if (configuredBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(configuredBytes, suppliedBytes))
        {
            return CreateSuccessResult(StaticAuthenticationMethod);
        }

        if (suppliedToken.StartsWith(OAuthService.AccessTokenPrefix, StringComparison.Ordinal) &&
            await oauthService.ValidateAccessTokenAsync(suppliedToken, Context.RequestAborted))
        {
            return CreateSuccessResult(OAuthAuthenticationMethod);
        }

        return AuthenticateResult.Fail("Invalid bearer token.");
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = Request.Path.StartsWithSegments("/mcp")
            ? $"Bearer resource_metadata=\"{oauthOptions.BaseUrl}/.well-known/oauth-protected-resource/mcp\", " +
                $"scope=\"{OAuthOptions.ReadScope}\""
            : "Bearer";
        return Task.CompletedTask;
    }

    private AuthenticateResult CreateSuccessResult(string authenticationMethod)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "self-hosted-user"),
                new Claim(AuthenticationMethodClaim, authenticationMethod),
                new Claim("scope", OAuthOptions.ReadScope)
            ],
            Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}