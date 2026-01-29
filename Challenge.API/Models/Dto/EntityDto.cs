namespace Challenge.API.Models.Dto
{
    /// <summary>
    /// DTO for returning entity data in REST API responses.
    /// Sanitizes internal database details from consumers.
    /// </summary>
    public class EntityDto
    {
        /// <summary>
        /// External entity ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Current published version number
        /// </summary>
        public int CurrentPublishedVersion { get; set; }

        /// <summary>
        /// Whether entity is published
        /// </summary>
        public bool IsPublished { get; set; }

        /// <summary>
        /// Whether entity has been disabled by admin
        /// (does not affect CMS, only local override)
        /// </summary>
        public bool IsDisabledByAdmin { get; set; }

        /// <summary>
        /// Latest published payload (JSON as string)
        /// </summary>
        public string? LatestPayload { get; set; }

        /// <summary>
        /// Entity creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
    }
}
