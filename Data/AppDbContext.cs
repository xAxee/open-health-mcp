using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data.Entities;

namespace OpenHealthMCP.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DailyMetric> DailyMetrics => Set<DailyMetric>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityLap> ActivityLaps => Set<ActivityLap>();
    public DbSet<ActivityHeartRateZone> ActivityHeartRateZones => Set<ActivityHeartRateZone>();
    public DbSet<ActivityStream> ActivityStreams => Set<ActivityStream>();
    public DbSet<DailyTimeline> DailyTimelines => Set<DailyTimeline>();
    public DbSet<RawProviderData> RawProviderData => Set<RawProviderData>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();
    public DbSet<OAuthAuthorizationCode> OAuthAuthorizationCodes => Set<OAuthAuthorizationCode>();
    public DbSet<OAuthToken> OAuthTokens => Set<OAuthToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyMetric>(entity =>
        {
            entity.ToTable("daily_metrics");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Source, x.Date }).IsUnique();
            entity.Property(x => x.Source).HasMaxLength(50);
        });

        modelBuilder.Entity<Activity>(entity =>
        {
            entity.ToTable("activities");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique();
            entity.HasIndex(x => x.StartedAt);
            entity.HasIndex(x => new { x.Source, x.ActivityType, x.StartedAt });
            entity.Property(x => x.Source).HasMaxLength(50);
            entity.Property(x => x.ExternalId).HasMaxLength(200);
            entity.Property(x => x.Name).HasMaxLength(500);
            entity.Property(x => x.ActivityType).HasMaxLength(100);
            entity.Property(x => x.CadenceUnit).HasMaxLength(30);
            entity.HasMany(x => x.Laps)
                .WithOne(x => x.Activity)
                .HasForeignKey(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.HeartRateZones)
                .WithOne(x => x.Activity)
                .HasForeignKey(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Stream)
                .WithOne(x => x.Activity)
                .HasForeignKey<ActivityStream>(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivityLap>(entity =>
        {
            entity.ToTable("activity_laps");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ActivityId, x.LapIndex }).IsUnique();
            entity.Property(x => x.CadenceUnit).HasMaxLength(30);
            entity.Property(x => x.IntensityType).HasMaxLength(50);
        });

        modelBuilder.Entity<ActivityHeartRateZone>(entity =>
        {
            entity.ToTable("activity_heart_rate_zones");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ActivityId, x.ZoneNumber }).IsUnique();
        });

        modelBuilder.Entity<ActivityStream>(entity =>
        {
            entity.ToTable("activity_streams");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ActivityId).IsUnique();
            entity.Property(x => x.AvailableMetrics).HasColumnType("text[]");
            entity.Property(x => x.Samples).HasColumnType("jsonb");
        });

        modelBuilder.Entity<DailyTimeline>(entity =>
        {
            entity.ToTable("daily_timelines");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Source, x.Date, x.Metric }).IsUnique();
            entity.Property(x => x.Source).HasMaxLength(50);
            entity.Property(x => x.Metric).HasMaxLength(50);
            entity.Property(x => x.Samples).HasColumnType("jsonb");
        });

        modelBuilder.Entity<RawProviderData>(entity =>
        {
            entity.ToTable("raw_provider_data");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Source, x.DataType, x.ExternalId }).IsUnique();
            entity.Property(x => x.Source).HasMaxLength(50);
            entity.Property(x => x.DataType).HasMaxLength(50);
            entity.Property(x => x.ExternalId).HasMaxLength(200);
            entity.Property(x => x.Payload).HasColumnType("jsonb");
        });

        modelBuilder.Entity<SyncState>(entity =>
        {
            entity.ToTable("sync_states");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Source).IsUnique();
            entity.Property(x => x.Source).HasMaxLength(50);
            entity.Property(x => x.LastError).HasMaxLength(2000);
        });

        modelBuilder.Entity<OAuthClient>(entity =>
        {
            entity.ToTable("oauth_clients");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ClientId).IsUnique();
            entity.Property(x => x.ClientId).HasMaxLength(100);
            entity.Property(x => x.ClientName).HasMaxLength(200);
            entity.Property(x => x.RedirectUrisJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<OAuthAuthorizationCode>(entity =>
        {
            entity.ToTable("oauth_authorization_codes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CodeHash).IsUnique();
            entity.Property(x => x.CodeHash).HasMaxLength(64);
            entity.Property(x => x.ClientId).HasMaxLength(100);
            entity.Property(x => x.RedirectUri).HasMaxLength(2000);
            entity.Property(x => x.CodeChallenge).HasMaxLength(128);
            entity.Property(x => x.Scope).HasMaxLength(500);
            entity.Property(x => x.Resource).HasMaxLength(2000);
        });

        modelBuilder.Entity<OAuthToken>(entity =>
        {
            entity.ToTable("oauth_tokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.Property(x => x.TokenType).HasMaxLength(20);
            entity.Property(x => x.ClientId).HasMaxLength(100);
            entity.Property(x => x.Scope).HasMaxLength(500);
            entity.Property(x => x.Resource).HasMaxLength(2000);
        });
    }
}