using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data;

var builder = WebApplication.CreateBuilder(args);

var postgresConnection = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgresConnection))
{
    throw new InvalidOperationException("ConnectionStrings:Postgres must be configured.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgresConnection));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
