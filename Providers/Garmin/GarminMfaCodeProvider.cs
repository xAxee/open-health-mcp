using Garmin.Connect.Auth;

namespace OpenHealthMCP.Providers.Garmin;

internal sealed class GarminMfaCodeProvider(GarminOptions options) : IMfaCodeProvider
{
    public Task<string> GetMfaCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(options.MfaCode))
        {
            throw new InvalidOperationException(
                "Garmin requested MFA. Set GARMIN_MFA_CODE to the current code and restart the application.");
        }

        return Task.FromResult(options.MfaCode.Trim());
    }
}