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
    /// 
    /// SYNCHRONOUS PROCESSING RATIONALE:
    /// ================================
    /// 
    /// This service uses synchronous (non-queued) processing because:
    /// 
    /// 1. VERSION SEQUENCING
    ///    - Events for the same entity MUST be processed in order
    ///    - Publish v1 ? Publish v2 ? Unpublish v2
    ///    - Async queues can reorder messages, breaking version integrity
    /// 
    /// 2. IDEMPOTENCY GUARANTEE
    ///    - Duplicate detection must happen BEFORE database write
    ///    - Synchronous: Check EventId ? Insert in same transaction
    ///    - Async: Risk of race condition between duplicate check and insert
    /// 
    /// 3. ATOMIC TRANSACTIONS
    ///    - All events in batch processed within single transaction
    ///    - If one fails, entire batch fails (easier rollback)
    ///    - Prevents partial state inconsistencies
    /// 
    /// 4. ACCEPTABLE LATENCY
    ///    - Max batch: 1000 events
    ///    - Typical DB throughput: 100-500ms per batch
    ///    - User receives 202 Accepted immediately
    ///    - Processing happens server-side (async from HTTP perspective)
    /// 
    /// 5. BETTER OBSERVABILITY
    ///    - All logging happens synchronously
    ///    - Error investigation is straightforward
    ///    - No need to correlate async failures with requests
    /// 
    /// ALTERNATIVE APPROACHES FOR HIGH-THROUGHPUT:
    /// ============================================
    /// If you need to handle 100k+ events/day:
    /// 
    /// Option A: Message Queue (Recommended for scale)
    ///   - RabbitMQ / Azure Service Bus
    ///   - Background worker processes queue
    ///   - Risk: Need version ordering per entity (use partition key)
    /// 
    /// Option B: Hangfire (Recommended for resilience)
    ///   - Background job scheduler
    ///   - Automatic retries + failure tracking
    ///   - Still maintains sequential processing per entity
    /// 
    /// Option C: Event Sourcing
    ///   - Event log as single source of truth
    ///   - Async projection to read model
    ///   - Complex but maximizes scalability
    /// </summary>
    public interface IEventProcessingService
    {
        Task ProcessEventsAsync(List<CmsEventDto> events);
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
        /// Process batch of events synchronously within a single transaction.
        /// </summary>
        public async Task ProcessEventsAsync(List<CmsEventDto> events)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    foreach (var @event in events)
                    {
                        await ProcessSingleEventAsync(@event);
                    }

                    // All events processed successfully - commit transaction
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation($"Successfully committed batch of {events.Count} events");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Transaction rolled back due to error: {ex.Message}", ex);

                    // Log individual failures for debugging
                    foreach (var @event in events)
                    {
                        await LogFailedEventAsync(@event, "Batch transaction rolled back");
                    }

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

                _context.WebhookEvents.Add(webhookEvent);

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
                _context.Entities.Add(entity);
            }

            // Check if version already exists
            var existingVersion = entity.Versions.FirstOrDefault(v => v.VersionNumber == @event.Version);
            if (existingVersion != null)
            {
                _logger.LogWarning($"Version {@event.Version} already exists for entity {@event.Id}, updating");
                existingVersion.Payload = @event.Payload != null ? JsonSerializer.Serialize(@event.Payload) : "";
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
                _logger.LogWarning(
                    $"Unpublish event received for non-existent entity: {@event.Id}. Creating with unpublished state.");
                
                // Create entity with unpublished version (corner case: unpublish before publish)
                entity = new Entity
                {
                    Id = @event.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsPublished = false,
                    IsDisabledByAdmin = false,
                    CurrentPublishedVersion = 0,
                    Versions = new List<EntityVersion>()
                };

                var version = new EntityVersion
                {
                    EntityId = @event.Id,
                    VersionNumber = @event.Version.Value,
                    Payload = @event.Payload != null ? JsonSerializer.Serialize(@event.Payload) : "",
                    IsPublished = false,
                    PublishedAt = DateTime.UtcNow,
                    UnpublishedAt = @event.Timestamp
                };

                entity.Versions.Add(version);
                _context.Entities.Add(entity);
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

                _context.WebhookEvents.Add(webhookEvent);
                // Note: Don't save here - let parent transaction handle it
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
