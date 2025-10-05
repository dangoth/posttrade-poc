using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Infrastructure.Entities;
using System.Reflection;

namespace PostTradeSystem.Infrastructure.Data;

public class PostTradeDbContext : DbContext
{
    public PostTradeDbContext(DbContextOptions<PostTradeDbContext> options) : base(options)
    {
    }

    public DbSet<EventStoreEntity> EventStore { get; set; } = null!;
    public DbSet<SnapshotEntity> Snapshots { get; set; } = null!;
    public DbSet<ProjectionEntity> Projections { get; set; } = null!;
    public DbSet<IdempotencyEntity> IdempotencyKeys { get; set; } = null!;
    public DbSet<OutboxEventEntity> OutboxEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.EnableSensitiveDataLogging(false);
            optionsBuilder.EnableDetailedErrors(true);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified && e.Entity is ProjectionEntity);

        foreach (var entry in entries)
        {
            if (entry.Entity is ProjectionEntity projection)
            {
                projection.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}