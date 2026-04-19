using Microsoft.EntityFrameworkCore;

namespace OctopusDashboard.Data;

public class OctopusDbContext(DbContextOptions<OctopusDbContext> options) : DbContext(options)
{
    public DbSet<EnergyReading> Readings => Set<EnergyReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EnergyReading>(e =>
        {
            e.HasIndex(r => new { r.EnergyType, r.IntervalStart }).IsUnique();
            e.Property(r => r.IntervalStart).HasConversion<long>(
                dto => dto.ToUniversalTime().Ticks,
                ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
            e.Property(r => r.IntervalEnd).HasConversion<long>(
                dto => dto.ToUniversalTime().Ticks,
                ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
        });
    }
}
