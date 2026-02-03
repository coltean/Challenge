namespace Challenge.API.Models
{
    public enum OutboxBatchStatus
    {
        Pending,
        Processing,
        Completed,
        DeadLettered
    }

    public class OutboxBatch
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Payload { get; set; } = string.Empty;
        public int EventCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public OutboxBatchStatus Status { get; set; } = OutboxBatchStatus.Pending;
        public int AttemptCount { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public string? LastError { get; set; }
    }
}
