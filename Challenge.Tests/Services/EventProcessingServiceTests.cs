using Challenge.API.Data;
using Challenge.API.Models.Dto;
using Challenge.API.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Challenge.Tests.Services
{
    /// <summary>
    /// Comprehensive tests for event processing service.
    /// Tests publish, unpublish, delete events and corner cases.
    /// </summary>
    public class EventProcessingServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly IEventProcessingService _service;
        private readonly Mock<ILogger<EventProcessingService>> _loggerMock;
        private readonly SqliteConnection _connection;

        public EventProcessingServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            _loggerMock = new Mock<ILogger<EventProcessingService>>();
            _service = new EventProcessingService(_context, _loggerMock.Object);
        }

        private async Task ProcessEventsAsync(IEnumerable<CmsEventDto> events)
        {
            foreach (var @event in events)
            {
                await _service.ProcessEventAsync(@event);
            }
        }

        public void Dispose()
        {
            _context?.Dispose();
            _connection.Dispose();
        }

        #region Publish Event Tests

        [Fact]
        public async Task ProcessPublishEvent_CreatesNewEntity_WhenEntityDoesNotExist()
        {
            // Arrange
            var events = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-1",
                    Version = 1,
                    Payload = new { title = "Test Entity", price = 99.99 },
                    Timestamp = DateTime.UtcNow
                }
            };

            // Act
            await ProcessEventsAsync(events);

            // Assert
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-1");
            entity.Should().NotBeNull();
            entity!.CurrentPublishedVersion.Should().Be(1);
            entity.IsPublished.Should().BeTrue();

            var version = await _context.EntityVersions.FirstOrDefaultAsync(v => v.EntityId == "entity-1" && v.VersionNumber == 1);
            version.Should().NotBeNull();
            version!.IsPublished.Should().BeTrue();
        }

        [Fact]
        public async Task ProcessPublishEvent_UpdatesExistingEntity_WithNewVersion()
        {
            // Arrange
            // First, create entity with v1
            var publishV1 = new CmsEventDto
            {
                Type = "publish",
                Id = "entity-2",
                Version = 1,
                Payload = new { title = "Original", price = 100 },
                Timestamp = DateTime.UtcNow
            };

            await _service.ProcessEventAsync(publishV1);

            // Now publish v2
            var publishV2 = new CmsEventDto
            {
                Type = "publish",
                Id = "entity-2",
                Version = 2,
                Payload = new { title = "Updated", price = 90 },
                Timestamp = DateTime.UtcNow.AddSeconds(1)
            };

            // Act
            await _service.ProcessEventAsync(publishV2);

            // Assert
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-2");
            entity.Should().NotBeNull();
            entity!.CurrentPublishedVersion.Should().Be(2);
            entity.IsPublished.Should().BeTrue();

            var versions = await _context.EntityVersions
                .Where(v => v.EntityId == "entity-2")
                .OrderBy(v => v.VersionNumber)
                .ToListAsync();

            versions.Should().HaveCount(2);
            versions[0].VersionNumber.Should().Be(1);
            versions[1].VersionNumber.Should().Be(2);
            versions[0].IsPublished.Should().BeTrue();
            versions[1].IsPublished.Should().BeTrue();
        }

        [Fact]
        public async Task ProcessPublishEvent_HandlesMultipleVersions_SequentiallyCorrectly()
        {
            // Arrange
            var events = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-3",
                    Version = 1,
                    Payload = new { v = "v1" },
                    Timestamp = DateTime.UtcNow
                },
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-3",
                    Version = 2,
                    Payload = new { v = "v2" },
                    Timestamp = DateTime.UtcNow.AddSeconds(1)
                },
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-3",
                    Version = 3,
                    Payload = new { v = "v3" },
                    Timestamp = DateTime.UtcNow.AddSeconds(2)
                }
            };

            // Act
            await ProcessEventsAsync(events);

            // Assert
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-3");
            entity.Should().NotBeNull();
            entity!.CurrentPublishedVersion.Should().Be(3);

            var versions = await _context.EntityVersions
                .Where(v => v.EntityId == "entity-3")
                .ToListAsync();

            versions.Should().HaveCount(3);
            versions.Should().AllSatisfy(v => v.IsPublished.Should().BeTrue());
        }

        #endregion

        #region Unpublish Event Tests

        [Fact]
        public async Task ProcessUnpublishEvent_MarksVersionAsUnpublished()
        {
            // Arrange
            // Create entity with v1 and v2
            var publishEvents = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-4",
                    Version = 1,
                    Payload = new { title = "v1" },
                    Timestamp = DateTime.UtcNow
                },
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-4",
                    Version = 2,
                    Payload = new { title = "v2" },
                    Timestamp = DateTime.UtcNow.AddSeconds(1)
                }
            };

            await ProcessEventsAsync(publishEvents);

            // Unpublish v2
            var unpublishEvent = new CmsEventDto
            {
                Type = "unPublish",
                Id = "entity-4",
                Version = 2,
                Payload = new { title = "v2" },
                Timestamp = DateTime.UtcNow.AddSeconds(2)
            };

            // Act
            await _service.ProcessEventAsync(unpublishEvent);

            // Assert
            var v2 = await _context.EntityVersions
                .FirstOrDefaultAsync(v => v.EntityId == "entity-4" && v.VersionNumber == 2);
            v2.Should().NotBeNull();
            v2!.IsPublished.Should().BeFalse();
            v2.UnpublishedAt.Should().NotBeNull();

            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-4");
            entity.Should().NotBeNull();
            // Should rollback to v1
            entity!.CurrentPublishedVersion.Should().Be(1);
            entity.IsPublished.Should().BeTrue();
        }

        [Fact]
        public async Task ProcessUnpublishEvent_RollsBackToPreviousVersion_WhenCurrentVersionUnpublished()
        {
            // Arrange
            var publishEvents = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-5",
                    Version = 1,
                    Payload = new { v = "v1" },
                    Timestamp = DateTime.UtcNow
                },
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-5",
                    Version = 2,
                    Payload = new { v = "v2" },
                    Timestamp = DateTime.UtcNow.AddSeconds(1)
                },
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-5",
                    Version = 3,
                    Payload = new { v = "v3" },
                    Timestamp = DateTime.UtcNow.AddSeconds(2)
                }
            };

            await ProcessEventsAsync(publishEvents);

            // Unpublish v3 (current version)
            var unpublishV3 = new CmsEventDto
            {
                Type = "unPublish",
                Id = "entity-5",
                Version = 3,
                Payload = new { v = "v3" },
                Timestamp = DateTime.UtcNow.AddSeconds(3)
            };

            // Act
            await _service.ProcessEventAsync(unpublishV3);

            // Assert - should rollback to v2
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-5");
            entity.Should().NotBeNull();
            entity!.CurrentPublishedVersion.Should().Be(2);
            entity.IsPublished.Should().BeTrue();

            var v3 = await _context.EntityVersions
                .FirstOrDefaultAsync(v => v.EntityId == "entity-5" && v.VersionNumber == 3);
            v3.Should().NotBeNull();
            v3!.IsPublished.Should().BeFalse();
        }

        [Fact]
        public async Task ProcessUnpublishEvent_CornerCase_UnpublishOnlyVersionMakesEntityUnpublished()
        {
            // Arrange
            var publishV1 = new CmsEventDto
            {
                Type = "publish",
                Id = "entity-6",
                Version = 1,
                Payload = new { title = "Only version" },
                Timestamp = DateTime.UtcNow
            };

            await _service.ProcessEventAsync(publishV1);

            // Unpublish the only version
            var unpublishEvent = new CmsEventDto
            {
                Type = "unPublish",
                Id = "entity-6",
                Version = 1,
                Payload = new { title = "Only version" },
                Timestamp = DateTime.UtcNow.AddSeconds(1)
            };

            // Act
            await _service.ProcessEventAsync(unpublishEvent);

            // Assert
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-6");
            entity.Should().NotBeNull();
            entity!.IsPublished.Should().BeFalse();
            entity.CurrentPublishedVersion.Should().Be(0);

            var version = await _context.EntityVersions
                .FirstOrDefaultAsync(v => v.EntityId == "entity-6" && v.VersionNumber == 1);
            version.Should().NotBeNull();
            version!.IsPublished.Should().BeFalse();
        }

        [Fact]
        public async Task ProcessUnpublishEvent_CornerCase_UnpublishNonExistentEntity_DoesNotCreateIt()
        {
            // Arrange
            var unpublishEvent = new CmsEventDto
            {
                Type = "unPublish",
                Id = "entity-never-published",
                Version = 2,
                Payload = new { title = "Never published" },
                Timestamp = DateTime.UtcNow
            };

            // Act
            await _service.ProcessEventAsync(unpublishEvent);

            // Assert - Entity should not be created
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-never-published");
            entity.Should().BeNull();

            var version = await _context.EntityVersions
                .FirstOrDefaultAsync(v => v.EntityId == "entity-never-published" && v.VersionNumber == 2);
            version.Should().BeNull();
        }

        #endregion

        #region Delete Event Tests

        [Fact]
        public async Task ProcessDeleteEvent_HardDeletesEntity()
        {
            // Arrange
            var publishEvent = new CmsEventDto
            {
                Type = "publish",
                Id = "entity-delete-1",
                Version = 1,
                Payload = new { title = "Delete me" },
                Timestamp = DateTime.UtcNow
            };

            await _service.ProcessEventAsync(publishEvent);

            var deleteEvent = new CmsEventDto
            {
                Type = "delete",
                Id = "entity-delete-1",
                Timestamp = DateTime.UtcNow.AddSeconds(1)
            };

            // Act
            await _service.ProcessEventAsync(deleteEvent);

            // Assert
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-delete-1");
            entity.Should().BeNull();

            var versions = await _context.EntityVersions
                .Where(v => v.EntityId == "entity-delete-1")
                .ToListAsync();
            versions.Should().BeEmpty();
        }

        [Fact]
        public async Task ProcessDeleteEvent_DeleteNonExistentEntity_DoesNotThrow()
        {
            // Arrange
            var deleteEvent = new CmsEventDto
            {
                Type = "delete",
                Id = "entity-does-not-exist",
                Timestamp = DateTime.UtcNow
            };

            // Act & Assert - Should not throw
            var exception = await Record.ExceptionAsync(async () =>
                await _service.ProcessEventAsync(deleteEvent));

            exception.Should().BeNull();
        }

        #endregion

        #region Idempotency Tests

        [Fact]
        public async Task ProcessDuplicateEvents_SkipsSecondOccurrence()
        {
            // Arrange
            var publishEvent = new CmsEventDto
            {
                Type = "publish",
                Id = "entity-dup",
                Version = 1,
                Payload = new { title = "Duplicate test" },
                Timestamp = DateTime.UtcNow
            };

            // Act - Process twice
            await _service.ProcessEventAsync(publishEvent);
            await _service.ProcessEventAsync(publishEvent);

            // Assert - Should only have one WebhookEvent record
            var webhookEvents = await _context.WebhookEvents
                .Where(we => we.EntityId == "entity-dup")
                .ToListAsync();

            // Should have exactly one processed event
            webhookEvents.Should().HaveCount(1);
            webhookEvents[0].IsProcessed.Should().BeTrue();

            // Entity should still be correct
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-dup");
            entity.Should().NotBeNull();
            entity!.CurrentPublishedVersion.Should().Be(1);
        }

        #endregion

        #region Transaction Rollback Tests

        [Fact]
        public async Task ProcessEvent_WithInvalidEvent_DoesNotRollbackPreviousEvent()
        {
            // Arrange
            var validEvent = new CmsEventDto
            {
                Type = "publish",
                Id = "entity-rollback-1",
                Version = 1,
                Payload = new { title = "Valid" },
                Timestamp = DateTime.UtcNow
            };

            var invalidEvent = new CmsEventDto
            {
                Type = "invalid_type", // Invalid type
                Id = "entity-rollback-2",
                Version = 1,
                Payload = new { title = "Invalid" },
                Timestamp = DateTime.UtcNow.AddSeconds(1)
            };

            // Act
            await _service.ProcessEventAsync(validEvent);

            var exception = await Record.ExceptionAsync(async () =>
                await _service.ProcessEventAsync(invalidEvent));

            exception.Should().NotBeNull();

            // Verify first event was saved
            var entity1 = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-rollback-1");
            entity1.Should().NotBeNull();

            var entity2 = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-rollback-2");
            entity2.Should().BeNull();
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task ProcessEvent_WithNullPayload_HandlesGracefully()
        {
            // Arrange
            var publishEvent = new CmsEventDto
            {
                Type = "publish",
                Id = "entity-null-payload",
                Version = 1,
                Payload = null,
                Timestamp = DateTime.UtcNow
            };

            // Act & Assert - Should handle null payload
            var exception = await Record.ExceptionAsync(async () =>
                await _service.ProcessEventAsync(publishEvent));

            exception.Should().BeNull();

            var version = await _context.EntityVersions
                .FirstOrDefaultAsync(v => v.EntityId == "entity-null-payload" && v.VersionNumber == 1);
            version.Should().NotBeNull();
            version!.Payload.Should().BeEmpty();
        }

        [Fact]
        public async Task ProcessEvent_WithComplexPayload_SerializesCorrectly()
        {
            // Arrange
            var complexPayload = new
            {
                title = "Complex Product",
                price = 99.99,
                tags = new[] { "tag1", "tag2", "tag3" },
                metadata = new
                {
                    sku = "SKU123",
                    warehouse = "WH01",
                    stock = 100
                }
            };

            var publishEvent = new CmsEventDto
            {
                Type = "publish",
                Id = "entity-complex",
                Version = 1,
                Payload = complexPayload,
                Timestamp = DateTime.UtcNow
            };

            // Act
            await _service.ProcessEventAsync(publishEvent);

            // Assert
            var version = await _context.EntityVersions
                .FirstOrDefaultAsync(v => v.EntityId == "entity-complex" && v.VersionNumber == 1);
            version.Should().NotBeNull();
            version!.Payload.Should().NotBeNullOrEmpty();
            version.Payload.Should().Contain("Complex Product");
            version.Payload.Should().Contain("SKU123");
        }

        #endregion
    }
}
