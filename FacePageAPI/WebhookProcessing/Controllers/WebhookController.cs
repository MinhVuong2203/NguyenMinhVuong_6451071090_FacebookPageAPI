using FacePageAPI.Model;
using FacePageAPI.Service;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace FacePageAPI.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly KafkaProducerService _kafkaProducer;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            KafkaProducerService kafkaProducer,
            IConfiguration configuration,
            ILogger<WebhookController> logger)
        {
            _kafkaProducer = kafkaProducer;
            _configuration = configuration;
            _logger = logger;
        }

        // GET /webhook - Webhook verification
        [HttpGet]
        public IActionResult VerifyWebhook([FromQuery(Name = "hub.mode")] string mode,
                                          [FromQuery(Name = "hub.verify_token")] string token,
                                          [FromQuery(Name = "hub.challenge")] string challenge)
        {
            var verifyToken = _configuration["Facebook:VerifyToken"];

            if (mode == "subscribe" && token == verifyToken)
            {
                _logger.LogInformation("Webhook verified successfully");
                return Ok(challenge);
            }

            _logger.LogWarning("Webhook verification failed");
            return Unauthorized();
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveWebhook()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var json = await reader.ReadToEndAsync();

                _logger.LogInformation($"=== WEBHOOK RECEIVED ===");
                _logger.LogInformation($"Raw JSON: {json}");

                if (!IsValidFacebookSignature(json))
                {
                    _logger.LogWarning("Invalid Facebook webhook signature.");
                    return Unauthorized(new { status = "INVALID_SIGNATURE" });
                }

                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogWarning("Empty body!");
                    return Ok(new { status = "EVENT_RECEIVED" });
                }

                var webhookEvent = JsonConvert.DeserializeObject<FacebookWebhookEvent>(json);

                if (webhookEvent?.Entry == null)
                {
                    _logger.LogWarning("Entry is null but raw saved!");
                    return Ok(new { status = "EVENT_RECEIVED" });
                }

                _logger.LogInformation($"Processing {webhookEvent.Entry.Count} entries");

                foreach (var entry in webhookEvent.Entry)
                {
                    _logger.LogInformation($"Entry ID: {entry.Id}, Changes: {entry.Changes?.Count ?? 0}");

                    foreach (var change in entry.Changes ?? new List<Change>())
                    {
                        _logger.LogInformation($"Change field: {change.Field}");

                        var normalizedEvent = NormalizeEvent(entry, change);
                        if (normalizedEvent != null)
                        {
                            await _kafkaProducer.ProduceAsync(
                                "raw_events",
                                normalizedEvent.EventId,
                                normalizedEvent
                            );
                            _logger.LogInformation("✅ Pushed normalized event to Kafka raw_events");
                        }
                    }
                }

                return Ok(new { status = "EVENT_RECEIVED" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ ERROR: {ex.Message}");
                _logger.LogError($"Stack: {ex.StackTrace}");
                return Ok(new { status = "EVENT_RECEIVED" });
            }
        }

        private NormalizedEvent NormalizeEvent(Entry entry, Change change)
        {
            try
            {
                var value = change.Value;

                // Xác định loại event (comment hoặc message)
                string eventType = change.Field switch
                {
                    "feed" when value.Item == "comment" => "comment",
                    "messages" => "message",
                    _ => "unknown"
                };

                if (eventType == "unknown")
                {
                    return null;
                }

                return new NormalizedEvent
                {
                    EventType = eventType,
                    EventId = value.CommentId ?? value.PostId ?? Guid.NewGuid().ToString(),
                    PageId = entry.Id,
                    PostId = value.PostId,
                    UserId = value.From?.Id,
                    UserName = value.From?.Name,
                    Message = value.Message,
                    CreatedTime = DateTimeOffset.FromUnixTimeSeconds(value.CreatedTime).DateTime,
                    RawData = JsonConvert.SerializeObject(change)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error normalizing event: {ex.Message}");
                return null;
            }
        }

        private bool IsValidFacebookSignature(string body)
        {
            var appSecret = _configuration["Facebook:AppSecret"];
            if (string.IsNullOrWhiteSpace(appSecret))
            {
                _logger.LogWarning("Facebook:AppSecret is not configured. Skipping HMAC signature verification.");
                return true;
            }

            if (!Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader))
            {
                return false;
            }

            const string prefix = "sha256=";
            var signature = signatureHeader.ToString();
            if (!signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var providedSignatureHex = signature[prefix.Length..];
            byte[] providedHash;
            try
            {
                providedHash = Convert.FromHexString(providedSignatureHex);
            }
            catch (FormatException)
            {
                return false;
            }

            var secretBytes = Encoding.UTF8.GetBytes(appSecret);
            var bodyBytes = Encoding.UTF8.GetBytes(body);

            using var hmac = new HMACSHA256(secretBytes);
            var computedHash = hmac.ComputeHash(bodyBytes);

            return providedHash.Length == computedHash.Length &&
                   CryptographicOperations.FixedTimeEquals(providedHash, computedHash);
        }
    }
}
