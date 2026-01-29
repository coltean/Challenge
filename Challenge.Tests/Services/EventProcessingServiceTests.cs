using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Challenge.API.Data;
using Challenge.API.Models;
using Challenge.API.Models.Dto;
using Challenge.API.Services;
using Microsoft.Extensions.Logging;

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

        public EventProcessingServiceTests()
        {
            // Create in-memory database for testing
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _loggerMock = new Mock<ILogger<EventProcessingService>>();
            _service = new EventProcessingService(_context, _loggerMock.Object);
        }

        public void Dispose()
        {
            _context?.Dispose();
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
            await _service.ProcessEventsAsync(events);

            // Assert
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-1");
            entity.Should().NotBeNull();
            entity.CurrentPublishedVersion.Should().Be(1);
            entity.IsPublished.Should().BeTrue();

            var version = await _context.EntityVersions.FirstOrDefaultAsync(v => v.EntityId == "entity-1" && v.VersionNumber == 1);
            version.Should().NotBeNull();
            version.IsPublished.Should().BeTrue();
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

            await _service.ProcessEventsAsync(new List<CmsEventDto> { publishV1 });

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
            await _service.ProcessEventsAsync(new List<CmsEventDto> { publishV2 });

            // Assert
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-2");
            entity.CurrentPublishedVersion.Should().Be(2);
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
            await _service.ProcessEventsAsync(events);

            // Assert
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-3");
            entity.CurrentPublishedVersion.Should().Be(3);

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

            await _service.ProcessEventsAsync(publishEvents);

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
            await _service.ProcessEventsAsync(new List<CmsEventDto> { unpublishEvent });

            // Assert
            var v2 = await _context.EntityVersions
                .FirstOrDefaultAsync(v => v.EntityId == "entity-4" && v.VersionNumber == 2);
            v2.IsPublished.Should().BeFalse();
            v2.UnpublishedAt.Should().NotBeNull();

            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-4");
            // Should rollback to v1
            entity.CurrentPublishedVersion.Should().Be(1);
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

            await _service.ProcessEventsAsync(publishEvents);

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
            await _service.ProcessEventsAsync(new List<CmsEventDto> { unpublishV3 });

            // Assert - should rollback to v2
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-5");
            entity.CurrentPublishedVersion.Should().Be(2);
            entity.IsPublished.Should().BeTrue();

            var v3 = await _context.EntityVersions
                .FirstOrDefaultAsync(v => v.EntityId == "entity-5" && v.VersionNumber == 3);
            v3.IsPublished.Should().BeFalse();
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

            await _service.ProcessEventsAsync(new List<CmsEventDto> { publishV1 });

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
            await _service.ProcessEventsAsync(new List<CmsEventDto> { unpublishEvent });

            // Assert
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-6");
            entity.IsPublished.Should().BeFalse();
            entity.CurrentPublishedVersion.Should().Be(0);

            var version = await _context.EntityVersions
                .FirstOrDefaultAsync(v => v.EntityId == "entity-6" && v.VersionNumber == 1);
            version.IsPublished.Should().BeFalse();
        }

        [Fact]
        public async Task ProcessUnpublishEvent_CornerCase_UnpublishAllVersionsMakesEntityUnpublished()
        {
            // Arrange
            var publishEvents = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-7",
                    Version = 1,
                    Payload = new { v = "v1" },
                    Timestamp = DateTime.UtcNow
                },
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-7",
                    Version = 2,
                    Payload = new { v = "v2" },
                    Timestamp = DateTime.UtcNow.AddSeconds(1)
                }
            };

            await _service.ProcessEventsAsync(publishEvents);

            // Unpublish v2, then v1
            var unpublishV2 = new CmsEventDto
            {
                Type = "unPublish",
                Id = "entity-7",
                Version = 2,
                Payload = new { v = "v2" },
                Timestamp = DateTime.UtcNow.AddSeconds(2)
            };

            await _service.ProcessEventsAsync(new List<CmsEventDto> { unpublishV2 });

            var unpublishV1 = new CmsEventDto
            {
                Type = "unPublish",
                Id = "entity-7",
                Version = 1,
                Payload = new { v = "v1" },
                Timestamp = DateTime.UtcNow.AddSeconds(3)
            };

            // Act
            await _service.ProcessEventsAsync(new List<CmsEventDto> { unpublishV1 });

            // Assert
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-7");
            entity.IsPublished.Should().BeFalse();
            entity.CurrentPublishedVersion.Should().Be(0);

            var versions = await _context.EntityVersions
                .Where(v => v.EntityId == "entity-7")
                .ToListAsync();

            versions.Should().AllSatisfy(v => v.IsPublished.Should().BeFalse());
        }

        [Fact]
        public async Task ProcessUnpublishEvent_CornerCase_UnpublishNonExistentEntity_CreatesIt()
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
            await _service.ProcessEventsAsync(new List<CmsEventDto> { unpublishEvent });

            // Assert - Entity should be created but unpublished
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-never-published");
            entity.Should().NotBeNull();
            entity.IsPublished.Should().BeFalse();
            entity.CurrentPublishedVersion.Should().Be(0);

            var version = await _context.EntityVersions
                .FirstOrDefaultAsync(v => v.EntityId == "entity-never-published" && v.VersionNumber == 2);
            version.Should().NotBeNull();
            version.IsPublished.Should().BeFalse();
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

            await _service.ProcessEventsAsync(new List<CmsEventDto> { publishEvent });

            var deleteEvent = new CmsEventDto
            {
                Type = "delete",
                Id = "entity-delete-1",
                Timestamp = DateTime.UtcNow.AddSeconds(1)
            };

            // Act
            await _service.ProcessEventsAsync(new List<CmsEventDto> { deleteEvent });

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
                await _service.ProcessEventsAsync(new List<CmsEventDto> { deleteEvent }));

            exception.Should().BeNull();
        }

        [Fact]
        public async Task ProcessDeleteEvent_RemovesAllVersions_Cascaded()
        {
            // Arrange
            var publishEvents = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-delete-2",
                    Version = 1,
                    Payload = new { v = "v1" },
                    Timestamp = DateTime.UtcNow
                },
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-delete-2",
                    Version = 2,
                    Payload = new { v = "v2" },
                    Timestamp = DateTime.UtcNow.AddSeconds(1)
                },
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-delete-2",
                    Version = 3,
                    Payload = new { v = "v3" },
                    Timestamp = DateTime.UtcNow.AddSeconds(2)
                }
            };

            await _service.ProcessEventsAsync(publishEvents);

            var deleteEvent = new CmsEventDto
            {
                Type = "delete",
                Id = "entity-delete-2",
                Timestamp = DateTime.UtcNow.AddSeconds(3)
            };

            // Act
            await _service.ProcessEventsAsync(new List<CmsEventDto> { deleteEvent });

            // Assert
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-delete-2");
            entity.Should().BeNull();

            var versions = await _context.EntityVersions
                .Where(v => v.EntityId == "entity-delete-2")
                .ToListAsync();
            versions.Should().BeEmpty();
        }

        #endregion

        #region Batch Processing Tests

        [Fact]
        public async Task ProcessBatch_WithMixedEventTypes_ProcessesAllCorrectly()
        {
            // Arrange
            var batch = new List<CmsEventDto>
            {
                // Create entity-1 with v1
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-batch-1",
                    Version = 1,
                    Payload = new { title = "Entity 1" },
                    Timestamp = DateTime.UtcNow
                },
                // Create entity-2 with v1 and v2
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-batch-2",
                    Version = 1,
                    Payload = new { title = "Entity 2 v1" },
                    Timestamp = DateTime.UtcNow.AddSeconds(1)
                },
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-batch-2",
                    Version = 2,
                    Payload = new { title = "Entity 2 v2" },
                    Timestamp = DateTime.UtcNow.AddSeconds(2)
                },
                // Unpublish entity-2 v2
                new CmsEventDto
                {
                    Type = "unPublish",
                    Id = "entity-batch-2",
                    Version = 2,
                    Payload = new { title = "Entity 2 v2" },
                    Timestamp = DateTime.UtcNow.AddSeconds(3)
                },
                // Create and delete entity-3
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-batch-3",
                    Version = 1,
                    Payload = new { title = "Entity 3" },
                    Timestamp = DateTime.UtcNow.AddSeconds(4)
                },
                new CmsEventDto
                {
                    Type = "delete",
                    Id = "entity-batch-3",
                    Timestamp = DateTime.UtcNow.AddSeconds(5)
                }
            };

            // Act
            await _service.ProcessEventsAsync(batch);

            // Assert
            var entity1 = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-batch-1");
            entity1.Should().NotBeNull();
            entity1.CurrentPublishedVersion.Should().Be(1);
            entity1.IsPublished.Should().BeTrue();

            var entity2 = await _context.Entities
                .Include(e => e.Versions)
                .FirstOrDefaultAsync(e => e.Id == "entity-batch-2");
            entity2.Should().NotBeNull();
            entity2.CurrentPublishedVersion.Should().Be(1); // Rolled back from v2
            entity2.IsPublished.Should().BeTrue();
            entity2.Versions.Should().HaveCount(2);

            var entity3 = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-batch-3");
            entity3.Should().BeNull(); // Deleted
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
            await _service.ProcessEventsAsync(new List<CmsEventDto> { publishEvent });
            await _service.ProcessEventsAsync(new List<CmsEventDto> { publishEvent });

            // Assert - Should only have one WebhookEvent record
            var webhookEvents = await _context.WebhookEvents
                .Where(we => we.EntityId == "entity-dup")
                .ToListAsync();

            // Should have exactly one processed event
            webhookEvents.Should().HaveCount(1);
            webhookEvents[0].IsProcessed.Should().BeTrue();

            // Entity should still be correct
            var entity = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-dup");
            entity.CurrentPublishedVersion.Should().Be(1);
        }

        #endregion

        #region Transaction Rollback Tests

        [Fact]
        public async Task ProcessBatch_WithInvalidEvent_RollsBackAllChanges()
        {
            // Arrange
            var batch = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "entity-rollback-1",
                    Version = 1,
                    Payload = new { title = "Valid" },
                    Timestamp = DateTime.UtcNow
                },
                new CmsEventDto
                {
                    Type = "invalid_type", // Invalid type
                    Id = "entity-rollback-2",
                    Version = 1,
                    Payload = new { title = "Invalid" },
                    Timestamp = DateTime.UtcNow.AddSeconds(1)
                }
            };

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () =>
                await _service.ProcessEventsAsync(batch));

            exception.Should().NotBeNull();

            // Verify rollback - first event should NOT be saved
            var entity1 = await _context.Entities.FirstOrDefaultAsync(e => e.Id == "entity-rollback-1");
            entity1.Should().BeNull();

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
                await _service.ProcessEventsAsync(new List<CmsEventDto> { publishEvent }));

            exception.Should().NotBeNull(); // Validation should fail
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
            await _service.ProcessEventsAsync(new List<CmsEventDto> { publishEvent });

            // Assert
            var version = await _context.EntityVersions
                .FirstOrDefaultAsync(v => v.EntityId == "entity-complex" && v.VersionNumber == 1);
            version.Should().NotBeNull();
            version.Payload.Should().NotBeNullOrEmpty();
            version.Payload.Should().Contain("Complex Product");
            version.Payload.Should().Contain("SKU123");
        }

        #endregion
    }
}
