using System.Text.Json.Serialization;

namespace Challenge.API.Models.Dto
{
    /// <summary>
    /// DTO for incoming CMS event.
    /// Represents a single event in the webhook batch.
    /// </summary>
    public class CmsEventDto
    {
        /// <summary>
        /// Event type: "publish", "unpublish", or "delete"
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// External entity ID from CMS
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Entity payload (JSON object).
        /// Required for publish/unpublish events.
        /// Not required for delete events.
        /// </summary>
        [JsonPropertyName("payload")]
        public object? Payload { get; set; }

        /// <summary>
        /// Entity version number.
        /// Required for publish/unpublish events.
        /// Not required for delete events.
        /// First version is 1, increments with each update.
        /// </summary>
        [JsonPropertyName("version")]
        public int? Version { get; set; }

        /// <summary>
        /// Event timestamp in ISO 8601 format.
        /// Should not be in the future.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
