using BackendAPI.Models;

namespace BackendAPI.Services
{
    public class FacebookGraphApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FacebookGraphApiClient> _logger;
        private readonly string _baseUrl;
        private readonly string _pageAccessToken;

        public FacebookGraphApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<FacebookGraphApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["Facebook:BaseUrl"] ?? "https://graph.facebook.com/v25.0";
            _pageAccessToken = configuration["Facebook:PageAccessToken"]
                ?? throw new InvalidOperationException("Facebook PageAccessToken is missing.");
        }

        public async Task<string> ExecuteCommandAsync(BackendCommand command)
        {
            return command.CommandType switch
            {
                "reply_comment" => await ReplyToCommentAsync(command),
                "hide_comment" => await HideCommentAsync(command),
                "block_user" => await BlockUserAsync(command),
                _ => throw new InvalidOperationException($"Unsupported command type: {command.CommandType}")
            };
        }

        public async Task<string> GetPageAsync(string pageId)
        {
            return await SendRawAsync(HttpMethod.Get, $"{_baseUrl}/{pageId}?access_token={_pageAccessToken}");
        }

        public async Task<string> GetPostsAsync(string pageId)
        {
            return await SendRawAsync(HttpMethod.Get, $"{_baseUrl}/{pageId}/posts?access_token={_pageAccessToken}");
        }

        public async Task<string> CreatePostAsync(string pageId, string message)
        {
            var url = $"{_baseUrl}/{pageId}/feed";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("message", message),
                new KeyValuePair<string, string>("access_token", _pageAccessToken)
            });

            return await SendRawAsync(HttpMethod.Post, url, content);
        }

        public async Task<string> DeletePostAsync(string postId)
        {
            return await SendRawAsync(HttpMethod.Delete, $"{_baseUrl}/{postId}?access_token={_pageAccessToken}");
        }

        public async Task<string> GetCommentsAsync(string postId)
        {
            return await SendRawAsync(HttpMethod.Get, $"{_baseUrl}/{postId}/comments?access_token={_pageAccessToken}");
        }

        public async Task<string> GetLikesAsync(string postId)
        {
            return await SendRawAsync(HttpMethod.Get, $"{_baseUrl}/{postId}/likes?access_token={_pageAccessToken}");
        }

        public async Task<string> GetInsightsAsync(string pageId)
        {
            var url = $"{_baseUrl}/{pageId}/insights?metric=page_follows&period=day&access_token={_pageAccessToken}";
            return await SendRawAsync(HttpMethod.Get, url);
        }

        private async Task<string> ReplyToCommentAsync(BackendCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.EventId))
            {
                throw new InvalidOperationException("reply_comment command requires EventId.");
            }

            if (string.IsNullOrWhiteSpace(command.Message))
            {
                throw new InvalidOperationException("reply_comment command requires Message.");
            }

            var url = $"{_baseUrl}/{command.EventId}/comments";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("message", command.Message),
                new KeyValuePair<string, string>("access_token", _pageAccessToken)
            });

            return await SendRawAsync(HttpMethod.Post, url, content);
        }

        private async Task<string> HideCommentAsync(BackendCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.EventId))
            {
                throw new InvalidOperationException("hide_comment command requires EventId.");
            }

            var url = $"{_baseUrl}/{command.EventId}";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("is_hidden", "true"),
                new KeyValuePair<string, string>("access_token", _pageAccessToken)
            });

            return await SendRawAsync(HttpMethod.Post, url, content);
        }

        private async Task<string> BlockUserAsync(BackendCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.PageId) || string.IsNullOrWhiteSpace(command.UserId))
            {
                throw new InvalidOperationException("block_user command requires PageId and UserId.");
            }

            var url = $"{_baseUrl}/{command.PageId}/blocked";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("uid", command.UserId),
                new KeyValuePair<string, string>("access_token", _pageAccessToken)
            });

            return await SendRawAsync(HttpMethod.Post, url, content);
        }

        private async Task<string> SendRawAsync(HttpMethod method, string url, HttpContent? content = null)
        {
            using var request = new HttpRequestMessage(method, url)
            {
                Content = content
            };

            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Facebook Graph API failed: {(int)response.StatusCode} {responseBody}");
                throw new InvalidOperationException($"Facebook Graph API failed: {(int)response.StatusCode} {responseBody}");
            }

            return responseBody;
        }
    }
}
