using Challenge.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Challenge.API.Data.EntityTypeConfiguration
{
    public class WebhookEventConfig : IEntityTypeConfiguration<WebhookEvent>
    {
        public void Configure(EntityTypeBuilder<WebhookEvent> entity)
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventId).HasMaxLength(255);
            entity.HasIndex(e => e.EventId).IsUnique();
        }
    }
}
