using Challenge.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Challenge.API.Data.EntityTypeConfiguration
{
    public class EntityVersionProjectionConfig : IEntityTypeConfiguration<EntityVersionProjection>
    {
        public void Configure(EntityTypeBuilder<EntityVersionProjection> entity)
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityId).HasMaxLength(255);
            entity.Property(e => e.Payload).HasColumnType("text");
            entity.HasIndex(e => new { e.EntityId, e.VersionNumber }).IsUnique();
        }
    }
}
