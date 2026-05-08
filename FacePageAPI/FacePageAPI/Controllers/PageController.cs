using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FacePageAPI.Controllers
{
    [Route("api/page")]
    [ApiController]
    public class PageController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public PageController(IConfiguration config)
        {
            _httpClient = new HttpClient();
            _config = config;
        }
        private string AccessToken => _config["Facebook:PageAccessToken"];
        private string BaseUrl => _config["Facebook:BaseUrl"];

        // 1. GET /api/page/{pageId}    
        [HttpGet("{pageId}")]
        public async Task<IActionResult> GetPage(string pageId)
        {
            var token = _config["Facebook:PageAccessToken"];
            var baseUrl = _config["Facebook:BaseUrl"];

            var url = $"{baseUrl}/{pageId}?access_token={token}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 2. GET /api/page/{pageId}/posts   
        [HttpGet("{pageId}/posts")]
        public async Task<IActionResult> GetPosts(string pageId)
        {
            var url = $"{BaseUrl}/{pageId}/posts?access_token={AccessToken}";
            var res = await _httpClient.GetStringAsync(url);
            return Ok(res);
        }


        // 3. POST /api/page/{pageId}/posts
        public class PostRequest
        {
            public string Message { get; set; }
        }

        [HttpPost("{pageId}/posts")]
        public async Task<IActionResult> CreatePost(string pageId, [FromBody] PostRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Message))
                return BadRequest("Message is required");

            var url = $"{BaseUrl}/{pageId}/feed";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("message", request.Message),
                new KeyValuePair<string, string>("access_token", AccessToken)
            });

            var res = await _httpClient.PostAsync(url, content);
            var result = await res.Content.ReadAsStringAsync();

            return Ok(result);
        }

        // 4. DELETE /api/page/post/{postId}
    
        [HttpDelete("post/{postId}")]
        public async Task<IActionResult> DeletePost(string postId)
        {
            var url = $"{BaseUrl}/{postId}?access_token={AccessToken}";
            var res = await _httpClient.DeleteAsync(url);
            var result = await res.Content.ReadAsStringAsync();

            return Ok(result);
        }

        
        // 5. GET /api/page/post/{postId}/comments
   
        [HttpGet("post/{postId}/comments")]
        public async Task<IActionResult> GetComments(string postId)
        {
            var url = $"{BaseUrl}/{postId}/comments?access_token={AccessToken}";
            var res = await _httpClient.GetStringAsync(url);

            return Ok(res);
        }

        // 6. GET /api/page/post/{postId}/likes      
        [HttpGet("post/{postId}/likes")]
        public async Task<IActionResult> GetLikes(string postId)
        {
            var url = $"{BaseUrl}/{postId}/likes?access_token={AccessToken}";
            var res = await _httpClient.GetStringAsync(url);

            return Ok(res);
        }

        // 7. GET /api/page/{pageId}/insights
        [HttpGet("{pageId}/insights")]
        public async Task<IActionResult> GetInsights(string pageId)
        {
            var token = _config["Facebook:PageAccessToken"];
            var baseUrl = _config["Facebook:BaseUrl"];

            var url = $"{baseUrl}/{pageId}/insights?metric=page_follows&period=day&access_token={token}";

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            return StatusCode((int)response.StatusCode, content);
        }
    }
}