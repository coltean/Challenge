using Challenge.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Challenge.API.Data.EntityTypeConfiguration
{
    public class OutboxBatchConfig : IEntityTypeConfiguration<OutboxBatch>
    {
        public void Configure(EntityTypeBuilder<OutboxBatch> entity)
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Payload).HasColumnType("text");
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.NextAttemptAt);
        }
    }
}
