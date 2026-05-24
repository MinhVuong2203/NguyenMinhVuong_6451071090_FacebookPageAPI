using Microsoft.AspNetCore.Mvc;
using RetryService.Services;

namespace RetryService.Controllers
{
    [ApiController]
    [Route("api/retry")]
    public class RetryController : ControllerBase
    {
        private readonly RetryStateService _state;
        private readonly IConfiguration _configuration;

        public RetryController(RetryStateService state, IConfiguration configuration)
        {
            _state = state;
            _configuration = configuration;
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var maxRetries = _configuration.GetValue("Retry:MaxRetries", 3);
            var baseDelaySeconds = _configuration.GetValue("Retry:BaseDelaySeconds", 1);
            return Ok(_state.GetSnapshot(maxRetries, baseDelaySeconds));
        }
    }
}
