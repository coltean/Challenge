namespace Challenge.API.Configuration
{
    public sealed class KafkaSettings
    {
        public string BootstrapServers { get; set; } = "broker:29092";
        public string TopicName { get; set; } = "cms-events";
        public string DeadLetterTopicName { get; set; } = "cms-events.dlq";
        public string ConsumerGroupId { get; set; } = "cms-events-consumer";
        public string? SchemaRegistryUrl { get; set; }
    }
}
