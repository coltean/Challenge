using Challenge.API.Data.EntityTypeConfiguration;
using Challenge.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Challenge.API.Data
{
    public class ReadOnlyDbContext : DbContext
    {
        public ReadOnlyDbContext(DbContextOptions<ReadOnlyDbContext> options) : base(options) { }

        public DbSet<EntityProjection> EntityProjections => Set<EntityProjection>();
        public DbSet<EntityVersionProjection> EntityVersionProjections => Set<EntityVersionProjection>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new EntityProjectionConfig());
            modelBuilder.ApplyConfiguration(new EntityVersionProjectionConfig());
            modelBuilder.ApplyConfiguration(new EventRecordConfig());
            modelBuilder.ApplyConfiguration(new OutboxBatchConfig());
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
