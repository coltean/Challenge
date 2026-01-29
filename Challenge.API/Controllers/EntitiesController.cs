using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Challenge.API.Data;
using Challenge.API.Models.Dto;

namespace Challenge.API.Controllers
{
    /// <summary>
    /// REST API for entity consumers.
    /// Provides read access to published entities.
    /// Admins can disable entities (local override, doesn't affect CMS).
    /// 
    /// Authorization:
    /// - All endpoints require API_USER role
    /// - Some endpoints require ADMIN role
    /// </summary>
    [ApiController]
    [Route("api/entities")]
    [Authorize(Roles = "API_USER,ADMIN")]
    public class EntitiesController : ControllerBase
    {
        private readonly ReadOnlyDbContext _readContext;
        private readonly ApplicationDbContext _writeContext;
        private readonly ILogger<EntitiesController> _logger;

        // Admin users - feel free to customize
        private static readonly HashSet<string> AdminUserIds = new()
        {
            "admin",
            "admin@example.com"
        };

        public EntitiesController(
            ReadOnlyDbContext readContext,
            ApplicationDbContext writeContext,
            ILogger<EntitiesController> logger)
        {
            _readContext = readContext;
            _writeContext = writeContext;
            _logger = logger;
        }

        /// <summary>
        /// List all entities.
        /// Regular users see only published entities.
        /// Admins see all entities including disabled ones.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<EntityDto>>> GetEntities()
        {
            var isAdmin = IsAdmin();
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";

            _logger.LogInformation($"GetEntities called by user: {username} (Admin: {isAdmin})");

            var query = _readContext.Entities.AsQueryable();

            // Non-admins only see published entities
            if (!isAdmin)
            {
                query = query.Where(e => e.IsPublished && !e.IsDisabledByAdmin);
            }

            var entities = await query
                .Include(e => e.Versions)
                .AsNoTracking()
                .Select(e => new EntityDto
                {
                    Id = e.Id,
                    CurrentPublishedVersion = e.CurrentPublishedVersion,
                    IsPublished = e.IsPublished,
                    IsDisabledByAdmin = e.IsDisabledByAdmin,
                    LatestPayload = e.Versions
                        .Where(v => v.VersionNumber == e.CurrentPublishedVersion)
                        .Select(v => v.Payload)
                        .FirstOrDefault(),
                    CreatedAt = e.CreatedAt,
                    UpdatedAt = e.UpdatedAt
                })
                .ToListAsync();

            _logger.LogInformation($"Returned {entities.Count} entities to user {username}");

            return Ok(entities);
        }

        /// <summary>
        /// Get a specific entity by ID.
        /// Regular users see only published entities.
        /// Admins see all entities.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<EntityDto>> GetEntity(string id)
        {
            var isAdmin = IsAdmin();
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";

            _logger.LogInformation($"GetEntity called for {id} by user {username} (Admin: {isAdmin})");

            var entity = await _readContext.Entities
                .Include(e => e.Versions)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entity == null)
            {
                _logger.LogWarning($"Entity {id} not found");
                return NotFound(new { error = $"Entity {id} not found" });
            }

            // Non-admins can only access published entities
            if (!isAdmin && (!entity.IsPublished || entity.IsDisabledByAdmin))
            {
                _logger.LogWarning($"User {username} attempted to access unpublished/disabled entity {id}");
                return NotFound(new { error = $"Entity {id} not found" });
            }

            var dto = new EntityDto
            {
                Id = entity.Id,
                CurrentPublishedVersion = entity.CurrentPublishedVersion,
                IsPublished = entity.IsPublished,
                IsDisabledByAdmin = entity.IsDisabledByAdmin,
                LatestPayload = entity.Versions
                    .Where(v => v.VersionNumber == entity.CurrentPublishedVersion)
                    .Select(v => v.Payload)
                    .FirstOrDefault(),
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };

            _logger.LogInformation($"Returned entity {id} to user {username}");

            return Ok(dto);
        }

        /// <summary>
        /// Disable an entity (admin only).
        /// This is a local override that doesn't affect CMS data.
        /// Users cannot re-enable entities; only admins can.
        /// </summary>
        [HttpPut("{id}/disable")]
        [Authorize(Roles = "ADMIN")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DisableEntity(string id)
        {
            var admin = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
            _logger.LogInformation($"DisableEntity called for {id} by admin {admin}");

            var entity = await _writeContext.Entities.FirstOrDefaultAsync(e => e.Id == id);

            if (entity == null)
            {
                _logger.LogWarning($"DisableEntity: Entity {id} not found");
                return NotFound(new { error = $"Entity {id} not found" });
            }

            if (entity.IsDisabledByAdmin)
            {
                _logger.LogInformation($"Entity {id} is already disabled");
                return NoContent();
            }

            entity.IsDisabledByAdmin = true;
            entity.UpdatedAt = DateTime.UtcNow;

            await _writeContext.SaveChangesAsync();

            _logger.LogInformation($"Entity {id} disabled by admin {admin}");

            return NoContent();
        }

        /// <summary>
        /// Enable an entity (admin only).
        /// This is a local override that doesn't affect CMS data.
        /// </summary>
        [HttpPut("{id}/enable")]
        [Authorize(Roles = "ADMIN")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> EnableEntity(string id)
        {
            var admin = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
            _logger.LogInformation($"EnableEntity called for {id} by admin {admin}");

            var entity = await _writeContext.Entities.FirstOrDefaultAsync(e => e.Id == id);

            if (entity == null)
            {
                _logger.LogWarning($"EnableEntity: Entity {id} not found");
                return NotFound(new { error = $"Entity {id} not found" });
            }

            if (!entity.IsDisabledByAdmin)
            {
                _logger.LogInformation($"Entity {id} is already enabled");
                return NoContent();
            }

            entity.IsDisabledByAdmin = false;
            entity.UpdatedAt = DateTime.UtcNow;

            await _writeContext.SaveChangesAsync();

            _logger.LogInformation($"Entity {id} enabled by admin {admin}");

            return NoContent();
        }

        /// <summary>
        /// Helper method to determine if current user is an admin.
        /// </summary>
        private bool IsAdmin()
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "ADMIN")
                return true;

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return AdminUserIds.Contains(userId ?? "");
        }
    }
}
