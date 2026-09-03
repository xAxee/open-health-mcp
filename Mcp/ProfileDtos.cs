namespace OpenHealthMCP.Mcp;

public sealed record UserProfileResult(
    string Source,
    bool ProviderConfigured,
    bool Connected,
    string? ProviderProfileId,
    string? Timezone,
    int? LatestUtcOffsetMinutes,
    double? Vo2MaxRunning,
    double? Vo2MaxCycling,
    double? FitnessAge,
    double? AchievableFitnessAge,
    DateTimeOffset? FitnessAgeUpdatedAt,
    DateTimeOffset? LastSuccessfulSync,
    IReadOnlyList<ConfiguredHeartRateZoneResult> HeartRateZones,
    ProfileCapabilities Capabilities,
    string SourceType);

public sealed record ConfiguredHeartRateZoneResult(
    string Sport,
    string? TrainingMethod,
    double? RestingHeartRateUsed,
    double? LactateThresholdHeartRateUsed,
    double? MaxHeartRateUsed,
    IReadOnlyList<double?> ZoneFloorsBpm,
    string SourceType);

public sealed record ProfileCapabilities(
    bool Daily,
    bool Sleep,
    bool HrvSummary,
    bool HrvSeries,
    bool StressSeries,
    bool BodyBatterySeries,
    bool Spo2Summary,
    bool Spo2Series,
    bool RespirationSeries,
    bool Activities,
    bool ActivityDetails,
    bool Laps,
    bool HeartRateZones,
    bool ActivitySeries,
    bool BodyComposition,
    bool BloodPressure,
    bool FitDownload);

public sealed record BodyCompositionResult(
    string Source,
    DateOnly From,
    DateOnly To,
    int Count,
    IReadOnlyList<BodyCompositionPoint> Measurements);

public sealed record BodyCompositionPoint(
    string MeasurementId,
    DateOnly LocalDate,
    DateTimeOffset TimestampUtc,
    double? WeightKilograms,
    double? Bmi,
    double? BodyFatPercent,
    double? MuscleMassKilograms,
    double? BoneMassKilograms,
    double? BodyWaterPercent,
    double? VisceralFat,
    double? MetabolicAge,
    string SourceType,
    MetricSourceMetadata MassUnitConversion);

public sealed record BloodPressureResult(
    string Source,
    DateOnly From,
    DateOnly To,
    int Count,
    IReadOnlyList<BloodPressurePoint> Measurements);

public sealed record BloodPressurePoint(
    string MeasurementId,
    DateOnly LocalDate,
    DateTimeOffset TimestampUtc,
    DateTime? TimestampLocal,
    int Systolic,
    int Diastolic,
    int? Pulse,
    string? ProviderSourceType,
    string SourceType);