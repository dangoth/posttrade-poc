using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PostTradeSystem.Infrastructure.Entities;

namespace PostTradeSystem.Infrastructure.Configuration;

public class ProjectionEntityConfiguration : IEntityTypeConfiguration<ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<ProjectionEntity> builder)
    {
        builder.ToTable("Projections");
        
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();
            
        builder.Property(p => p.ProjectionName)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(p => p.AggregateId)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(p => p.AggregateType)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(p => p.PartitionKey)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(p => p.LastProcessedVersion)
            .IsRequired();
            
        builder.Property(p => p.ProjectionData)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
            
        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasDefaultValueSql("GETUTCDATE()");
            
        builder.Property(p => p.UpdatedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(p => new { p.ProjectionName, p.AggregateId })
            .IsUnique()
            .HasDatabaseName("IX_Projections_ProjectionName_AggregateId");
            
        builder.HasIndex(p => p.PartitionKey)
            .HasDatabaseName("IX_Projections_PartitionKey");
            
        builder.HasIndex(p => new { p.ProjectionName, p.LastProcessedVersion })
            .HasDatabaseName("IX_Projections_ProjectionName_LastProcessedVersion");
    }
}