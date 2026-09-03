using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using OpenHealthMCP.Data;
using OpenHealthMCP.Providers.Garmin;

namespace OpenHealthMCP.Mcp;

[McpServerToolType]
public sealed class ProfileTools
{
    private const int MaximumRangeDays = 3660;

    [McpServerTool(Name = "get_user_profile", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns safe provider connection, confirmed fitness profile values, configured Garmin HR zones, capabilities, and last successful sync. It does not expose names, email, credentials, or medical interpretation.")]
    public static async Task<UserProfileResult> GetUserProfileAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        GarminOptions garminOptions,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await dbContext.UserFitnessProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Source == "garmin", cancellationToken);
        var sync = await dbContext.SyncStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Source == "garmin", cancellationToken);
        var latestOffset = await dbContext.DailyMetrics.AsNoTracking()
            .Where(item => item.Source == "garmin" && item.UtcOffsetMinutes.HasValue)
            .OrderByDescending(item => item.Date)
            .Select(item => item.UtcOffsetMinutes)
            .FirstOrDefaultAsync(cancellationToken);
        var zones = await dbContext.ConfiguredHeartRateZones.AsNoTracking()
            .Where(item => item.Source == "garmin")
            .OrderBy(item => item.Sport)
            .Select(item => new ConfiguredHeartRateZoneResult(
                item.Sport,
                item.TrainingMethod,
                item.RestingHeartRateUsed,
                item.LactateThresholdHeartRateUsed,
                item.MaxHeartRateUsed,
                new double?[] { item.Zone1Floor, item.Zone2Floor, item.Zone3Floor, item.Zone4Floor, item.Zone5Floor },
                "garmin_api"))
            .ToListAsync(cancellationToken);

        return new UserProfileResult(
            "garmin",
            garminOptions.IsConfigured,
            sync?.LastSuccessfulSyncAt.HasValue == true,
            profile?.ProviderProfileId,
            null,
            latestOffset,
            profile?.Vo2MaxRunning,
            profile?.Vo2MaxCycling,
            profile?.FitnessAge,
            profile?.AchievableFitnessAge,
            profile?.FitnessAgeUpdatedAt,
            sync?.LastSuccessfulSyncAt,
            zones,
            new ProfileCapabilities(
                true, true, true, false, true, true, true, false, true,
                true, true, true, true, true, true, true, true),
            "garmin_api");
    }

    [McpServerTool(Name = "get_body_composition", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns sparse Garmin body-composition measurements. Missing days are not interpolated.")]
    public static async Task<BodyCompositionResult> GetBodyCompositionAsync(
        [Description("Inclusive provider-local start date in YYYY-MM-DD format.")] string from,
        [Description("Inclusive provider-local end date in YYYY-MM-DD format.")] string to,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken)
    {
        var (fromDate, toDate) = ParseRange(from, to);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var values = await dbContext.BodyCompositionMeasurements.AsNoTracking()
            .Where(item => item.Source == "garmin" && item.LocalDate >= fromDate && item.LocalDate <= toDate)
            .OrderBy(item => item.TimestampUtc)
            .Select(item => new BodyCompositionPoint(
                item.ExternalId, item.LocalDate, item.TimestampUtc, item.WeightKilograms, item.Bmi,
                item.BodyFatPercent, item.MuscleMassKilograms, item.BoneMassKilograms,
                item.BodyWaterPercent, item.VisceralFat, item.MetabolicAge, item.SourceType,
                new MetricSourceMetadata("derived_by_openhealth", "garmin-grams-to-kilograms-v1")))
            .ToListAsync(cancellationToken);
        return new BodyCompositionResult("garmin", fromDate, toDate, values.Count, values);
    }

    [McpServerTool(Name = "get_blood_pressure", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns sparse Garmin blood-pressure measurements without aggregation or medical interpretation. Missing days are valid empty data.")]
    public static async Task<BloodPressureResult> GetBloodPressureAsync(
        [Description("Inclusive provider-local start date in YYYY-MM-DD format.")] string from,
        [Description("Inclusive provider-local end date in YYYY-MM-DD format.")] string to,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CancellationToken cancellationToken)
    {
        var (fromDate, toDate) = ParseRange(from, to);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var values = await dbContext.BloodPressureMeasurements.AsNoTracking()
            .Where(item => item.Source == "garmin" && item.LocalDate >= fromDate && item.LocalDate <= toDate)
            .OrderBy(item => item.TimestampUtc)
            .Select(item => new BloodPressurePoint(
                item.ExternalId, item.LocalDate, item.TimestampUtc, item.TimestampLocal,
                item.Systolic, item.Diastolic, item.Pulse, item.ProviderSourceType, item.SourceType))
            .ToListAsync(cancellationToken);
        return new BloodPressureResult("garmin", fromDate, toDate, values.Count, values);
    }

    private static (DateOnly From, DateOnly To) ParseRange(string from, string to)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var fromDate) ||
            !DateOnly.TryParseExact(to, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var toDate))
        {
            throw new ArgumentException("from and to must use YYYY-MM-DD format.");
        }
        if (fromDate > toDate) throw new ArgumentException("from must not be after to.");
        if (toDate.DayNumber - fromDate.DayNumber + 1 > MaximumRangeDays)
            throw new ArgumentException($"Date range cannot exceed {MaximumRangeDays} days.");
        return (fromDate, toDate);
    }
}