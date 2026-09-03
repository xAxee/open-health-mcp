using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenHealthMCP.Data;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DesignTimeConnection =
        "Host=localhost;Database=openhealthmcp_design;Username=design;Password=design";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(string.IsNullOrWhiteSpace(connectionString) ? DesignTimeConnection : connectionString)
            .Options;
        return new AppDbContext(options);
    }
}