using Confluent.Kafka;
using RetryService.Models;
using System.Text.Json;

namespace RetryService.Services
{
    public class SendFailedConsumerService : BackgroundService
    {
        private readonly ILogger<SendFailedConsumerService> _logger;
        private readonly KafkaProducerService _producer;
        private readonly RetryStateService _state;
        private readonly IConsumer<string, string> _consumer;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly int _maxRetries;
        private readonly int _baseDelaySeconds;

        public SendFailedConsumerService(
            IConfiguration configuration,
            KafkaProducerService producer,
            RetryStateService state,
            ILogger<SendFailedConsumerService> logger)
        {
            _logger = logger;
            _producer = producer;
            _state = state;
            _maxRetries = configuration.GetValue("Retry:MaxRetries", 3);
            _baseDelaySeconds = configuration.GetValue("Retry:BaseDelaySeconds", 1);
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                GroupId = "retry-service-consumer-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Retry Service Kafka consumer started.");
            await Task.Delay(5000, stoppingToken);

            _consumer.Subscribe("send_failed");
            _logger.LogInformation("Subscribed to send_failed.");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));
                        if (consumeResult?.Message?.Value == null)
                        {
                            await Task.Delay(100, stoppingToken);
                            continue;
                        }

                        await ProcessFailedEventAsync(consumeResult.Message.Value, stoppingToken);
                        _consumer.Commit(consumeResult);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError($"Kafka consume error: {ex.Error.Reason}");
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Retry processing error: {ex.Message}");
                    }
                }
            }
            finally
            {
                _consumer.Close();
                _logger.LogInformation("Retry Service Kafka consumer stopped.");
            }
        }

        private async Task ProcessFailedEventAsync(string messageJson, CancellationToken stoppingToken)
        {
            var failedEvent = JsonSerializer.Deserialize<SendFailedEvent>(messageJson, _jsonOptions);
            if (failedEvent == null)
            {
                _logger.LogWarning("Invalid send_failed message.");
                return;
            }

            var currentRetryCount = failedEvent.Command.RetryCount;
            var nextRetryCount = currentRetryCount + 1;

            if (nextRetryCount > _maxRetries)
            {
                var deadLetter = new DeadLetterEvent
                {
                    DeadLetterId = Guid.NewGuid().ToString(),
                    OriginalFailureId = failedEvent.FailureId,
                    ErrorMessage = failedEvent.ErrorMessage,
                    FailedAt = failedEvent.FailedAt,
                    DeadLetteredAt = DateTime.UtcNow,
                    MaxRetries = _maxRetries,
                    Command = failedEvent.Command
                };

                await _producer.ProduceAsync("dead_letter", deadLetter.DeadLetterId, deadLetter);
                _state.MarkDeadLetter(failedEvent.FailureId);
                _logger.LogWarning($"Command {failedEvent.Command.CommandId} exceeded max retry count and was sent to dead_letter.");
                return;
            }

            var delaySeconds = CalculateDelaySeconds(currentRetryCount);
            _logger.LogInformation($"Retrying command {failedEvent.Command.CommandId} after {delaySeconds}s. Retry {nextRetryCount}/{_maxRetries}.");

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);

            failedEvent.Command.RetryCount = nextRetryCount;
            await _producer.ProduceAsync("send_retry", failedEvent.Command.CommandId, failedEvent.Command);
            _state.MarkRetry(failedEvent.FailureId);
        }

        private int CalculateDelaySeconds(int retryCount)
        {
            var exponent = Math.Min(retryCount, 10);
            return _baseDelaySeconds * (int)Math.Pow(2, exponent);
        }

        public override void Dispose()
        {
            _consumer.Dispose();
            base.Dispose();
        }
    }
}
