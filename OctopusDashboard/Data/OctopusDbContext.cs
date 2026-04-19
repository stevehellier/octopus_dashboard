using Microsoft.EntityFrameworkCore;

namespace OctopusDashboard.Data;

public class OctopusDbContext(DbContextOptions<OctopusDbContext> options) : DbContext(options)
{
    public DbSet<CachedInterval> CachedIntervals => Set<CachedInterval>();
    public DbSet<CachedDay> CachedDays => Set<CachedDay>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CachedInterval>()
            .HasIndex(x => new { x.EnergyType, x.IntervalStartUtc });

        modelBuilder.Entity<CachedDay>()
            .HasIndex(x => new { x.EnergyType, x.Date })
            .IsUnique();
    }
}
