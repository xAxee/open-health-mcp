using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data;
using OpenHealthMCP.Data.Entities;

namespace OpenHealthMCP.OAuth;

public sealed class OAuthService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    OAuthOptions options,
    TimeProvider timeProvider)
{
    public const string AccessTokenPrefix = "ohat_";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan AuthorizationCodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<OAuthClientRegistrationResponse> RegisterClientAsync(
        OAuthClientRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var redirectUris = ValidateRegistration(request);
        var now = timeProvider.GetUtcNow();
        var clientId = $"ohmcp_{CreateRandomValue(24)}";
        var clientName = string.IsNullOrWhiteSpace(request.ClientName)
            ? "MCP client"
            : request.ClientName.Trim();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.OAuthClients.Add(new OAuthClient
        {
            ClientId = clientId,
            ClientName = clientName,
            RedirectUrisJson = JsonSerializer.Serialize(redirectUris, JsonOptions),
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new OAuthClientRegistrationResponse(
            clientId,
            now.ToUnixTimeSeconds(),
            redirectUris,
            clientName,
            ["authorization_code", "refresh_token"],
            ["code"],
            "none");
    }

    public async Task<OAuthAuthorizationRequest?> ValidateAuthorizationRequestAsync(
        IQueryCollection query,
        CancellationToken cancellationToken)
    {
        var responseType = query["response_type"].ToString();
        var clientId = query["client_id"].ToString();
        var redirectUri = query["redirect_uri"].ToString();
        var state = query["state"].ToString();
        var codeChallenge = query["code_challenge"].ToString();
        var codeChallengeMethod = query["code_challenge_method"].ToString();
        var requestedScope = query["scope"].ToString();
        var scope = string.IsNullOrWhiteSpace(requestedScope)
            ? OAuthOptions.ReadScope
            : NormalizeScope(requestedScope);
        var resource = query["resource"].ToString();

        if (responseType != "code" ||
            string.IsNullOrWhiteSpace(clientId) || clientId.Length > 100 ||
            string.IsNullOrWhiteSpace(redirectUri) || redirectUri.Length > 2000 ||
            string.IsNullOrWhiteSpace(state) || state.Length > 2000 ||
            !IsValidCodeChallenge(codeChallenge) ||
            codeChallengeMethod != "S256" ||
            scope != OAuthOptions.ReadScope ||
            resource.Length > 2000 || resource != options.ResourceUrl)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var client = await dbContext.OAuthClients
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientId == clientId, cancellationToken);
        if (client is null || !GetRedirectUris(client).Contains(redirectUri, StringComparer.Ordinal))
        {
            return null;
        }

        return new OAuthAuthorizationRequest(
            clientId,
            redirectUri,
            state,
            codeChallenge,
            scope,
            resource);
    }

    public bool ValidateOwnerPassword(string? suppliedPassword)
    {
        if (string.IsNullOrEmpty(suppliedPassword))
        {
            return false;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(options.OwnerPassword);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedPassword);
        return configuredBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(configuredBytes, suppliedBytes);
    }

    public async Task<string> CreateAuthorizationCodeAsync(
        OAuthAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        var code = $"ohac_{CreateRandomValue(32)}";
        var now = timeProvider.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.OAuthAuthorizationCodes.Add(new OAuthAuthorizationCode
        {
            CodeHash = HashValue(code),
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            CodeChallenge = request.CodeChallenge,
            Scope = request.Scope,
            Resource = request.Resource,
            CreatedAt = now,
            ExpiresAt = now.Add(AuthorizationCodeLifetime)
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return code;
    }

    public async Task<OAuthTokenPair?> ExchangeAuthorizationCodeAsync(
        string? code,
        string? clientId,
        string? redirectUri,
        string? codeVerifier,
        string? resource,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            code.Length > 100 ||
            string.IsNullOrWhiteSpace(clientId) || clientId.Length > 100 ||
            string.IsNullOrWhiteSpace(redirectUri) || redirectUri.Length > 2000 ||
            !IsValidCodeVerifier(codeVerifier) ||
            resource is null || resource.Length > 2000 || resource != options.ResourceUrl)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var codeHash = HashValue(code);
        var authorizationCode = await dbContext.OAuthAuthorizationCodes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CodeHash == codeHash, cancellationToken);

        if (authorizationCode is null ||
            authorizationCode.ExpiresAt <= now ||
            authorizationCode.ClientId != clientId ||
            authorizationCode.RedirectUri != redirectUri ||
            authorizationCode.Resource != resource ||
            !VerifyCodeChallenge(codeVerifier!, authorizationCode.CodeChallenge))
        {
            return null;
        }

        var consumed = await dbContext.OAuthAuthorizationCodes
            .Where(item => item.Id == authorizationCode.Id && item.CodeHash == codeHash)
            .ExecuteDeleteAsync(cancellationToken);
        if (consumed != 1)
        {
            return null;
        }

        await DeleteExpiredGrantsAsync(dbContext, now, cancellationToken);
        var tokenPair = AddTokenPair(dbContext, clientId, authorizationCode.Scope, resource, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return tokenPair;
    }

    public async Task<OAuthTokenPair?> RefreshAccessTokenAsync(
        string? refreshToken,
        string? clientId,
        string? scope,
        string? resource,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) ||
            refreshToken.Length > 200 ||
            string.IsNullOrWhiteSpace(clientId) || clientId.Length > 100 ||
            resource is null || resource.Length > 2000 || resource != options.ResourceUrl)
        {
            return null;
        }

        var requestedScope = string.IsNullOrWhiteSpace(scope)
            ? OAuthOptions.ReadScope
            : NormalizeScope(scope);
        if (requestedScope != OAuthOptions.ReadScope)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var tokenHash = HashValue(refreshToken);
        var storedToken = await dbContext.OAuthTokens.AsNoTracking().SingleOrDefaultAsync(
            item => item.TokenHash == tokenHash && item.TokenType == "refresh",
            cancellationToken);

        if (storedToken is null ||
            storedToken.ExpiresAt <= now ||
            storedToken.ClientId != clientId ||
            storedToken.Resource != resource ||
            storedToken.Scope != requestedScope)
        {
            return null;
        }

        var consumed = await dbContext.OAuthTokens
            .Where(item => item.Id == storedToken.Id && item.TokenHash == tokenHash)
            .ExecuteDeleteAsync(cancellationToken);
        if (consumed != 1)
        {
            return null;
        }

        await DeleteExpiredGrantsAsync(dbContext, now, cancellationToken);
        var tokenPair = AddTokenPair(dbContext, clientId, requestedScope, resource, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return tokenPair;
    }

    public async Task<bool> ValidateAccessTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (token.Length > 200)
        {
            return false;
        }

        var tokenHash = HashValue(token);
        var now = timeProvider.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.OAuthTokens.AsNoTracking().AnyAsync(
            item => item.TokenHash == tokenHash &&
                item.TokenType == "access" &&
                item.ExpiresAt > now &&
                item.Scope == OAuthOptions.ReadScope &&
                item.Resource == options.ResourceUrl,
            cancellationToken);
    }

    public async Task<string?> GetClientNameAsync(string clientId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.OAuthClients.AsNoTracking()
            .Where(item => item.ClientId == clientId)
            .Select(item => item.ClientName)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static string[] ValidateRegistration(OAuthClientRegistrationRequest request)
    {
        if (request.RedirectUris is not { Length: > 0 and <= 10 })
        {
            throw new ArgumentException("redirect_uris must contain between one and ten entries.");
        }

        var redirectUris = request.RedirectUris.Distinct(StringComparer.Ordinal).ToArray();
        if (redirectUris.Length != request.RedirectUris.Length || redirectUris.Any(uri => !IsAllowedRedirectUri(uri)))
        {
            throw new ArgumentException("Every redirect URI must be unique and use HTTPS or an HTTP loopback address.");
        }

        if (request.ClientName is { Length: > 200 })
        {
            throw new ArgumentException("client_name cannot exceed 200 characters.");
        }

        if (request.GrantTypes is not null &&
            (!request.GrantTypes.Contains("authorization_code", StringComparer.Ordinal) ||
             request.GrantTypes.Except(["authorization_code", "refresh_token"], StringComparer.Ordinal).Any()))
        {
            throw new ArgumentException("grant_types must include authorization_code; refresh_token is optional.");
        }

        if (request.ResponseTypes is not null &&
            (request.ResponseTypes.Length != 1 || request.ResponseTypes[0] != "code"))
        {
            throw new ArgumentException("Only the code response type is supported.");
        }

        if (request.TokenEndpointAuthMethod is not null && request.TokenEndpointAuthMethod != "none")
        {
            throw new ArgumentException("Only public clients using token_endpoint_auth_method=none are supported.");
        }

        return redirectUris;
    }

    private static bool IsAllowedRedirectUri(string value)
    {
        if (value.Length > 2000 ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            (uri.Host == "127.0.0.1" || uri.Host == "[::1]" || uri.Host == "localhost");
    }

    private static string[] GetRedirectUris(OAuthClient client) =>
        JsonSerializer.Deserialize<string[]>(client.RedirectUrisJson, JsonOptions) ?? [];

    private static string NormalizeScope(string? scope) =>
        string.Join(' ', (scope ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));

    private static bool IsValidCodeChallenge(string? value) =>
        value is { Length: >= 43 and <= 128 } && value.All(IsBase64UrlCharacter);

    private static bool IsValidCodeVerifier(string? value) =>
        value is { Length: >= 43 and <= 128 } && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~');

    private static bool IsBase64UrlCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_';

    private static bool VerifyCodeChallenge(string verifier, string expectedChallenge)
    {
        var calculatedChallenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var calculatedBytes = Encoding.ASCII.GetBytes(calculatedChallenge);
        var expectedBytes = Encoding.ASCII.GetBytes(expectedChallenge);
        return calculatedBytes.Length == expectedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(calculatedBytes, expectedBytes);
    }

    private static OAuthTokenPair AddTokenPair(
        AppDbContext dbContext,
        string clientId,
        string scope,
        string resource,
        DateTimeOffset now)
    {
        var accessToken = $"{AccessTokenPrefix}{CreateRandomValue(32)}";
        var refreshToken = $"ohrt_{CreateRandomValue(48)}";
        dbContext.OAuthTokens.AddRange(
            new OAuthToken
            {
                TokenHash = HashValue(accessToken),
                TokenType = "access",
                ClientId = clientId,
                Scope = scope,
                Resource = resource,
                CreatedAt = now,
                ExpiresAt = now.Add(AccessTokenLifetime)
            },
            new OAuthToken
            {
                TokenHash = HashValue(refreshToken),
                TokenType = "refresh",
                ClientId = clientId,
                Scope = scope,
                Resource = resource,
                CreatedAt = now,
                ExpiresAt = now.Add(RefreshTokenLifetime)
            });

        return new OAuthTokenPair(accessToken, refreshToken, (int)AccessTokenLifetime.TotalSeconds, scope);
    }

    private static async Task DeleteExpiredGrantsAsync(
        AppDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await dbContext.OAuthAuthorizationCodes
            .Where(item => item.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.OAuthTokens
            .Where(item => item.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string CreateRandomValue(int byteCount) => Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    private static string HashValue(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}