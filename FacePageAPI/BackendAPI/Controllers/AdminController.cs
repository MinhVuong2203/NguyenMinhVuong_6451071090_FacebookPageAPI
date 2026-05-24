using BackendAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendAPI.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly IdempotencyStore _idempotencyStore;

        public AdminController(IdempotencyStore idempotencyStore)
        {
            _idempotencyStore = idempotencyStore;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var database = await _idempotencyStore.GetSnapshotAsync();

            return Ok(new
            {
                service = "backend-api",
                port = 3000,
                consumes = new[] { "reply_commands", "send_retry" },
                publishes = new[] { "send_failed" },
                sentCount = database.SentCommands.Count,
                failedCount = database.FailedCommands.Count
            });
        }

        [HttpGet("idempotency")]
        public async Task<IActionResult> GetIdempotencyKeys()
        {
            var database = await _idempotencyStore.GetSnapshotAsync();
            return Ok(database.SentCommands.Values.OrderByDescending(record => record.SentAt));
        }

        [HttpGet("failures")]
        public async Task<IActionResult> GetFailures()
        {
            var database = await _idempotencyStore.GetSnapshotAsync();
            return Ok(database.FailedCommands.OrderByDescending(record => record.FailedAt));
        }

        [HttpGet("idempotency/{key}")]
        public async Task<IActionResult> GetIdempotencyKey(string key)
        {
            var database = await _idempotencyStore.GetSnapshotAsync();

            if (!database.SentCommands.TryGetValue(key, out var record))
            {
                return NotFound(new { message = "Idempotency key not found." });
            }

            return Ok(record);
        }
    }
}
