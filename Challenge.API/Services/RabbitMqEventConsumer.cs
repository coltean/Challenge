using Challenge.API.Configuration;
using Challenge.API.Models.Dto;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Challenge.API.Services;

public sealed class RabbitMqEventConsumer : BackgroundService
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqEventConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly AsyncEventingBasicConsumer _consumer;

    public RabbitMqEventConsumer(
        IOptions<RabbitMqSettings> options,
        ILogger<RabbitMqEventConsumer> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        ValidateSettings();

        var connectionFactory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password,
            DispatchConsumersAsync = true
        };

        _connection = connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();

        var deadLetterExchange = $"{_settings.QueueName}.dlx";
        var deadLetterQueue = $"{_settings.QueueName}.dlq";

        _channel.ExchangeDeclare(deadLetterExchange, ExchangeType.Direct, durable: true);
        _channel.QueueDeclare(
            queue: deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        _channel.QueueBind(deadLetterQueue, deadLetterExchange, routingKey: deadLetterQueue);

        _channel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange,
                ["x-dead-letter-routing-key"] = deadLetterQueue
            });

        _channel.BasicQos(0, 1, false);

        _consumer = new AsyncEventingBasicConsumer(_channel);
        _consumer.Received += OnEventReceivedAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting RabbitMQ consumer for queue '{Queue}'", _settings.QueueName);

        _channel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: _consumer);

        stoppingToken.Register(() => _logger.LogInformation("RabbitMQ consumer is stopping"));

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Expected when the service is stopping
        }
    }

    private async Task OnEventReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        if (eventArgs.Body.IsEmpty)
        {
            _logger.LogWarning("Received empty payload from RabbitMQ, acknowledging and skipping.");
            _channel.BasicAck(eventArgs.DeliveryTag, false);
            return;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(eventArgs.Body.Span);
            var cmsEvent = JsonSerializer.Deserialize<CmsEventDto>(payload);

            if (cmsEvent == null)
            {
                _logger.LogWarning("Deserialized CMS event was null, rejecting message.");
                _channel.BasicReject(eventArgs.DeliveryTag, false);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IEventProcessingService>();
            await processor.ProcessEventAsync(cmsEvent);

            _channel.BasicAck(eventArgs.DeliveryTag, false);
            _logger.LogInformation("Successfully processed event {EventId}", cmsEvent.Id);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Failed to deserialize CMS event payload, discarding message.");
            _channel.BasicReject(eventArgs.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process CMS event, message will be dead-lettered.");
            _channel.BasicNack(eventArgs.DeliveryTag, false, false);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _consumer.Received -= OnEventReceivedAsync;
        _channel?.Close();
        _connection?.Close();
        return base.StopAsync(cancellationToken);
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
}
