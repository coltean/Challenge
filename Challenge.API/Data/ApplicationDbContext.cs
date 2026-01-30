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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Entity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasMaxLength(255);
                entity.HasIndex(e => e.IsPublished);
                entity.HasIndex(e => e.IsDisabledByAdmin);

                entity.HasMany(e => e.Versions)
                    .WithOne(v => v.Entity)
                    .HasForeignKey(v => v.EntityId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EntityVersion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityId).HasMaxLength(255);
                entity.Property(e => e.Payload).HasColumnType("text"); // PostgreSQL
                entity.HasIndex(e => new { e.EntityId, e.VersionNumber }).IsUnique();
                entity.HasIndex(e => e.IsPublished);
            });

            modelBuilder.Entity<WebhookEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EventId).HasMaxLength(255);
                entity.HasIndex(e => e.EventId).IsUnique();
                entity.HasIndex(e => e.IsProcessed);
            });
        }
    }
}
