using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PostTradeSystem.Infrastructure.Entities;

namespace PostTradeSystem.Infrastructure.Configuration;

public class OutboxEventEntityConfiguration : IEntityTypeConfiguration<OutboxEventEntity>
{
    public void Configure(EntityTypeBuilder<OutboxEventEntity> builder)
    {
        builder.ToTable("OutboxEvents");
        
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();
            
        builder.Property(e => e.EventId)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(e => e.AggregateId)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(e => e.AggregateType)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(e => e.EventData)
            .IsRequired();
            
        builder.Property(e => e.Metadata)
            .IsRequired();
            
        builder.Property(e => e.Topic)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(e => e.PartitionKey)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(e => e.CreatedAt)
            .IsRequired();
            
        builder.Property(e => e.ProcessedAt);
            
        builder.Property(e => e.IsProcessed)
            .IsRequired()
            .HasDefaultValue(false);
            
        builder.Property(e => e.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);
            
        builder.Property(e => e.LastRetryAt);
            
        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(1000);
            
        builder.Property(e => e.IsDeadLettered)
            .IsRequired()
            .HasDefaultValue(false);
            
        builder.Property(e => e.DeadLetteredAt);
            
        builder.Property(e => e.DeadLetterReason)
            .HasMaxLength(1000);
        
        builder.HasIndex(e => e.EventId)
            .IsUnique();
            
        builder.HasIndex(e => new { e.IsProcessed, e.CreatedAt })
            .HasDatabaseName("IX_OutboxEvents_IsProcessed_CreatedAt");
            
        builder.HasIndex(e => e.AggregateId)
            .HasDatabaseName("IX_OutboxEvents_AggregateId");
            
        builder.HasIndex(e => new { e.IsDeadLettered, e.DeadLetteredAt })
            .HasDatabaseName("IX_OutboxEvents_IsDeadLettered_DeadLetteredAt");
            
        builder.HasIndex(e => new { e.IsProcessed, e.IsDeadLettered, e.RetryCount })
            .HasDatabaseName("IX_OutboxEvents_ProcessingStatus");
    }
}