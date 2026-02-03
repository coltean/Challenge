using System.Text.Json;
using Challenge.API.Data;
using Challenge.API.Models;
using Challenge.API.Models.Dto;

namespace Challenge.API.Services
{
    public interface IOutboxBatchService
    {
        Task<string> EnqueueBatchAsync(List<CmsEventDto> events, CancellationToken cancellationToken = default);
    }

    public sealed class OutboxBatchService : IOutboxBatchService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OutboxBatchService> _logger;

        public OutboxBatchService(ApplicationDbContext context, ILogger<OutboxBatchService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> EnqueueBatchAsync(List<CmsEventDto> events, CancellationToken cancellationToken = default)
        {
            if (events == null || events.Count == 0)
            {
                throw new ArgumentException("Events batch cannot be empty", nameof(events));
            }

            var payload = JsonSerializer.Serialize(events);
            var batch = new OutboxBatch
            {
                Payload = payload,
                EventCount = events.Count,
                CreatedAt = DateTime.UtcNow,
                Status = OutboxBatchStatus.Pending
            };

            _context.OutboxBatches.Add(batch);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Persisted outbox batch {BatchId} with {Count} events", batch.Id, batch.EventCount);

            return batch.Id;
        }
    }
}
