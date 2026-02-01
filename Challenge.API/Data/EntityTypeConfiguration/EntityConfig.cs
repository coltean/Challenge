using Challenge.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Challenge.API.Data.EntityTypeConfiguration
{
    public class EntityConfig : IEntityTypeConfiguration<Entity>
    {
        public void Configure(EntityTypeBuilder<Entity> entity)
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(255);
            entity.HasIndex(e => e.IsPublished);
            entity.HasIndex(e => e.IsDisabledByAdmin);

            entity.HasMany(e => e.Versions)
                .WithOne(v => v.Entity)
                .HasForeignKey(v => v.EntityId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
