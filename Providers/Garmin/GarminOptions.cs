namespace OpenHealthMCP.Providers.Garmin;

public sealed class GarminOptions
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? MfaCode { get; init; }
    public string SessionPath { get; init; } = "garmin-session/token.json";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);

    public static GarminOptions FromConfiguration(IConfiguration configuration) => new()
    {
        Email = configuration["GARMIN_EMAIL"] ?? string.Empty,
        Password = configuration["GARMIN_PASSWORD"] ?? string.Empty,
        MfaCode = configuration["GARMIN_MFA_CODE"],
        SessionPath = configuration["GARMIN_SESSION_PATH"] ?? "garmin-session/token.json"
    };
}