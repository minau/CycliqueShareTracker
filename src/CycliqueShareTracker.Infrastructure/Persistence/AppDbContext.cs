using CycliqueShareTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CycliqueShareTracker.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<DailyPrice> DailyPrices => Set<DailyPrice>();
    public DbSet<DailyIndicator> DailyIndicators => Set<DailyIndicator>();
    public DbSet<DailySignal> DailySignals => Set<DailySignal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasIndex(x => x.Symbol).IsUnique();
            entity.Property(x => x.Symbol).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Market).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<DailyPrice>(entity =>
        {
            entity.HasIndex(x => new { x.AssetId, x.Date }).IsUnique();
            entity.Property(x => x.Open).HasColumnType("numeric(18,4)");
            entity.Property(x => x.High).HasColumnType("numeric(18,4)");
            entity.Property(x => x.Low).HasColumnType("numeric(18,4)");
            entity.Property(x => x.Close).HasColumnType("numeric(18,4)");
        });

        modelBuilder.Entity<DailyIndicator>(entity =>
        {
            entity.HasIndex(x => new { x.AssetId, x.Date }).IsUnique();
            entity.Property(x => x.Sma50).HasColumnType("numeric(18,4)");
            entity.Property(x => x.Sma200).HasColumnType("numeric(18,4)");
            entity.Property(x => x.Rsi14).HasColumnType("numeric(18,4)");
            entity.Property(x => x.Drawdown52WeeksPercent).HasColumnType("numeric(18,4)");
        });

        modelBuilder.Entity<DailySignal>(entity =>
        {
            entity.HasIndex(x => new { x.AssetId, x.Date }).IsUnique();
            entity.Property(x => x.Explanation).HasMaxLength(512);
            entity.Property(x => x.ExitPrimaryReason).HasMaxLength(512);
        });
    }
}
