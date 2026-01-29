using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Challenge.API.Data;

namespace Challenge.API.Controllers
{
    /// <summary>
    /// Health check endpoint for Docker container orchestration.
    /// Used by health checks and liveness/readiness probes.
    /// </summary>
    [ApiController]
    [Route("")]
    public class HealthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            ApplicationDbContext context,
            ILogger<HealthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Health check - used by Docker HEALTHCHECK directive.
        /// Returns 200 if service is healthy, 503 if unhealthy.
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Health()
        {
            try
            {
                // Check database connectivity
                await _context.Database.ExecuteSqlRawAsync("SELECT 1");

                _logger.LogInformation("Health check passed");
                return Ok(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Health check failed: {ex.Message}");
                return StatusCode(503, new
                {
                    status = "unhealthy",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Readiness check - indicates if service is ready to accept requests.
        /// Used by orchestrators for load balancer routing decisions.
        /// </summary>
        [HttpGet("ready")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Ready()
        {
            try
            {
                // Check database connectivity
                await _context.Database.ExecuteSqlRawAsync("SELECT 1");

                // Check pending migrations
                var pendingMigrations = (await _context.Database.GetPendingMigrationsAsync()).ToList();
                
                if (pendingMigrations.Any())
                {
                    _logger.LogWarning($"Pending migrations found: {string.Join(", ", pendingMigrations)}");
                    return StatusCode(503, new
                    {
                        status = "not_ready",
                        reason = "pending_migrations",
                        migrations = pendingMigrations
                    });
                }

                return Ok(new
                {
                    status = "ready",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Readiness check failed: {ex.Message}");
                return StatusCode(503, new
                {
                    status = "not_ready",
                    error = ex.Message
                });
            }
        }
    }
}
