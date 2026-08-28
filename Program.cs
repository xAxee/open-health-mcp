using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data;
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

builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(postgresConnection));
builder.Services.AddSingleton(garminOptions);
builder.Services.AddSingleton(syncOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<GarminRawPayloadCollector>();
builder.Services.AddSingleton<GarminClientSession>();
builder.Services.AddSingleton<IHealthDataProvider, GarminProvider>();
builder.Services.AddSingleton<HealthSyncService>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<HealthSyncService>());

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
