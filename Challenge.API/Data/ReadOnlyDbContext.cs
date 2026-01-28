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

            modelBuilder.Entity<Entity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasMaxLength(255);
                entity.HasIndex(e => e.IsPublished);
                entity.HasIndex(e => e.IsDisabledByAdmin);
            });

            modelBuilder.Entity<EntityVersion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityId).HasMaxLength(255);
                entity.HasIndex(e => new { e.EntityId, e.VersionNumber }).IsUnique();
            });
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
