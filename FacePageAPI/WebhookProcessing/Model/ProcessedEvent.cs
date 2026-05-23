namespace FacePageAPI.Model
{
    public class ProcessedEvent
    {
        public string EventId { get; set; }
        public string EventType { get; set; } // comment, message
        public string PageId { get; set; }
        public string PostId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Message { get; set; }
        public DateTime CreatedTime { get; set; }
        public string RawData { get; set; }

        // AI Analysis
        public bool IsSpam { get; set; }
        public string Intent { get; set; } // inquiry, complaint, praise, interaction
        public string Sentiment { get; set; } // positive, neutral, negative
        public double ConfidenceScore { get; set; }

        // State Management
        public EventState State { get; set; }
        public DateTime ProcessedAt { get; set; }
        public DateTime? RepliedAt { get; set; }
        public string FailureReason { get; set; }
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
        public string SpamReason { get; set; }
        public string Intent { get; set; }
        public string Sentiment { get; set; }
        public double ConfidenceScore { get; set; }
        public bool IsLinkOrBot { get; set; }
        public bool IsRepetitive { get; set; }
    }

    public class RawEvent
    {
        public string Id { get; set; }
        public long Time { get; set; }
        public Change Change { get; set; }
    }

}
