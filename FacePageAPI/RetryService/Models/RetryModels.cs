namespace RetryService.Models
{
    public class BackendCommand
    {
        public string CommandId { get; set; } = string.Empty;
        public string IdempotencyKey { get; set; } = string.Empty;
        public string? EventId { get; set; }
        public string CommandType { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public string? PostId { get; set; }
        public string? UserId { get; set; }
        public string? Message { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public int RetryCount { get; set; }
    }

    public class SendFailedEvent
    {
        public string FailureId { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime FailedAt { get; set; }
        public BackendCommand Command { get; set; } = new();
    }

    public class DeadLetterEvent
    {
        public string DeadLetterId { get; set; } = string.Empty;
        public string OriginalFailureId { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime FailedAt { get; set; }
        public DateTime DeadLetteredAt { get; set; }
        public int MaxRetries { get; set; }
        public BackendCommand Command { get; set; } = new();
    }
}
