using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PostTradeSystem.Infrastructure.Entities;

namespace PostTradeSystem.Infrastructure.Configuration;

public class SnapshotEntityConfiguration : IEntityTypeConfiguration<SnapshotEntity>
{
    public void Configure(EntityTypeBuilder<SnapshotEntity> builder)
    {
        builder.ToTable("Snapshots");
        
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd();
            
        builder.Property(s => s.AggregateId)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(s => s.AggregateType)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(s => s.PartitionKey)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(s => s.AggregateVersion)
            .IsRequired();
            
        builder.Property(s => s.SnapshotData)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
            
        builder.Property(s => s.Metadata)
            .HasColumnType("nvarchar(max)");
            
        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(s => s.AggregateId)
            .HasDatabaseName("IX_Snapshots_AggregateId");
            
        builder.HasIndex(s => new { s.AggregateId, s.AggregateVersion })
            .IsUnique()
            .HasDatabaseName("IX_Snapshots_AggregateId_Version");
            
        builder.HasIndex(s => s.PartitionKey)
            .HasDatabaseName("IX_Snapshots_PartitionKey");
    }
}