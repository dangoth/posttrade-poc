using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PostTradeSystem.Infrastructure.Entities;

namespace PostTradeSystem.Infrastructure.Configuration;

public class EventStoreEntityConfiguration : IEntityTypeConfiguration<EventStoreEntity>
{
    public void Configure(EntityTypeBuilder<EventStoreEntity> builder)
    {
        builder.ToTable("EventStore");
        
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();
            
        builder.Property(e => e.EventId)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(e => e.AggregateId)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(e => e.AggregateType)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(e => e.PartitionKey)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(e => e.AggregateVersion)
            .IsRequired();
            
        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(e => e.EventData)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
            
        builder.Property(e => e.Metadata)
            .HasColumnType("nvarchar(max)");
            
        builder.Property(e => e.OccurredAt)
            .IsRequired()
            .HasColumnType("datetime2");
            
        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasDefaultValueSql("GETUTCDATE()");
            
        builder.Property(e => e.CorrelationId)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(e => e.CausedBy)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(e => e.IsProcessed)
            .IsRequired()
            .HasDefaultValue(false);
            
        builder.Property(e => e.ProcessedAt)
            .HasColumnType("datetime2");

        builder.HasIndex(e => e.EventId)
            .IsUnique()
            .HasDatabaseName("IX_EventStore_EventId");
            
        builder.HasIndex(e => new { e.AggregateId, e.AggregateVersion })
            .IsUnique()
            .HasDatabaseName("IX_EventStore_AggregateId_Version");
            
        builder.HasIndex(e => e.PartitionKey)
            .HasDatabaseName("IX_EventStore_PartitionKey");
            
        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_EventStore_CorrelationId");
            
        builder.HasIndex(e => new { e.IsProcessed, e.CreatedAt })
            .HasDatabaseName("IX_EventStore_IsProcessed_CreatedAt");
    }
}