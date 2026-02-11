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
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await ProcessSingleEventAsync(@event);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Transaction rolled back due to error");
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        /// <summary>
        /// Process a single event: append to event store and update projections.
        /// </summary>
        private async Task ProcessSingleEventAsync(CmsEventDto @event)
        {
            var eventId = GenerateEventId(@event);

            var existingEvent = await _context.EventRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (existingEvent != null)
            {
                _logger.LogInformation("Skipping duplicate event: {EventId}", eventId);
                return;
            }

            var aggregateVersion = await _context.EventRecords
                .Where(e => e.AggregateId == @event.Id)
                .OrderByDescending(e => e.AggregateVersion)
                .Select(e => e.AggregateVersion)
                .FirstOrDefaultAsync();

            var eventType = @event.Type.ToLowerInvariant();

            var record = new EventRecord
            {
                EventId = eventId,
                AggregateId = @event.Id,
                AggregateVersion = aggregateVersion + 1,
                EventType = eventType,
                Data = JsonSerializer.Serialize(@event),
                Timestamp = @event.Timestamp
            };

            await _context.EventRecords.AddAsync(record);

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

            _logger.LogInformation("Appended event {EventId} and updated projections for {EntityId}", eventId, @event.Id);
        }

        private async Task HandlePublishEventAsync(CmsEventDto @event)
        {
            if (@event.Version == null)
                throw new InvalidOperationException("Publish event requires version");

            var entity = await _context.EntityProjections
                .Include(e => e.Versions)
                .FirstOrDefaultAsync(e => e.Id == @event.Id);

            if (entity == null)
            {
                entity = new EntityProjection
                {
                    Id = @event.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsPublished = false,
                    IsDisabledByAdmin = false,
                    CurrentPublishedVersion = 0,
                    Versions = new List<EntityVersionProjection>()
                };
                await _context.EntityProjections.AddAsync(entity);
            }

            var existingVersion = entity.Versions.FirstOrDefault(v => v.VersionNumber == @event.Version);
            if (existingVersion != null)
            {
                _logger.LogWarning("Version {Version} already exists for entity {EntityId}, updating", @event.Version, @event.Id);
                existingVersion.Payload = @event.Payload != null ? JsonSerializer.Serialize(@event.Payload) : "";
                existingVersion.IsPublished = true;
                existingVersion.PublishedAt = @event.Timestamp;
            }
            else
            {
                var version = new EntityVersionProjection
                {
                    EntityId = @event.Id,
                    VersionNumber = @event.Version.Value,
                    Payload = @event.Payload != null ? JsonSerializer.Serialize(@event.Payload) : "",
                    IsPublished = true,
                    PublishedAt = @event.Timestamp
                };

                entity.Versions.Add(version);
            }

            entity.CurrentPublishedVersion = @event.Version.Value;
            entity.IsPublished = true;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        private async Task HandleUnpublishEventAsync(CmsEventDto @event)
        {
            if (@event.Version == null)
                throw new InvalidOperationException("Unpublish event requires version");

            var entity = await _context.EntityProjections
                .Include(e => e.Versions)
                .FirstOrDefaultAsync(e => e.Id == @event.Id);

            if (entity == null)
            {
                _logger.LogWarning("Unpublish event received for non-existent entity: {EntityId}. Ignoring.", @event.Id);
                return;
            }

            var versionToUnpublish = entity.Versions.FirstOrDefault(v => v.VersionNumber == @event.Version);

            if (versionToUnpublish != null)
            {
                versionToUnpublish.IsPublished = false;
                versionToUnpublish.UnpublishedAt = @event.Timestamp;
            }
            else
            {
                _logger.LogWarning("Unpublish event for non-existent version {Version} of entity {EntityId}", @event.Version, @event.Id);
            }

            if (entity.CurrentPublishedVersion == @event.Version)
            {
                var previousPublishedVersion = entity.Versions
                    .Where(v => v.VersionNumber < @event.Version && v.IsPublished)
                    .OrderByDescending(v => v.VersionNumber)
                    .FirstOrDefault();

                if (previousPublishedVersion != null)
                {
                    entity.CurrentPublishedVersion = previousPublishedVersion.VersionNumber;
                    _logger.LogInformation("Rolled back to version {Version} for entity {EntityId}", previousPublishedVersion.VersionNumber, @event.Id);
                }
                else
                {
                    entity.IsPublished = false;
                    entity.CurrentPublishedVersion = 0;
                    _logger.LogInformation("No previous published version found, entity {EntityId} is now unpublished", @event.Id);
                }
            }

            entity.UpdatedAt = DateTime.UtcNow;
        }

        private async Task HandleDeleteEventAsync(CmsEventDto @event)
        {
            var entity = await _context.EntityProjections.FirstOrDefaultAsync(e => e.Id == @event.Id);

            if (entity != null)
            {
                _context.EntityProjections.Remove(entity);
                _logger.LogInformation("Marked entity {EntityId} for hard deletion", @event.Id);
            }
            else
            {
                _logger.LogWarning("Delete event received for non-existent entity: {EntityId}", @event.Id);
            }
        }

        private static string GenerateEventId(CmsEventDto @event)
        {
            var key = $"{@event.Type}_{@event.Id}_{@event.Version}_{@event.Timestamp:O}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            return Convert.ToHexString(bytes);
        }
    }
}
