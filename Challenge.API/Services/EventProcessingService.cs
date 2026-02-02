using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Challenge.API.Data;
using Challenge.API.Models;
using Challenge.API.Models.Dto;
using System.Security.Cryptography;
using System.Text;

namespace Challenge.API.Services
{
    /// <summary>
    /// Service responsible for processing incoming CMS events.
    /// </summary>
    public interface IEventProcessingService
    {
        Task ProcessEventAsync(CmsEventDto @event);
    }

    public class EventProcessingService : IEventProcessingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EventProcessingService> _logger;

        public EventProcessingService(
            ApplicationDbContext context,
            ILogger<EventProcessingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Process event asynchronously within a single transaction.
        /// </summary>
        public async Task ProcessEventAsync(CmsEventDto @event)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    await ProcessSingleEventAsync(@event);

                    // All events processed successfully - commit transaction
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Transaction rolled back due to error: {ex.Message}", ex);

                    // Log failures for debugging
                    await LogFailedEventAsync(@event, "Transaction rolled back");

                    _context.ChangeTracker.Clear();

                    throw;
                }
            }
        }

        /// <summary>
        /// Process a single event based on its type.
        /// </summary>
        private async Task ProcessSingleEventAsync(CmsEventDto @event)
        {
            try
            {
                // Generate idempotency key
                var eventId = GenerateEventId(@event);

                // Check for duplicate (already processed)
                var existingEvent = await _context.WebhookEvents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.EventId == eventId && e.IsProcessed);

                if (existingEvent != null)
                {
                    _logger.LogInformation($"Skipping duplicate event: {eventId}");
                    return;
                }

                var eventType = @event.Type.ToLower();

                switch (eventType)
                {
                    case "publish":
                        await HandlePublishEventAsync(@event);
                        break;
                    case "unpublish":
                        await HandleUnpublishEventAsync(@event);
                        break;
                    case "delete":
                        await HandleDeleteEventAsync(@event);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown event type: {eventType}");
                }

                // Log successful processing
                var webhookEvent = new WebhookEvent
                {
                    EventId = eventId,
                    EventType = @event.Type,
                    EntityId = @event.Id,
                    Version = @event.Version,
                    Payload = @event.Payload != null ? JsonSerializer.Serialize(@event.Payload) : null,
                    Timestamp = @event.Timestamp,
                    ProcessedAt = DateTime.UtcNow,
                    IsProcessed = true,
                    ErrorMessage = null
                };

                await _context.WebhookEvents.AddAsync(webhookEvent);

                _logger.LogInformation(
                    $"Successfully processed {eventType} event for entity {@event.Id} version {@event.Version}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing event {GenerateEventId(@event)}: {ex.Message}", ex);
                await LogFailedEventAsync(@event, ex.Message);
                throw; // Rethrow to trigger transaction rollback
            }
        }

        /// <summary>
        /// Handle PUBLISH event: Create or update entity with new version.
        /// </summary>
        private async Task HandlePublishEventAsync(CmsEventDto @event)
        {
            if (@event.Version == null)
                throw new InvalidOperationException("Publish event requires version");

            var entity = await _context.Entities
                .Include(e => e.Versions)
                .FirstOrDefaultAsync(e => e.Id == @event.Id);

            if (entity == null)
            {
                entity = new Entity
                {
                    Id = @event.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsPublished = false,
                    IsDisabledByAdmin = false,
                    CurrentPublishedVersion = 0,
                    Versions = new List<EntityVersion>()
                };
               await _context.Entities.AddAsync(entity);
            }

            // Check if version already exists
            var existingVersion = entity.Versions.FirstOrDefault(v => v.VersionNumber == @event.Version);
            if (existingVersion != null)
            {
                _logger.LogWarning($"Version {@event.Version} already exists for entity {@event.Id}, updating");
                existingVersion.Payload = @event.Payload != null ? JsonSerializer.Serialize(@event.Payload) : "";
                existingVersion.IsPublished = true;
                existingVersion.PublishedAt = @event.Timestamp;
            }
            else
            {
                var version = new EntityVersion
                {
                    EntityId = @event.Id,
                    VersionNumber = @event.Version.Value,
                    Payload = @event.Payload != null ? JsonSerializer.Serialize(@event.Payload) : "",
                    IsPublished = true,
                    PublishedAt = @event.Timestamp
                };

                entity.Versions.Add(version);
            }

            // Update entity state
            entity.CurrentPublishedVersion = @event.Version.Value;
            entity.IsPublished = true;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Handle UNPUBLISH event: Mark version as unpublished, handle version rollback.
        /// 
        /// CORNER CASE HANDLING:
        /// If unpublishing the current published version and no earlier published version exists,
        /// the entity becomes unpublished (never-published scenario).
        /// </summary>
        private async Task HandleUnpublishEventAsync(CmsEventDto @event)
        {
            if (@event.Version == null)
                throw new InvalidOperationException("Unpublish event requires version");

            var entity = await _context.Entities
                .Include(e => e.Versions)
                .FirstOrDefaultAsync(e => e.Id == @event.Id);

            if (entity == null)
            {
                _logger.LogWarning($"Unpublish event received for non-existent entity: {@event.Id}. Ignoring.");
                return;
            }

            // Find the version being unpublished
            var versionToUnpublish = entity.Versions.FirstOrDefault(v => v.VersionNumber == @event.Version);

            if (versionToUnpublish != null)
            {
                versionToUnpublish.IsPublished = false;
                versionToUnpublish.UnpublishedAt = @event.Timestamp;
            }
            else
            {
                _logger.LogWarning(
                    $"Unpublish event for non-existent version {@event.Version} of entity {@event.Id}");
            }

            // CORNER CASE: If unpublishing the current published version
            if (entity.CurrentPublishedVersion == @event.Version)
            {
                // Try to find previous published version
                var previousPublishedVersion = entity.Versions
                    .Where(v => v.VersionNumber < @event.Version && v.IsPublished)
                    .OrderByDescending(v => v.VersionNumber)
                    .FirstOrDefault();

                if (previousPublishedVersion != null)
                {
                    // Rollback to previous published version
                    entity.CurrentPublishedVersion = previousPublishedVersion.VersionNumber;
                    _logger.LogInformation(
                        $"Rolled back to version {previousPublishedVersion.VersionNumber} for entity {@event.Id}");
                }
                else
                {
                    // No previous published version exists: entity becomes unpublished
                    entity.IsPublished = false;
                    entity.CurrentPublishedVersion = 0;
                    _logger.LogInformation(
                        $"No previous published version found, entity {@event.Id} is now unpublished");
                }
            }

            entity.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Handle DELETE event: Hard-delete the entity and all versions.
        /// </summary>
        private async Task HandleDeleteEventAsync(CmsEventDto @event)
        {
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == @event.Id);

            if (entity != null)
            {
                // Hard delete: Remove entity and all versions (cascade delete)
                _context.Entities.Remove(entity);
                _logger.LogInformation($"Marked entity {@event.Id} for hard deletion");
            }
            else
            {
                _logger.LogWarning($"Delete event received for non-existent entity: {@event.Id}");
            }
        }

        /// <summary>
        /// Log a failed event to webhook events table for audit trail.
        /// </summary>
        private async Task LogFailedEventAsync(CmsEventDto @event, string error)
        {
            try
            {
                var webhookEvent = new WebhookEvent
                {
                    EventId = GenerateEventId(@event),
                    EventType = @event.Type,
                    EntityId = @event.Id,
                    Version = @event.Version,
                    Payload = @event.Payload != null ? JsonSerializer.Serialize(@event.Payload) : null,
                    Timestamp = @event.Timestamp,
                    ProcessedAt = DateTime.UtcNow,
                    IsProcessed = false,
                    ErrorMessage = error
                };

                await _context.WebhookEvents.AddAsync(webhookEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to log error event: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generate deterministic event ID for idempotency (idempotency key).
        /// </summary>
        private static string GenerateEventId(CmsEventDto @event)
        {
            // Create deterministic hash from event properties
            var key = $"{@event.Type}_{@event.Id}_{@event.Version}_{@event.Timestamp:O}";

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            return Convert.ToHexString(bytes);
        }
    }
}
