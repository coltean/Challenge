using System.Text.Json;
using Challenge.API.Configuration;
using Challenge.API.Models.Dto;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Challenge.API.Services;

public sealed class KafkaEventConsumer : BackgroundService
{
    private const int MaxRetryAttempts = 3;
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaEventConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConsumer<string, string> _consumer;
    private readonly IProducer<string, string> _producer;
    private readonly AsyncRetryPolicy _retryPolicy;

    public KafkaEventConsumer(
        IOptions<KafkaSettings> options,
        ILogger<KafkaEventConsumer> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        ValidateSettings();

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                MaxRetryAttempts,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (exception, delay, attempt, _) =>
                    _logger.LogWarning(exception,
                        "Retrying CMS event processing in {DelaySeconds}s (attempt {Attempt}/{MaxAttempts}).",
                        delay.TotalSeconds,
                        attempt,
                        MaxRetryAttempts));

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Kafka consumer for topic '{Topic}'", _settings.TopicName);
        _consumer.Subscribe(_settings.TopicName);

        return Task.Run(() => ConsumeLoopAsync(stoppingToken), stoppingToken);
    }

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = _consumer.Consume(stoppingToken);

                if (result?.Message?.Value == null)
                {
                    _logger.LogWarning("Received empty payload from Kafka, skipping.");
                    continue;
                }

                var success = await ProcessMessageAsync(result.Message.Key, result.Message.Value, stoppingToken);

                if (success)
                {
                    _consumer.Commit(result);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka consumer loop terminated unexpectedly.");
        }
    }

    private async Task<bool> ProcessMessageAsync(string? key, string payload, CancellationToken stoppingToken)
    {
        try
        {
            var cmsEvent = JsonSerializer.Deserialize<CmsEventDto>(payload);

            if (cmsEvent == null)
            {
                _logger.LogWarning("Deserialized CMS event was null, sending to dead-letter topic.");
                await SendToDeadLetterAsync(key, payload, stoppingToken);
                return true;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IEventProcessingService>();
            await _retryPolicy.ExecuteAsync(() => processor.ProcessEventAsync(cmsEvent));

            _logger.LogInformation("Successfully processed event {EventId}", cmsEvent.Id);
            return true;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Failed to deserialize CMS event payload, sending to dead-letter topic.");
            await SendToDeadLetterAsync(key, payload, stoppingToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process CMS event, sending to dead-letter topic.");
            await SendToDeadLetterAsync(key, payload, stoppingToken);
            return true;
        }
    }

    private async Task SendToDeadLetterAsync(string? key, string payload, CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.DeadLetterTopicName))
        {
            _logger.LogWarning("Dead-letter topic is not configured. Skipping DLQ publish.");
            return;
        }

        await _producer.ProduceAsync(
            _settings.DeadLetterTopicName,
            new Message<string, string> { Key = key, Value = payload },
            stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _consumer.Close();
        _consumer.Dispose();
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        return base.StopAsync(cancellationToken);
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

        if (string.IsNullOrWhiteSpace(_settings.ConsumerGroupId))
        {
            throw new InvalidOperationException("Kafka consumer group id must be configured.");
        }
    }
}
