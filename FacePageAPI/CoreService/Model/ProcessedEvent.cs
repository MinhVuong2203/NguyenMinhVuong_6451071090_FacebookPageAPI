namespace CoreService.Model
{
    public class NormalizedEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string EventId { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public string? PostId { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Message { get; set; }
        public DateTime CreatedTime { get; set; }
        public string RawData { get; set; } = string.Empty;
    }

    public class ProcessedEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty; // comment, message
        public string PageId { get; set; } = string.Empty;
        public string? PostId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public string RawData { get; set; } = string.Empty;

        // AI Analysis
        public bool IsSpam { get; set; }
        public string Intent { get; set; } = string.Empty; // inquiry, complaint, praise, interaction
        public string Sentiment { get; set; } = string.Empty; // positive, neutral, negative
        public double ConfidenceScore { get; set; }

        // State Management
        public EventState State { get; set; }
        public DateTime ProcessedAt { get; set; }
        public DateTime? RepliedAt { get; set; }
        public string? FailureReason { get; set; }
        public int RetryCount { get; set; }
    }

    public enum EventState
    {
        Received,
        Processing,
        Processed,
        Replied,
        Failed,
        Blocked,
        Hidden
    }

    public class AIAnalysisResult
    {
        public bool IsSpam { get; set; }
        public string? SpamReason { get; set; }
        public string Intent { get; set; } = "unknown";
        public string Sentiment { get; set; } = "neutral";
        public double ConfidenceScore { get; set; }
        public bool IsLinkOrBot { get; set; }
        public bool IsRepetitive { get; set; }
    }

    public class ReplyCommand
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
}

