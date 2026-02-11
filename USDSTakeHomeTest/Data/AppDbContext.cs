using Microsoft.EntityFrameworkCore;
using USDSTakeHomeTest.Models;

namespace USDSTakeHomeTest.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
    public DbSet<AgencyMetric> AgencyMetrics => Set<AgencyMetric>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Agency>(entity =>
        {
            entity.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(a => a.NormalizedName)
                .IsRequired()
                .HasMaxLength(512);

            entity.HasIndex(a => a.NormalizedName)
                .IsUnique();
        });

        modelBuilder.Entity<Snapshot>(entity =>
        {
            entity.Property(s => s.Type)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(s => s.Source)
                .HasMaxLength(2000);

            entity.Property(s => s.IngestedAt)
                .IsRequired();
        });

        modelBuilder.Entity<AgencyMetric>(entity =>
        {
            entity.Property(m => m.Sha256Checksum)
                .IsRequired()
                .HasMaxLength(64);

            // Required relationship: AgencyMetric -> Agency
            entity.HasOne(m => m.Agency)
                .WithMany()
                .HasForeignKey(m => m.AgencyId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            // Required relationship: AgencyMetric -> Snapshot
            entity.HasOne(m => m.Snapshot)
                .WithMany()
                .HasForeignKey(m => m.SnapshotId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.HasIndex(m => new { m.AgencyId, m.SnapshotId }).IsUnique();
        });
    }
}
