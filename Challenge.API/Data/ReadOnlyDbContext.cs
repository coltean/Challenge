using Challenge.API.Data.EntityTypeConfiguration;
using Challenge.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Challenge.API.Data
{
    public class ReadOnlyDbContext : DbContext
    {
        public ReadOnlyDbContext(DbContextOptions<ReadOnlyDbContext> options) : base(options) { }

        public DbSet<Entity> Entities => Set<Entity>();
        public DbSet<EntityVersion> EntityVersions => Set<EntityVersion>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new EntityConfig());
            modelBuilder.ApplyConfiguration(new EntityVersionConfig());
            modelBuilder.ApplyConfiguration(new WebhookEventConfig());
        }

        public override int SaveChanges()
        {
            throw new InvalidOperationException("Read-only context cannot save changes.");
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Read-only context cannot save changes.");
        }
    }
}
