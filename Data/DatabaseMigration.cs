using Microsoft.EntityFrameworkCore;

namespace OpenHealthMCP.Data;

public static class DatabaseMigration
{
    private const int MaximumAttempts = 10;

    public static async Task ApplyMigrationsAsync(
        this WebApplication application,
        CancellationToken cancellationToken = default)
    {
        var logger = application.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseMigration");
        var dbContextFactory = application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                logger.LogInformation(
                    "Database migration started (attempt {Attempt} of {MaximumAttempts})",
                    attempt,
                    MaximumAttempts);
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migration completed");
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (attempt < MaximumAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 10));
                logger.LogWarning(
                    "Database migration attempt {Attempt} failed: {Error}. Retrying in {DelaySeconds} seconds",
                    attempt,
                    exception.GetBaseException().Message,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Database migration failed after {MaximumAttempts} attempts.");
    }
}