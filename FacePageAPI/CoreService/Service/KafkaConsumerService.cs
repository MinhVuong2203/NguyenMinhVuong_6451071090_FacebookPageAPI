using Confluent.Kafka;
using CoreService.Model;
using System.Text.Json;

namespace CoreService.Service
{
    public class KafkaConsumerService : BackgroundService
    {
        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly IConfiguration _configuration;
        private readonly GeminiAIService _geminiAI;
        private readonly SpamDetectionService _spamDetection;
        private readonly ActionExecutorService _actionExecutor;
        private readonly StateManagementService _stateManagement;
        private readonly IConsumer<string, string> _consumer;

        public KafkaConsumerService(
            ILogger<KafkaConsumerService> logger,
            IConfiguration configuration,
            GeminiAIService geminiAI,
            SpamDetectionService spamDetection,
            ActionExecutorService actionExecutor,
            StateManagementService stateManagement)
        {
            _logger = logger;
            _configuration = configuration;
            _geminiAI = geminiAI;
            _spamDetection = spamDetection;
            _actionExecutor = actionExecutor;
            _stateManagement = stateManagement;

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                GroupId = "core-service-consumer-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false // Manual commit for reliability
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Core Service Kafka Consumer started");

            // Delay để ASP.NET server start hoàn toàn trước
            await Task.Delay(5000, stoppingToken);

            _consumer.Subscribe("raw_events");

            _logger.LogInformation("✅ Subscribed to raw_events");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Consume message với timeout
                        var consumeResult = _consumer.Consume(
                            TimeSpan.FromSeconds(1));

                        // Không có message thì tiếp tục loop
                        if (consumeResult?.Message?.Value == null)
                        {
                            await Task.Delay(100, stoppingToken);
                            continue;
                        }

                        _logger.LogInformation(
                            $"📨 Received event: {consumeResult.Message.Key}");

                        // Xử lý event
                        await ProcessEvent(consumeResult.Message.Value);

                        // Commit offset sau khi xử lý thành công
                        _consumer.Commit(consumeResult);

                        _logger.LogInformation(
                            $"✅ Committed offset for event: {consumeResult.Message.Key}");
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(
                            $"❌ Kafka consume error: {ex.Error.Reason}");
                    }
                    catch (OperationCanceledException)
                    {
                        // App shutdown
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            $"❌ Processing error: {ex.Message}");

                        _logger.LogError(
                            $"❌ Stack trace: {ex.StackTrace}");
                    }

                    // Tránh loop ăn CPU
                    await Task.Delay(100, stoppingToken);
                }
            }
            finally
            {
                _consumer.Close();
                _logger.LogInformation("🛑 Kafka consumer stopped");
            }
        }

        private async Task ProcessEvent(string messageJson)
        {
            ProcessedEvent? processedEvent = null;

            try
            {
                _logger.LogInformation($"Processing raw event: {messageJson}");

                var normalizedEvent =
                    JsonSerializer.Deserialize<NormalizedEvent>(messageJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (normalizedEvent == null)
                {
                    _logger.LogWarning("Invalid normalized event");
                    return;
                }

                // Convert to ProcessedEvent
                processedEvent = new ProcessedEvent
                {
                    EventId = normalizedEvent.EventId,
                    EventType = normalizedEvent.EventType,
                    PageId = normalizedEvent.PageId,
                    PostId = normalizedEvent.PostId,
                    UserId = normalizedEvent.UserId ?? "unknown",
                    UserName = normalizedEvent.UserName ?? "Unknown User",
                    Message = normalizedEvent.Message ?? "",
                    CreatedTime = normalizedEvent.CreatedTime,
                    RawData = normalizedEvent.RawData,

                    State = EventState.Received,
                    ProcessedAt = DateTime.UtcNow,
                    RetryCount = 0
                };

                _stateManagement.UpdateState(processedEvent);

                // Skip if message is empty
                if (string.IsNullOrWhiteSpace(processedEvent.Message))
                {
                    _logger.LogWarning("Empty message, skipping AI analysis");
                    processedEvent.State = EventState.Processed;
                    _stateManagement.UpdateState(processedEvent);
                    return;
                }

                _logger.LogInformation($"🤖 Analyzing message from {processedEvent.UserName}: {processedEvent.Message}");

                // Update state to Processing
                processedEvent.State = EventState.Processing;
                _stateManagement.UpdateState(processedEvent);

                // AI Analysis
                var analysis = await _geminiAI.AnalyzeMessage(processedEvent.Message, processedEvent.UserName);

                // Update event with AI results
                processedEvent.IsSpam = analysis.IsSpam;
                processedEvent.Intent = analysis.Intent;
                processedEvent.Sentiment = analysis.Sentiment;
                processedEvent.ConfidenceScore = analysis.ConfidenceScore;

                _logger.LogInformation($"📊 AI Analysis Result:");
                _logger.LogInformation($"  - Spam: {analysis.IsSpam} (Link/Bot: {analysis.IsLinkOrBot})");
                _logger.LogInformation($"  - Intent: {analysis.Intent}");
                _logger.LogInformation($"  - Sentiment: {analysis.Sentiment}");
                _logger.LogInformation($"  - Confidence: {analysis.ConfidenceScore:P}");

                // Execute appropriate action
                var success = await _actionExecutor.ExecuteAction(processedEvent, analysis);

                if (!success)
                {
                    _logger.LogWarning($"Action execution failed for event {processedEvent.EventId}");
                }

                _stateManagement.UpdateState(processedEvent);

                _logger.LogInformation($"✅ Event {processedEvent.EventId} processed successfully - State: {processedEvent.State}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error processing event: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");

                if (processedEvent != null)
                {
                    processedEvent.State = EventState.Failed;
                    processedEvent.FailureReason = ex.Message;
                    processedEvent.RetryCount++;
                    _stateManagement.UpdateState(processedEvent);

                    // Retry logic: if retry count < 3, can implement retry
                    if (processedEvent.RetryCount < 3)
                    {
                        _logger.LogInformation($"Will retry event {processedEvent.EventId} later (retry count: {processedEvent.RetryCount})");
                    }
                }
            }
        }

        public override void Dispose()
        {
            _consumer?.Dispose();
            base.Dispose();
        }
    }
}
