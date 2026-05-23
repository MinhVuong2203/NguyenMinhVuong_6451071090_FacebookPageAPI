using FacePageAPI.Model;

namespace FacePageAPI.Service
{
    public class ActionExecutorService
    {
        private readonly FacebookAPIService _facebookAPI;
        private readonly SpamDetectionService _spamDetection;
        private readonly ILogger<ActionExecutorService> _logger;

        public ActionExecutorService(
            FacebookAPIService facebookAPI,
            SpamDetectionService spamDetection,
            ILogger<ActionExecutorService> logger)
        {
            _facebookAPI = facebookAPI;
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
                    await _facebookAPI.HideComment(evt.EventId);
                    evt.State = EventState.Hidden;
                    return true;
                }

                // 2. Check if user is spamming by frequency
                if (_spamDetection.IsSpamByFrequency(evt.UserId))
                {
                    _logger.LogWarning($"[FREQUENCY SPAM] User {evt.UserName} ({evt.UserId}) spamming");

                    // Hide comment and add to blacklist
                    await _facebookAPI.HideComment(evt.EventId);
                    _spamDetection.AddToBlacklist(evt.UserId);

                    evt.State = EventState.Hidden;
                    return true;
                }

                // 3. Handle based on intent and sentiment
                var replyMessage = GenerateReply(analysis.Intent, analysis.Sentiment, evt.UserName);

                if (!string.IsNullOrEmpty(replyMessage))
                {
                    var success = await _facebookAPI.ReplyToComment(evt.EventId, replyMessage);

                    if (success)
                    {
                        evt.State = EventState.Replied;
                        evt.RepliedAt = DateTime.UtcNow;
                        _logger.LogInformation($"✅ Replied to comment {evt.EventId}");
                        return true;
                    }
                    else
                    {
                        evt.State = EventState.Failed;
                        evt.FailureReason = "Failed to send reply";
                        return false;
                    }
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

        private string GenerateReply(string intent, string sentiment, string userName)
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

                var success = await _facebookAPI.BlockUserFromPage(pageId, userId);

                if (success)
                {
                    _spamDetection.AddToBlacklist(userId);
                    _logger.LogInformation($"✅ Blocked user {userId}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error blocking user {userId}: {ex.Message}");
                return false;
            }
        }
    }
}
