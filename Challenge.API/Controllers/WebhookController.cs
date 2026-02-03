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
        private readonly IOutboxBatchService _outboxBatchService;
        private readonly IValidator<List<CmsEventDto>> _batchValidator;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            IOutboxBatchService outboxBatchService,
            IValidator<List<CmsEventDto>> batchValidator,
            ILogger<WebhookController> logger)
        {
            _outboxBatchService = outboxBatchService;
            _batchValidator = batchValidator;
            _logger = logger;
        }

        /// <summary>
        /// Receives batch events from CMS, validates them, and enqueues them for background processing.
        /// The webhook endpoint remains fast by offloading persistence to a worker that reads from RabbitMQ.
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
                _logger.LogWarning("Invalid events batch validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new { errors });
            }

            _logger.LogInformation("Received batch of {Count} events from CMS", events.Count);

            try
            {
                var batchId = await _outboxBatchService.EnqueueBatchAsync(events, HttpContext.RequestAborted);

                _logger.LogInformation("Batch {BatchId} with {Count} events persisted to outbox", batchId, events.Count);

                // 202 Accepted: Batch has been accepted and is being/will be processed
                return Accepted(new { message = $"Batch of {events.Count} events accepted for processing", batchId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error persisting events batch to outbox");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Internal server error while saving events batch" });
            }
        }
    }
}
