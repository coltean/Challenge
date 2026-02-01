using System.Text;
using System.Text.Json;
using Challenge.API.Configuration;
using Challenge.API.Models.Dto;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Challenge.API.Services
{
    public interface IEventQueueService
    {
        Task EnqueueEventsAsync(List<CmsEventDto> events);
    }

    public sealed class RabbitMqEventQueueService : IEventQueueService, IDisposable
    {
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<RabbitMqEventQueueService> _logger;
        private readonly IConnection _connection;

        public RabbitMqEventQueueService(
            IOptions<RabbitMqSettings> options,
            ILogger<RabbitMqEventQueueService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            ValidateSettings();

            var connectionFactory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password
            };

            _connection = connectionFactory.CreateConnection();

            using var channel = _connection.CreateModel();
            var deadLetterExchange = $"{_settings.QueueName}.dlx";
            var deadLetterQueue = $"{_settings.QueueName}.dlq";

            channel.ExchangeDeclare(deadLetterExchange, ExchangeType.Direct, durable: true);
            channel.QueueDeclare(
                queue: deadLetterQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            channel.QueueBind(deadLetterQueue, deadLetterExchange, routingKey: deadLetterQueue);

            channel.QueueDeclare(
                queue: _settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object>
                {
                    ["x-dead-letter-exchange"] = deadLetterExchange,
                    ["x-dead-letter-routing-key"] = deadLetterQueue
                });
        }

        public Task EnqueueEventsAsync(List<CmsEventDto> events)
        {
            if (events == null || events.Count == 0)
            {
                throw new ArgumentException("Events batch cannot be empty", nameof(events));
            }

            using var channel = _connection.CreateModel();
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;

            foreach (var @event in events)
            {
                var payload = JsonSerializer.Serialize(@event);
                var body = Encoding.UTF8.GetBytes(payload);

                channel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: _settings.QueueName,
                    basicProperties: properties,
                    body: body);
            }

            _logger.LogInformation("Enqueued {Count} events into RabbitMQ queue '{Queue}' as individual messages", events.Count, _settings.QueueName);

            return Task.CompletedTask;
        }

        private void ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(_settings.HostName))
            {
                throw new InvalidOperationException("RabbitMQ host name must be configured.");
            }

            if (string.IsNullOrWhiteSpace(_settings.QueueName))
            {
                throw new InvalidOperationException("RabbitMQ queue name must be configured.");
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
