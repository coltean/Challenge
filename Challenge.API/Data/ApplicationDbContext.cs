using Challenge.API.Data.EntityTypeConfiguration;
using Challenge.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Challenge.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Entity> Entities => Set<Entity>();
        public DbSet<EntityVersion> EntityVersions => Set<EntityVersion>();
        public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
        public DbSet<OutboxBatch> OutboxBatches => Set<OutboxBatch>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new EntityConfig());
            modelBuilder.ApplyConfiguration(new EntityVersionConfig());
            modelBuilder.ApplyConfiguration(new WebhookEventConfig());
            modelBuilder.ApplyConfiguration(new OutboxBatchConfig());
        }
    }
}
