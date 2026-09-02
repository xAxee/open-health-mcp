using Microsoft.EntityFrameworkCore;
using OpenHealthMCP.Data.Entities;

namespace OpenHealthMCP.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DailyMetric> DailyMetrics => Set<DailyMetric>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<RawProviderData> RawProviderData => Set<RawProviderData>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();

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
            entity.Property(x => x.Source).HasMaxLength(50);
            entity.Property(x => x.ExternalId).HasMaxLength(200);
            entity.Property(x => x.Name).HasMaxLength(500);
            entity.Property(x => x.ActivityType).HasMaxLength(100);
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
    }
}