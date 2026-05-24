using CoreService.Model;

namespace CoreService.Service
{
    public class ActionExecutorService
    {
        private readonly KafkaProducerService _kafkaProducer;
        private readonly SpamDetectionService _spamDetection;
        private readonly ILogger<ActionExecutorService> _logger;

        public ActionExecutorService(
            KafkaProducerService kafkaProducer,
            SpamDetectionService spamDetection,
            ILogger<ActionExecutorService> logger)
        {
            _kafkaProducer = kafkaProducer;
            _spamDetection = spamDetection;
            _logger = logger;
        }

        public async Task<bool> ExecuteAction(ProcessedEvent evt, AIAnalysisResult analysis)
        {
            try
            {
                // 1. If spam -> hide immediately
                if (analysis.IsSpam || analysis.IsLinkOrBot)
                {
                    _logger.LogWarning($"[SPAM DETECTED] Comment {evt.EventId} from {evt.UserName}");
                    await PublishCommand("hide_comment", evt, reason: analysis.SpamReason ?? "Spam detected");
                    evt.State = EventState.Hidden;
                    return true;
                }

                // 2. Check if user is spamming by frequency
                if (_spamDetection.IsSpamByFrequency(evt.UserId))
                {
                    _logger.LogWarning($"[FREQUENCY SPAM] User {evt.UserName} ({evt.UserId}) spamming");

                    await PublishCommand("hide_comment", evt, reason: "Frequency spam");
                    _spamDetection.AddToBlacklist(evt.UserId);

                    evt.State = EventState.Hidden;
                    return true;
                }

                // 3. Handle based on intent and sentiment
                var replyMessage = GenerateReply(analysis.Intent, analysis.Sentiment, evt.UserName);

                if (!string.IsNullOrEmpty(replyMessage))
                {
                    await PublishCommand("reply_comment", evt, replyMessage);
                    evt.State = EventState.Replied;
                    evt.RepliedAt = DateTime.UtcNow;
                    _logger.LogInformation($"✅ Published reply command for comment {evt.EventId}");
                    return true;
                }

                // 4. Mark as processed (no action needed)
                evt.State = EventState.Processed;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error executing action for event {evt.EventId}: {ex.Message}");
                evt.State = EventState.Failed;
                evt.FailureReason = ex.Message;
                return false;
            }
        }

        private Task PublishCommand(string commandType, ProcessedEvent evt, string? message = null, string? reason = null)
        {
            var command = new ReplyCommand
            {
                CommandId = Guid.NewGuid().ToString(),
                IdempotencyKey = $"{commandType}:{evt.EventId}",
                EventId = evt.EventId,
                CommandType = commandType,
                PageId = evt.PageId,
                PostId = evt.PostId,
                UserId = evt.UserId,
                Message = message,
                Reason = reason,
                CreatedAt = DateTime.UtcNow,
                RetryCount = evt.RetryCount
            };

            return _kafkaProducer.ProduceAsync("reply_commands", command.CommandId, command);
        }

        private string? GenerateReply(string intent, string sentiment, string userName)
        {
            // Generate contextual reply based on intent and sentiment
            return intent switch
            {
                "inquiry" when sentiment == "neutral" || sentiment == "positive" =>
                    $"Cảm ơn bạn {userName} đã quan tâm! Shop sẽ inbox thông tin chi tiết cho bạn ngay nhé! 😊",

                "inquiry" when sentiment == "negative" =>
                    $"Xin lỗi bạn {userName}! Shop sẽ inbox hỗ trợ bạn ngay ạ! 🙏",

                "complaint" =>
                    $"Shop rất xin lỗi vì trải nghiệm chưa tốt của bạn {userName}. Shop sẽ inbox để hỗ trợ bạn giải quyết vấn đề ngay nhé! 🙏",

                "praise" =>
                    $"Cảm ơn bạn {userName} rất nhiều! Shop rất vui khi bạn hài lòng! 🥰❤️",

                "interaction" when sentiment == "positive" =>
                    $"Cảm ơn bạn {userName} đã ủng hộ! 😍",

                _ => null // No reply needed
            };
        }

        // Block user if they violate policies severely
        public async Task<bool> BlockUser(string pageId, string userId, string reason)
        {
            try
            {
                _logger.LogWarning($"[BLOCKING USER] {userId} - Reason: {reason}");

                var command = new ReplyCommand
                {
                    CommandId = Guid.NewGuid().ToString(),
                    IdempotencyKey = $"block_user:{pageId}:{userId}",
                    CommandType = "block_user",
                    PageId = pageId,
                    UserId = userId,
                    Reason = reason,
                    CreatedAt = DateTime.UtcNow
                };

                await _kafkaProducer.ProduceAsync("reply_commands", command.CommandId, command);
                _spamDetection.AddToBlacklist(userId);
                _logger.LogInformation($"✅ Published block command for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error blocking user {userId}: {ex.Message}");
                return false;
            }
        }
    }
}

