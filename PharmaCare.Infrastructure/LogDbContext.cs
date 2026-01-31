using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Entities.Logging;

namespace PharmaCare.Infrastructure;

/// <summary>
/// Database context for the logging database - completely separate from main application database
/// </summary>
public class LogDbContext : DbContext
{
    public LogDbContext(DbContextOptions<LogDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Activity logs tracking all user actions
    /// </summary>
    public DbSet<ActivityLog> ActivityLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("ActivityLogs");
            entity.HasKey(e => e.ActivityLogID);

            // Index for querying by user
            entity.HasIndex(e => e.UserId);

            // Index for querying by entity
            entity.HasIndex(e => new { e.EntityName, e.EntityId });

            // Index for querying by timestamp
            entity.HasIndex(e => e.Timestamp);

            // Index for querying by activity type
            entity.HasIndex(e => e.ActivityType);

            // Configure string lengths
            entity.Property(e => e.UserName).HasMaxLength(256);
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.Property(e => e.EntityId).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(500);

            // Configure JSON columns for old/new values (stored as NVARCHAR(MAX))
            entity.Property(e => e.OldValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.NewValues).HasColumnType("nvarchar(max)");
        });
    }
}
