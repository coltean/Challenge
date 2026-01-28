namespace Challenge.API.Models
{
    public class WebhookEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty; // publish, unPublish, delete
        public string EntityId { get; set; } = string.Empty;
        public int? Version { get; set; }
        public string? Payload { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
        public bool IsProcessed { get; set; }
    }
}
