namespace OpenHealthMCP.Providers;

public interface IHealthDataProvider
{
    string Name { get; }

    Task SyncAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}