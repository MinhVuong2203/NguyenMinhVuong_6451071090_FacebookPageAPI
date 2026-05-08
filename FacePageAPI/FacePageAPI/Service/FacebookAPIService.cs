namespace FacePageAPI.Service
{
    public class FacebookAPIService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FacebookAPIService> _logger;
        private readonly string _pageAccessToken;
        private const string FACEBOOK_GRAPH_API = "https://graph.facebook.com/v25.0";

        public FacebookAPIService(IConfiguration configuration, ILogger<FacebookAPIService> logger)
        {
            _logger = logger;
            _pageAccessToken = configuration["Facebook:PageAccessToken"]
                ?? throw new Exception("Facebook Page Access Token not found");
            _httpClient = new HttpClient();
        }

        // Hide comment
        public async Task<bool> HideComment(string commentId)
        {
            try
            {
                var url = $"{FACEBOOK_GRAPH_API}/{commentId}?is_hidden=true&access_token={_pageAccessToken}";
                var response = await _httpClient.PostAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Hidden comment {commentId}");
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ Failed to hide comment {commentId}: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error hiding comment {commentId}: {ex.Message}");
                return false;
            }
        }

        // Reply to comment
        public async Task<bool> ReplyToComment(string commentId, string message)
        {
            try
            {
                var url = $"{FACEBOOK_GRAPH_API}/{commentId}/comments?message={Uri.EscapeDataString(message)}&access_token={_pageAccessToken}";
                var response = await _httpClient.PostAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Replied to comment {commentId}");
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ Failed to reply to comment {commentId}: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error replying to comment {commentId}: {ex.Message}");
                return false;
            }
        }

        // Block user from page (requires manage_pages permission)
        public async Task<bool> BlockUserFromPage(string pageId, string userId)
        {
            try
            {
                var url = $"{FACEBOOK_GRAPH_API}/{pageId}/blocked?uid={userId}&access_token={_pageAccessToken}";
                var response = await _httpClient.PostAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Blocked user {userId} from page {pageId}");
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ Failed to block user {userId}: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error blocking user {userId}: {ex.Message}");
                return false;
            }
        }

        // Delete comment (if user violated policies severely)
        public async Task<bool> DeleteComment(string commentId)
        {
            try
            {
                var url = $"{FACEBOOK_GRAPH_API}/{commentId}?access_token={_pageAccessToken}";
                var response = await _httpClient.DeleteAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Deleted comment {commentId}");
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ Failed to delete comment {commentId}: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error deleting comment {commentId}: {ex.Message}");
                return false;
            }
        }
    }
}
