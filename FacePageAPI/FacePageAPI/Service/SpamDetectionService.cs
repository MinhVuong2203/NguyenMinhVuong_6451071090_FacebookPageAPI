using System.Collections.Concurrent;

namespace FacePageAPI.Service
{
    public class SpamDetectionService
    {
        private readonly ILogger<SpamDetectionService> _logger;

        // Track user comment count in 24h window
        private readonly ConcurrentDictionary<string, List<DateTime>> _userCommentHistory;

        // Blacklist for users who spam repeatedly
        private readonly HashSet<string> _blacklist;

        public SpamDetectionService(ILogger<SpamDetectionService> logger)
        {
            _logger = logger;
            _userCommentHistory = new ConcurrentDictionary<string, List<DateTime>>();
            _blacklist = new HashSet<string>();
        }

        public bool IsSpamByFrequency(string userId)
        {
            if (_blacklist.Contains(userId))
            {
                _logger.LogWarning($"User {userId} is in blacklist");
                return true;
            }

            var now = DateTime.UtcNow;
            var history = _userCommentHistory.GetOrAdd(userId, new List<DateTime>());

            lock (history)
            {
                // Remove comments older than 24h
                history.RemoveAll(t => (now - t).TotalHours > 24);

                // Add current comment
                history.Add(now);

                _logger.LogInformation($"User {userId} has {history.Count} comments in last 24h");

                // If more than 3 comments in 24h -> spam
                if (history.Count > 3)
                {
                    _logger.LogWarning($"User {userId} exceeded 3 comments in 24h -> SPAM");
                    AddToBlacklist(userId);
                    return true;
                }
            }

            return false;
        }

        public void AddToBlacklist(string userId)
        {
            _blacklist.Add(userId);
            _logger.LogWarning($"Added user {userId} to blacklist");
        }

        public bool IsInBlacklist(string userId)
        {
            return _blacklist.Contains(userId);
        }

        public void RemoveFromBlacklist(string userId)
        {
            _blacklist.Remove(userId);
            _logger.LogInformation($"Removed user {userId} from blacklist");
        }

        // Cleanup old data periodically
        public void CleanupOldData()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _userCommentHistory)
            {
                lock (kvp.Value)
                {
                    kvp.Value.RemoveAll(t => (now - t).TotalHours > 24);
                }
            }
        }
    }

}
