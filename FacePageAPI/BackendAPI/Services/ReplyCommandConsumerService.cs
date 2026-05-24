using BackendAPI.Models;
using Confluent.Kafka;
using System.Text.Json;

namespace BackendAPI.Services
{
    public class ReplyCommandConsumerService : BackgroundService
    {
        private readonly ILogger<ReplyCommandConsumerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConsumer<string, string> _consumer;
        private readonly JsonSerializerOptions _jsonOptions;

        public ReplyCommandConsumerService(
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ILogger<ReplyCommandConsumerService> logger)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                GroupId = "backend-api-consumer-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Backend API Kafka consumer started.");
            await Task.Delay(5000, stoppingToken);

            _consumer.Subscribe(new[] { "reply_commands", "send_retry" });
            _logger.LogInformation("Subscribed to reply_commands and send_retry.");

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

                        await ProcessMessageAsync(consumeResult.Message.Value);
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
                        _logger.LogError($"Backend API processing error: {ex.Message}");
                    }
                }
            }
            finally
            {
                _consumer.Close();
                _logger.LogInformation("Backend API Kafka consumer stopped.");
            }
        }

        private async Task ProcessMessageAsync(string messageJson)
        {
            var command = JsonSerializer.Deserialize<BackendCommand>(messageJson, _jsonOptions);
            if (command == null)
            {
                _logger.LogWarning("Invalid backend command message.");
                return;
            }

            command.IdempotencyKey = ResolveIdempotencyKey(command);

            using var scope = _serviceProvider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IdempotencyStore>();
            var facebookClient = scope.ServiceProvider.GetRequiredService<FacebookGraphApiClient>();
            var kafkaProducer = scope.ServiceProvider.GetRequiredService<KafkaProducerService>();

            if (await store.HasProcessedAsync(command.IdempotencyKey))
            {
                _logger.LogInformation($"Skip duplicate command with idempotency key {command.IdempotencyKey}");
                return;
            }

            try
            {
                var facebookResponse = await facebookClient.ExecuteCommandAsync(command);
                await store.MarkProcessedAsync(command.IdempotencyKey, command, facebookResponse);
                _logger.LogInformation($"Command {command.CommandId} sent successfully. Key: {command.IdempotencyKey}");
            }
            catch (Exception ex)
            {
                var failedEvent = new SendFailedEvent
                {
                    FailureId = Guid.NewGuid().ToString(),
                    ErrorMessage = ex.Message,
                    FailedAt = DateTime.UtcNow,
                    Command = command
                };

                await store.RecordFailureAsync(failedEvent);
                await kafkaProducer.ProduceAsync("send_failed", failedEvent.FailureId, failedEvent);
                _logger.LogWarning($"Command {command.CommandId} failed and was published to send_failed.");
            }
        }

        private static string ResolveIdempotencyKey(BackendCommand command)
        {
            if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
            {
                return command.IdempotencyKey;
            }

            return command.CommandType switch
            {
                "reply_comment" or "hide_comment" => $"{command.CommandType}:{command.EventId}",
                "block_user" => $"{command.CommandType}:{command.PageId}:{command.UserId}",
                _ => $"{command.CommandType}:{command.CommandId}"
            };
        }

        public override void Dispose()
        {
            _consumer.Dispose();
            base.Dispose();
        }
    }
}
