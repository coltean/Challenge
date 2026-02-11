using Challenge.API.Data.EntityTypeConfiguration;
using Challenge.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Challenge.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<EntityProjection> EntityProjections => Set<EntityProjection>();
        public DbSet<EntityVersionProjection> EntityVersionProjections => Set<EntityVersionProjection>();
        public DbSet<EventRecord> EventRecords => Set<EventRecord>();
        public DbSet<OutboxBatch> OutboxBatches => Set<OutboxBatch>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new EntityProjectionConfig());
            modelBuilder.ApplyConfiguration(new EntityVersionProjectionConfig());
            modelBuilder.ApplyConfiguration(new EventRecordConfig());
            modelBuilder.ApplyConfiguration(new OutboxBatchConfig());
        }
    }
}
