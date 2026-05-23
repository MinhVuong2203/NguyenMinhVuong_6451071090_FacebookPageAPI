using Newtonsoft.Json;

namespace FacePageAPI.Model
{
    // Model cho webhook verification
    public class WebhookVerification
    {
        [JsonProperty("hub.mode")]
        public string Mode { get; set; }

        [JsonProperty("hub.verify_token")]
        public string VerifyToken { get; set; }

        [JsonProperty("hub.challenge")]
        public string Challenge { get; set; }
    }

    // Model cho webhook event
    public class FacebookWebhookEvent
    {
        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("entry")]
        public List<Entry> Entry { get; set; }
    }

    public class Entry
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("changes")]
        public List<Change> Changes { get; set; }
    }

    public class Change
    {
        [JsonProperty("field")]
        public string Field { get; set; }

        [JsonProperty("value")]
        public Value Value { get; set; }
    }

    public class Value
    {
        [JsonProperty("from")]
        public From From { get; set; }

        [JsonProperty("post_id")]
        public string PostId { get; set; }

        [JsonProperty("verb")]
        public string Verb { get; set; }

        [JsonProperty("item")]
        public string Item { get; set; }

        [JsonProperty("comment_id")]
        public string CommentId { get; set; }

        [JsonProperty("created_time")]
        public long CreatedTime { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class From
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    // Model chuẩn hóa cho Kafka
    public class NormalizedEvent
    {
        public string EventType { get; set; } // "comment" hoặc "message"
        public string EventId { get; set; }
        public string PageId { get; set; }
        public string PostId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Message { get; set; }
        public DateTime CreatedTime { get; set; }
        public string RawData { get; set; }
    }
}