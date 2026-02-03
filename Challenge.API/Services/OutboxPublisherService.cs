using System.Text.Json;
using Challenge.API.Data;
using Challenge.API.Models;
using Challenge.API.Models.Dto;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace Challenge.API.Services
{
    public sealed class OutboxPublisherService : BackgroundService
    {
        private const int MaxAttempts = 5;
        private const int MaxBatchesPerRun = 10;
        private const int PublishRetryAttempts = 3;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxPublisherService> _logger;
        private readonly AsyncRetryPolicy _publishRetryPolicy;

        public OutboxPublisherService(IServiceScopeFactory scopeFactory, ILogger<OutboxPublisherService> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _publishRetryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    PublishRetryAttempts,
                    attempt => TimeSpan.FromSeconds(Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, attempt))),
                    (exception, delay, attempt, _) =>
                        _logger.LogWarning(exception,
                            "Retrying outbox publish in {DelaySeconds}s (attempt {Attempt}/{MaxAttempts}).",
                            delay.TotalSeconds,
                            attempt,
                            PublishRetryAttempts));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Outbox publisher started");

            while (!stoppingToken.IsCancellationRequested)
            {
                await PublishPendingBatchesAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        private async Task PublishPendingBatchesAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var queueService = scope.ServiceProvider.GetRequiredService<IEventQueueService>();

            var now = DateTime.UtcNow;
            var pendingBatches = await dbContext.OutboxBatches
                .Where(batch => batch.Status == OutboxBatchStatus.Pending
                    && (batch.NextAttemptAt == null || batch.NextAttemptAt <= now))
                .OrderBy(batch => batch.CreatedAt)
                .Take(MaxBatchesPerRun)
                .ToListAsync(stoppingToken);

            if (pendingBatches.Count == 0)
            {
                return;
            }

            foreach (var batch in pendingBatches)
            {
                batch.Status = OutboxBatchStatus.Processing;
            }

            await dbContext.SaveChangesAsync(stoppingToken);

            foreach (var batch in pendingBatches)
            {
                await PublishBatchAsync(dbContext, queueService, batch, stoppingToken);
            }
        }

        private async Task PublishBatchAsync(
            ApplicationDbContext dbContext,
            IEventQueueService queueService,
            OutboxBatch batch,
            CancellationToken stoppingToken)
        {
            try
            {
                var events = JsonSerializer.Deserialize<List<CmsEventDto>>(batch.Payload);
                if (events == null || events.Count == 0)
                {
                    throw new InvalidOperationException("Outbox batch payload was empty.");
                }

                await _publishRetryPolicy.ExecuteAsync(() => queueService.EnqueueEventsAsync(events));

                batch.Status = OutboxBatchStatus.Completed;
                batch.ProcessedAt = DateTime.UtcNow;
                batch.NextAttemptAt = null;
                batch.LastError = null;

                _logger.LogInformation("Published outbox batch {BatchId} with {Count} events", batch.Id, events.Count);
            }
            catch (Exception ex)
            {
                batch.AttemptCount++;
                batch.LastError = ex.Message;

                if (batch.AttemptCount >= MaxAttempts)
                {
                    batch.Status = OutboxBatchStatus.DeadLettered;
                    batch.NextAttemptAt = null;

                    _logger.LogError(ex, "Outbox batch {BatchId} failed after {Attempts} attempts", batch.Id, batch.AttemptCount);
                }
                else
                {
                    var delaySeconds = Math.Pow(2, batch.AttemptCount);
                    var delay = TimeSpan.FromSeconds(Math.Min(MaxBackoff.TotalSeconds, delaySeconds));

                    batch.Status = OutboxBatchStatus.Pending;
                    batch.NextAttemptAt = DateTime.UtcNow.Add(delay);

                    _logger.LogWarning(ex,
                        "Outbox batch {BatchId} failed on attempt {Attempt}. Retrying in {DelaySeconds}s",
                        batch.Id,
                        batch.AttemptCount,
                        delay.TotalSeconds);
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
}
