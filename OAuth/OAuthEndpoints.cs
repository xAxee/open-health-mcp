using System.Net;
using Microsoft.AspNetCore.Http.HttpResults;

namespace OpenHealthMCP.OAuth;

public static class OAuthEndpoints
{
    public static void MapOAuthEndpoints(this WebApplication app, OAuthOptions options)
    {
        var protectedResourceMetadata = new
        {
            resource = options.ResourceUrl,
            authorization_servers = new[] { options.BaseUrl },
            scopes_supported = new[] { OAuthOptions.ReadScope },
            bearer_methods_supported = new[] { "header" }
        };

        app.MapGet("/.well-known/oauth-protected-resource", () => Results.Json(protectedResourceMetadata));
        app.MapGet("/.well-known/oauth-protected-resource/mcp", () => Results.Json(protectedResourceMetadata));

        app.MapGet("/.well-known/oauth-authorization-server", () => Results.Json(new
        {
            issuer = options.BaseUrl,
            authorization_endpoint = $"{options.BaseUrl}/oauth/authorize",
            token_endpoint = $"{options.BaseUrl}/oauth/token",
            registration_endpoint = $"{options.BaseUrl}/oauth/register",
            scopes_supported = new[] { OAuthOptions.ReadScope },
            response_types_supported = new[] { "code" },
            response_modes_supported = new[] { "query" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            token_endpoint_auth_methods_supported = new[] { "none" },
            code_challenge_methods_supported = new[] { "S256" },
            authorization_response_iss_parameter_supported = true
        }));

        app.MapPost("/oauth/register", RegisterClientAsync).RequireRateLimiting("OAuthRegistration");
        app.MapGet("/oauth/authorize", ShowAuthorizationPageAsync).RequireRateLimiting("OAuthAuthorization");
        app.MapPost("/oauth/authorize", ApproveAuthorizationAsync)
            .DisableAntiforgery()
            .RequireRateLimiting("OAuthAuthorization");
        app.MapPost("/oauth/token", IssueTokenAsync)
            .DisableAntiforgery()
            .RequireRateLimiting("OAuthToken");
    }

    private static async Task<IResult> RegisterClientAsync(
        OAuthClientRegistrationRequest request,
        OAuthService oauthService,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await oauthService.RegisterClientAsync(request, cancellationToken);
            return Results.Json(response, statusCode: StatusCodes.Status201Created);
        }
        catch (ArgumentException exception)
        {
            return OAuthError("invalid_client_metadata", exception.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> ShowAuthorizationPageAsync(
        HttpRequest request,
        OAuthService oauthService,
        CancellationToken cancellationToken)
    {
        var authorizationRequest = await oauthService.ValidateAuthorizationRequestAsync(request.Query, cancellationToken);
        if (authorizationRequest is null)
        {
            return OAuthError("invalid_request", "The authorization request is invalid.", StatusCodes.Status400BadRequest);
        }

        var clientName = await oauthService.GetClientNameAsync(authorizationRequest.ClientId, cancellationToken) ?? "MCP client";
        SetAuthorizationPageHeaders(request.HttpContext.Response);
        return Results.Content(RenderConsentPage(authorizationRequest, clientName), "text/html; charset=utf-8");
    }

    private static async Task<IResult> ApproveAuthorizationAsync(
        HttpRequest request,
        OAuthService oauthService,
        OAuthOptions options,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return OAuthError("invalid_request", "Form data is required.", StatusCodes.Status400BadRequest);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var query = new QueryCollection(form.ToDictionary(pair => pair.Key, pair => pair.Value));
        var authorizationRequest = await oauthService.ValidateAuthorizationRequestAsync(query, cancellationToken);
        if (authorizationRequest is null)
        {
            return OAuthError("invalid_request", "The authorization request is invalid.", StatusCodes.Status400BadRequest);
        }

        if (form["decision"] != "approve")
        {
            return RedirectWithParameters(authorizationRequest.RedirectUri, new Dictionary<string, string?>
            {
                ["error"] = "access_denied",
                ["state"] = authorizationRequest.State,
                ["iss"] = options.BaseUrl
            });
        }

        if (!oauthService.ValidateOwnerPassword(form["owner_password"].ToString()))
        {
            SetAuthorizationPageHeaders(request.HttpContext.Response);
            return Results.Content(
                RenderConsentPage(authorizationRequest, "MCP client", "The owner password is incorrect."),
                "text/html; charset=utf-8",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var code = await oauthService.CreateAuthorizationCodeAsync(authorizationRequest, cancellationToken);
        return RedirectWithParameters(authorizationRequest.RedirectUri, new Dictionary<string, string?>
        {
            ["code"] = code,
            ["state"] = authorizationRequest.State,
            ["iss"] = options.BaseUrl
        });
    }

    private static async Task<IResult> IssueTokenAsync(
        HttpRequest request,
        HttpResponse response,
        OAuthService oauthService,
        CancellationToken cancellationToken)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
        if (!request.HasFormContentType)
        {
            return OAuthError("invalid_request", "Form-encoded data is required.", StatusCodes.Status400BadRequest);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        OAuthTokenPair? tokenPair;
        switch (form["grant_type"].ToString())
        {
            case "authorization_code":
                tokenPair = await oauthService.ExchangeAuthorizationCodeAsync(
                    form["code"],
                    form["client_id"],
                    form["redirect_uri"],
                    form["code_verifier"],
                    form["resource"],
                    cancellationToken);
                break;
            case "refresh_token":
                tokenPair = await oauthService.RefreshAccessTokenAsync(
                    form["refresh_token"],
                    form["client_id"],
                    form["scope"],
                    form["resource"],
                    cancellationToken);
                break;
            default:
                return OAuthError("unsupported_grant_type", "The grant type is not supported.", StatusCodes.Status400BadRequest);
        }

        return tokenPair is null
            ? OAuthError("invalid_grant", "The authorization grant is invalid or expired.", StatusCodes.Status400BadRequest)
            : Results.Json(new OAuthTokenResponse(
                tokenPair.AccessToken,
                "Bearer",
                tokenPair.ExpiresIn,
                tokenPair.RefreshToken,
                tokenPair.Scope));
    }

    private static IResult OAuthError(string error, string description, int statusCode) =>
        Results.Json(new OAuthErrorResponse(error, description), statusCode: statusCode);

    private static void SetAuthorizationPageHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
        response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";
    }

    private static IResult RedirectWithParameters(string redirectUri, IReadOnlyDictionary<string, string?> parameters)
    {
        var separator = redirectUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var query = string.Join('&', parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? string.Empty)}"));
        return new SeeOtherResult($"{redirectUri}{separator}{query}");
    }

    private sealed class SeeOtherResult(string location) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status303SeeOther;
            httpContext.Response.Headers.Location = location;
            return Task.CompletedTask;
        }
    }

    private static string RenderConsentPage(
        OAuthAuthorizationRequest request,
        string clientName,
        string? error = null)
    {
        static string Encode(string value) => WebUtility.HtmlEncode(value);
        static string Hidden(string name, string value) =>
            $"<input type=\"hidden\" name=\"{Encode(name)}\" value=\"{Encode(value)}\">";

        var errorHtml = error is null ? string.Empty : $"<p class=\"error\">{Encode(error)}</p>";
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Authorize OpenHealthMCP</title>
              <style>
                body{font-family:system-ui,sans-serif;background:#f4f7f6;color:#17201d;margin:0;padding:2rem}
                main{max-width:32rem;margin:5vh auto;background:white;padding:2rem;border-radius:1rem;box-shadow:0 1rem 3rem #173b2b1f}
                h1{margin-top:0}.scope{padding:1rem;background:#eef8f2;border-radius:.6rem}.error{color:#a40000}
                label{display:block;font-weight:600;margin:1.25rem 0 .4rem}input[type=password]{box-sizing:border-box;width:100%;padding:.8rem}
                .actions{display:flex;gap:.75rem;margin-top:1.25rem}button{padding:.8rem 1.1rem;border:0;border-radius:.5rem;cursor:pointer}
                .approve{background:#126b45;color:white}.deny{background:#e7e7e7;color:#222}
              </style>
            </head>
            <body><main>
              <h1>Authorize OpenHealthMCP</h1>
              <p><strong>{{Encode(clientName)}}</strong> is requesting access to your private health data.</p>
              <p class="scope">Permission: read normalized health metrics and activities.</p>
              <p>After approval, the authorization code will be sent to:<br><code>{{Encode(request.RedirectUri)}}</code></p>
              {{errorHtml}}
              <form method="post" action="/oauth/authorize" autocomplete="off">
                {{Hidden("response_type", "code")}}
                {{Hidden("client_id", request.ClientId)}}
                {{Hidden("redirect_uri", request.RedirectUri)}}
                {{Hidden("state", request.State)}}
                {{Hidden("code_challenge", request.CodeChallenge)}}
                {{Hidden("code_challenge_method", "S256")}}
                {{Hidden("scope", request.Scope)}}
                {{Hidden("resource", request.Resource)}}
                <label for="owner_password">Owner password</label>
                <input id="owner_password" name="owner_password" type="password" required autofocus>
                <div class="actions">
                  <button class="approve" type="submit" name="decision" value="approve">Authorize</button>
                  <button class="deny" type="submit" name="decision" value="deny">Deny</button>
                </div>
              </form>
            </main></body>
            </html>
            """;
    }
}