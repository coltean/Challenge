using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using Challenge.API.Models.Dto;
using Challenge.API.Services;

namespace Challenge.API.Controllers
{
    [ApiController]
    [Route("api")]
    public class WebhookController : ControllerBase
    {
        private readonly IEventProcessingService _eventProcessingService;
        private readonly IValidator<List<CmsEventDto>> _batchValidator;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            IEventProcessingService eventProcessingService,
            IValidator<List<CmsEventDto>> batchValidator,
            ILogger<WebhookController> logger)
        {
            _eventProcessingService = eventProcessingService;
            _batchValidator = batchValidator;
            _logger = logger;
        }

        /// <summary>
        /// Receives batch events from CMS.
        /// Uses synchronous processing for data consistency and version sequencing.
        /// 
        /// Why Sync (not async)?
        /// 1. Version Integrity: Events must be processed in order (v1 ? v2 ? unpublish v2)
        /// 2. Idempotency: Duplicate detection happens atomically
        /// 3. Small Batches: Max 1000 events = ~500ms processing (acceptable latency)
        /// 4. Transaction Safety: Atomic all-or-nothing persistence
        /// 5. Observability: Easy error logging and debugging
        /// 
        /// For high-throughput scenarios (100k+ events/day), consider:
        /// - Message queue (RabbitMQ, Azure Service Bus)
        /// - Background job processor (Hangfire, Quartz)
        /// - Event sourcing with eventual consistency
        /// </summary>
        [HttpPost("cms/events")]
        [Authorize(Roles = "CMS_WEBHOOK")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReceiveEvents([FromBody] List<CmsEventDto> events)
        {
            if (events == null || events.Count == 0)
            {
                _logger.LogWarning("Received empty events batch from CMS");
                return BadRequest(new { error = "Events batch cannot be empty" });
            }

            // Validate batch
            var validationResult = await _batchValidator.ValidateAsync(events);

            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning($"Invalid events batch validation failed: {string.Join(", ", errors)}");
                return BadRequest(new { errors });
            }

            _logger.LogInformation($"Received batch of {events.Count} events from CMS");

            try
            {
                // Synchronous processing ensures:
                // - Version ordering is maintained
                // - Duplicate detection is atomic
                // - All-or-nothing transaction safety
                await _eventProcessingService.ProcessEventsAsync(events);

                _logger.LogInformation($"Successfully processed batch of {events.Count} events");

                // 202 Accepted: Batch has been accepted and is being/will be processed
                return Accepted(new { message = $"Batch of {events.Count} events accepted for processing" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Critical error processing events batch: {ex.Message}", ex);
                return StatusCode(500, new { error = "Internal server error while processing events" });
            }
        }
    }
}
