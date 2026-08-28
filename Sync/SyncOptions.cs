namespace OpenHealthMCP.Sync;

public sealed class SyncOptions
{
    public double IntervalHours { get; init; } = 3;
    public int LookbackDays { get; init; } = 3;
    public int HistoricalChunkDays { get; init; } = 31;

    public static SyncOptions FromConfiguration(IConfiguration configuration)
    {
        var interval = ParseDouble(configuration["SYNC_INTERVAL_HOURS"], 3);
        var lookback = ParseInt(configuration["SYNC_LOOKBACK_DAYS"], 3);

        if (interval is <= 0 or > 168)
        {
            throw new InvalidOperationException("SYNC_INTERVAL_HOURS must be greater than 0 and no more than 168.");
        }

        if (lookback is <= 0 or > 31)
        {
            throw new InvalidOperationException("SYNC_LOOKBACK_DAYS must be between 1 and 31.");
        }

        return new SyncOptions
        {
            IntervalHours = interval,
            LookbackDays = lookback
        };
    }

    private static double ParseDouble(string? value, double defaultValue) =>
        string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : double.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var result)
                ? result
                : throw new InvalidOperationException("SYNC_INTERVAL_HOURS must be a number.");

    private static int ParseInt(string? value, int defaultValue) =>
        string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var result)
                ? result
                : throw new InvalidOperationException("SYNC_LOOKBACK_DAYS must be an integer.");
}