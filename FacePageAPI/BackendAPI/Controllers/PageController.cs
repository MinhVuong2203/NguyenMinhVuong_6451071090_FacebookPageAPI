using BackendAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendAPI.Controllers
{
    [Route("api/page")]
    [ApiController]
    public class PageController : ControllerBase
    {
        private readonly FacebookGraphApiClient _facebookGraphApiClient;

        public PageController(FacebookGraphApiClient facebookGraphApiClient)
        {
            _facebookGraphApiClient = facebookGraphApiClient;
        }

        [HttpGet("{pageId}")]
        public async Task<IActionResult> GetPage(string pageId)
        {
            var result = await _facebookGraphApiClient.GetPageAsync(pageId);
            return Content(result, "application/json");
        }

        [HttpGet("{pageId}/posts")]
        public async Task<IActionResult> GetPosts(string pageId)
        {
            var result = await _facebookGraphApiClient.GetPostsAsync(pageId);
            return Content(result, "application/json");
        }

        public class PostRequest
        {
            public string Message { get; set; } = string.Empty;
        }

        [HttpPost("{pageId}/posts")]
        public async Task<IActionResult> CreatePost(string pageId, [FromBody] PostRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Message is required.");
            }

            var result = await _facebookGraphApiClient.CreatePostAsync(pageId, request.Message);
            return Content(result, "application/json");
        }

        [HttpDelete("post/{postId}")]
        public async Task<IActionResult> DeletePost(string postId)
        {
            var result = await _facebookGraphApiClient.DeletePostAsync(postId);
            return Content(result, "application/json");
        }

        [HttpGet("post/{postId}/comments")]
        public async Task<IActionResult> GetComments(string postId)
        {
            var result = await _facebookGraphApiClient.GetCommentsAsync(postId);
            return Content(result, "application/json");
        }

        [HttpGet("post/{postId}/likes")]
        public async Task<IActionResult> GetLikes(string postId)
        {
            var result = await _facebookGraphApiClient.GetLikesAsync(postId);
            return Content(result, "application/json");
        }

        [HttpGet("{pageId}/insights")]
        public async Task<IActionResult> GetInsights(string pageId)
        {
            var result = await _facebookGraphApiClient.GetInsightsAsync(pageId);
            return Content(result, "application/json");
        }
    }
}
