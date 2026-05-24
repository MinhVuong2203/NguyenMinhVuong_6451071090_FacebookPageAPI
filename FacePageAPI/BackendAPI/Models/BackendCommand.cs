namespace BackendAPI.Models
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

    public class SentCommandRecord
    {
        public string IdempotencyKey { get; set; } = string.Empty;
        public string CommandId { get; set; } = string.Empty;
        public string CommandType { get; set; } = string.Empty;
        public string? EventId { get; set; }
        public string? UserId { get; set; }
        public DateTime SentAt { get; set; }
        public string FacebookResponse { get; set; } = string.Empty;
    }

    public class FailedCommandRecord
    {
        public string FailureId { get; set; } = string.Empty;
        public string IdempotencyKey { get; set; } = string.Empty;
        public string CommandId { get; set; } = string.Empty;
        public string CommandType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime FailedAt { get; set; }
        public int RetryCount { get; set; }
    }

    public class IdempotencyDatabase
    {
        public Dictionary<string, SentCommandRecord> SentCommands { get; set; } = new();
        public List<FailedCommandRecord> FailedCommands { get; set; } = new();
    }
}
