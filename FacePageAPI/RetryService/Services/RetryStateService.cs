namespace RetryService.Services
{
    public class RetryStateService
    {
        private readonly object _lock = new();

        public int ConsumedFailedCount { get; private set; }
        public int RetriedCount { get; private set; }
        public int DeadLetterCount { get; private set; }
        public DateTime? LastProcessedAt { get; private set; }
        public string? LastFailureId { get; private set; }
        public string? LastAction { get; private set; }

        public void MarkRetry(string failureId)
        {
            lock (_lock)
            {
                ConsumedFailedCount++;
                RetriedCount++;
                LastFailureId = failureId;
                LastAction = "send_retry";
                LastProcessedAt = DateTime.UtcNow;
            }
        }

        public void MarkDeadLetter(string failureId)
        {
            lock (_lock)
            {
                ConsumedFailedCount++;
                DeadLetterCount++;
                LastFailureId = failureId;
                LastAction = "dead_letter";
                LastProcessedAt = DateTime.UtcNow;
            }
        }

        public object GetSnapshot(int maxRetries, int baseDelaySeconds)
        {
            lock (_lock)
            {
                return new
                {
                    service = "retry-service",
                    port = 3003,
                    consumes = new[] { "send_failed" },
                    publishes = new[] { "send_retry", "dead_letter" },
                    maxRetries,
                    baseDelaySeconds,
                    consumedFailedCount = ConsumedFailedCount,
                    retriedCount = RetriedCount,
                    deadLetterCount = DeadLetterCount,
                    lastFailureId = LastFailureId,
                    lastAction = LastAction,
                    lastProcessedAt = LastProcessedAt
                };
            }
        }
    }
}
