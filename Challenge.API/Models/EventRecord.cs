namespace Challenge.API.Models
{
    public class EventRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EventId { get; set; } = string.Empty;
        public string AggregateId { get; set; } = string.Empty;
        public int AggregateVersion { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Metadata { get; set; }
    }
}
