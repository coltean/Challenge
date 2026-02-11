using System.Text.Json;
using Challenge.API.Configuration;
using Challenge.API.Models.Dto;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Challenge.API.Services
{
    public sealed class KafkaEventQueueService : IEventQueueService, IDisposable
    {
        private readonly KafkaSettings _settings;
        private readonly ILogger<KafkaEventQueueService> _logger;
        private readonly IProducer<string, string> _producer;

        public KafkaEventQueueService(
            IOptions<KafkaSettings> options,
            ILogger<KafkaEventQueueService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            ValidateSettings();

            var config = new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                EnableIdempotence = true,
                Acks = Acks.All,
                LingerMs = 0,
            };

            _producer = new ProducerBuilder<string, string>(config).Build();
        }

        public async Task EnqueueEventsAsync(List<CmsEventDto> events)
        {
            if (events == null || events.Count == 0)
            {
                throw new ArgumentException("Events batch cannot be empty", nameof(events));
            }

            foreach (var @event in events)
            {
                var payload = JsonSerializer.Serialize(@event);
                var report = await _producer.ProduceAsync(
                    _settings.TopicName,
                    new Message<string, string> { Key = @event.Id, Value = payload });

                // This confirms it actually hit the broker and which partition it landed on
                _logger.LogInformation("Message delivered to {Topic} [Partition: {Partition}, Offset: {Offset}]",
                    report.Topic, report.Partition, report.Offset);
            }

            _logger.LogInformation("Enqueued {Count} events into Kafka topic '{Topic}' as individual messages", events.Count, _settings.TopicName);
        }

        private void ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(_settings.BootstrapServers))
            {
                throw new InvalidOperationException("Kafka bootstrap servers must be configured.");
            }

            if (string.IsNullOrWhiteSpace(_settings.TopicName))
            {
                throw new InvalidOperationException("Kafka topic name must be configured.");
            }
        }

        public void Dispose()
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
            _producer.Dispose();
        }
    }
}
