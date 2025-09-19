using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PostTradeSystem.Infrastructure.Entities;

namespace PostTradeSystem.Infrastructure.Configuration;

public class IdempotencyEntityConfiguration : IEntityTypeConfiguration<IdempotencyEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyEntity> builder)
    {
        builder.ToTable("IdempotencyKeys");
        
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd();
            
        builder.Property(i => i.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(i => i.AggregateId)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(i => i.RequestHash)
            .IsRequired()
            .HasMaxLength(64);
            
        builder.Property(i => i.ResponseData)
            .HasColumnType("nvarchar(max)");
            
        builder.Property(i => i.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasDefaultValueSql("GETUTCDATE()");
            
        builder.Property(i => i.ExpiresAt)
            .IsRequired()
            .HasColumnType("datetime2");

        builder.HasIndex(i => i.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_IdempotencyKeys_IdempotencyKey");
            
        builder.HasIndex(i => i.ExpiresAt)
            .HasDatabaseName("IX_IdempotencyKeys_ExpiresAt");
            
        builder.HasIndex(i => new { i.AggregateId, i.RequestHash })
            .HasDatabaseName("IX_IdempotencyKeys_AggregateId_RequestHash");
    }
}