namespace OpenHealthMCP.Providers.Garmin;

public sealed class GarminOptions
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? MfaCode { get; init; }
    public string SessionPath { get; init; } = "garmin-session/token.json";
    public int ActivityEnrichmentLimit { get; init; } = 10;
    public int ActivityEnrichmentDelayMilliseconds { get; init; } = 250;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);

    public static GarminOptions FromConfiguration(IConfiguration configuration)
    {
        var enrichmentLimit = ParseInt(configuration["GARMIN_ACTIVITY_ENRICHMENT_LIMIT"], 10);
        var enrichmentDelay = ParseInt(configuration["GARMIN_ACTIVITY_ENRICHMENT_DELAY_MS"], 250);
        if (enrichmentLimit is < 0 or > 100)
        {
            throw new InvalidOperationException("GARMIN_ACTIVITY_ENRICHMENT_LIMIT must be between 0 and 100.");
        }

        if (enrichmentDelay is < 0 or > 10000)
        {
            throw new InvalidOperationException("GARMIN_ACTIVITY_ENRICHMENT_DELAY_MS must be between 0 and 10000.");
        }

        return new GarminOptions
        {
            Email = configuration["GARMIN_EMAIL"] ?? string.Empty,
            Password = configuration["GARMIN_PASSWORD"] ?? string.Empty,
            MfaCode = configuration["GARMIN_MFA_CODE"],
            SessionPath = configuration["GARMIN_SESSION_PATH"] ?? "garmin-session/token.json",
            ActivityEnrichmentLimit = enrichmentLimit,
            ActivityEnrichmentDelayMilliseconds = enrichmentDelay
        };
    }

    private static int ParseInt(string? value, int defaultValue) =>
        string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var result)
                ? result
                : throw new InvalidOperationException("Garmin enrichment settings must be integers.");
}