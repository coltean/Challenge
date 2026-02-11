using Challenge.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Challenge.API.Data.EntityTypeConfiguration
{
    public class EventRecordConfig : IEntityTypeConfiguration<EventRecord>
    {
        public void Configure(EntityTypeBuilder<EventRecord> entity)
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventId).HasMaxLength(255);
            entity.Property(e => e.AggregateId).HasMaxLength(255);
            entity.Property(e => e.Data).HasColumnType("text");
            entity.Property(e => e.Metadata).HasColumnType("text");

            entity.HasIndex(e => e.EventId).IsUnique();
            entity.HasIndex(e => new { e.AggregateId, e.AggregateVersion }).IsUnique();
            entity.HasIndex(e => e.AggregateId);
        }
    }
}
