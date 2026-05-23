using FacePageAPI.Model;
using System.Text;
using System.Text.Json;

namespace FacePageAPI.Service
{
    public class GeminiAIService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiAIService> _logger;
        private readonly string _apiKey;
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite-preview:generateContent";

        public GeminiAIService(IConfiguration configuration, ILogger<GeminiAIService> logger)
        {
            _logger = logger;
            _apiKey = configuration["Gemini:ApiKey"] ?? throw new Exception("Gemini API Key not found");
            _httpClient = new HttpClient();
        }

        public async Task<AIAnalysisResult> AnalyzeMessage(string message, string userName)
        {
            try
            {
                var prompt = BuildAnalysisPrompt(message, userName);
                var response = await CallGeminiAPI(prompt);
                return ParseAIResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"AI Analysis failed: {ex.Message}");
                // Fallback to basic spam detection
                return new AIAnalysisResult
                {
                    IsSpam = DetectSpamBasic(message),
                    Intent = "unknown",
                    Sentiment = "neutral",
                    ConfidenceScore = 0.5
                };
            }
        }

        private string BuildAnalysisPrompt(string message, string userName)
        {
            return $@"Phân tích comment sau đây từ người dùng '{userName}':
""{message}""
 
Trả về kết quả CHÍNH XÁC theo định dạng JSON sau (không có markdown, không có giải thích thêm):
{{
  ""isSpam"": true/false,
  ""spamReason"": ""lý do nếu là spam"",
  ""intent"": ""inquiry/complaint/praise/interaction"",
  ""sentiment"": ""positive/neutral/negative"",
  ""confidenceScore"": 0.0-1.0,
  ""isLinkOrBot"": true/false,
  ""isRepetitive"": true/false
}}
 
Hướng dẫn:
- isSpam: true nếu comment chứa link, lặp lại nhiều lần, quảng cáo, spam, bot
- intent: 
  * inquiry (hỏi giá, hỏi thông tin sản phẩm)
  * complaint (khiếu nại, phàn nàn)
  * praise (khen ngợi, tích cực)
  * interaction (tương tác bình thường, bình luận)
- sentiment: positive (tích cực), neutral (trung tính), negative (tiêu cực)
- confidenceScore: độ tin cậy từ 0.0 đến 1.0
- isLinkOrBot: true nếu có link hoặc nghi ngờ là bot
- isRepetitive: true nếu nội dung lặp lại, copy-paste
 
CHỈ TRẢ VỀ JSON, KHÔNG CÓ GÌ KHÁC!";
        }

        private async Task<string> CallGeminiAPI(string prompt)
        {
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    maxOutputTokens = 500
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{GEMINI_API_URL}?key={_apiKey}";
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API error: {error}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(responseJson);

            var text = result.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? "";
        }

        private AIAnalysisResult ParseAIResponse(string response)
        {
            try
            {
                // Remove markdown code blocks if present
                response = response.Trim();
                if (response.StartsWith("```json"))
                {
                    response = response.Substring(7);
                }
                if (response.StartsWith("```"))
                {
                    response = response.Substring(3);
                }
                if (response.EndsWith("```"))
                {
                    response = response.Substring(0, response.Length - 3);
                }
                response = response.Trim();

                var result = JsonSerializer.Deserialize<AIAnalysisResult>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? throw new Exception("Failed to parse AI response");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to parse AI response: {ex.Message}. Response: {response}");
                throw;
            }
        }

        private bool DetectSpamBasic(string message)
        {
            var spamKeywords = new[] { "http://", "https://", "www.", ".com", "inbox", "click here", "👉" };
            message = message.ToLower();
            return spamKeywords.Any(keyword => message.Contains(keyword));
        }
    }
}
