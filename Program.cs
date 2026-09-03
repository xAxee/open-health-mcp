using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using ModelContextProtocol.AspNetCore;
using OpenHealthMCP.Admin;
using OpenHealthMCP.Authentication;
using OpenHealthMCP.Data;
using OpenHealthMCP.Mcp;
using OpenHealthMCP.OAuth;
using OpenHealthMCP.Providers;
using OpenHealthMCP.Providers.Garmin;
using OpenHealthMCP.Sync;

var builder = WebApplication.CreateBuilder(args);

var postgresConnection = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgresConnection))
{
    throw new InvalidOperationException("ConnectionStrings:Postgres must be configured.");
}

var garminOptions = GarminOptions.FromConfiguration(builder.Configuration);
var syncOptions = SyncOptions.FromConfiguration(builder.Configuration);
var tokenOptions = McpTokenOptions.FromConfiguration(builder.Configuration);
var oauthOptions = OAuthOptions.FromConfiguration(builder.Configuration);

builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(postgresConnection));
builder.Services.AddSingleton(garminOptions);
builder.Services.AddSingleton(syncOptions);
builder.Services.AddSingleton(tokenOptions);
builder.Services.AddSingleton(oauthOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<GarminRawPayloadCollector>();
builder.Services.AddSingleton<GarminClientSession>();
builder.Services.AddSingleton<IHealthDataProvider, GarminProvider>();
builder.Services.AddSingleton<HealthSyncService>();
builder.Services.AddSingleton<OAuthService>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<HealthSyncService>());
builder.Services
    .AddAuthentication(McpTokenOptions.Scheme)
    .AddScheme<AuthenticationSchemeOptions, McpTokenAuthenticationHandler>(McpTokenOptions.Scheme, null);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("McpAccess", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("scope", OAuthOptions.ReadScope))
    .AddPolicy("AdminAccess", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(
            McpTokenAuthenticationHandler.AuthenticationMethodClaim,
            McpTokenAuthenticationHandler.StaticAuthenticationMethod));
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("OAuthRegistration", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromHours(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("OAuthAuthorization", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("OAuthToken", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<HealthTools>()
    .WithTools<SeriesTools>();

var app = builder.Build();

await app.ApplyMigrationsAsync();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapOAuthEndpoints(oauthOptions);

app.MapPost("/admin/sync", async (
    AdminSyncRequest request,
    HealthSyncService syncService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await syncService.SyncRangeAsync(request.From, request.To, cancellationToken);
        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Problem(
            title: "Synchronization failed",
            detail: exception.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
}).RequireAuthorization("AdminAccess");

app.MapMcp("/mcp").RequireAuthorization("McpAccess");

app.Run();
