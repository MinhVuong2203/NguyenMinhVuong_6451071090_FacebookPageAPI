using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using RetryService.Services;

namespace RetryService.Controllers
{
    [ApiController]
    [Route("metrics")]
    public class MetricsController : ControllerBase
    {
        private readonly RetryStateService _state;

        public MetricsController(RetryStateService state)
        {
            _state = state;
        }

        [HttpGet]
        public IActionResult GetMetrics()
        {
            var snapshot = _state.GetMetricsSnapshot();
            var lastProcessedUnixSeconds = snapshot.LastProcessedAt.HasValue
                ? new DateTimeOffset(snapshot.LastProcessedAt.Value).ToUnixTimeSeconds()
                : 0;

            var metrics = new StringBuilder();

            metrics.AppendLine("# HELP facepage_retry_send_failed_consumed_total Total send_failed messages consumed by retry-service.");
            metrics.AppendLine("# TYPE facepage_retry_send_failed_consumed_total counter");
            metrics.AppendLine($"facepage_retry_send_failed_consumed_total {snapshot.ConsumedFailedCount.ToString(CultureInfo.InvariantCulture)}");

            metrics.AppendLine("# HELP facepage_retry_messages_retried_total Total failed commands republished to send_retry.");
            metrics.AppendLine("# TYPE facepage_retry_messages_retried_total counter");
            metrics.AppendLine($"facepage_retry_messages_retried_total {snapshot.RetriedCount.ToString(CultureInfo.InvariantCulture)}");

            metrics.AppendLine("# HELP facepage_dead_letter_messages_total Total failed commands published to the dead_letter Kafka topic.");
            metrics.AppendLine("# TYPE facepage_dead_letter_messages_total counter");
            metrics.AppendLine($"facepage_dead_letter_messages_total {snapshot.DeadLetterCount.ToString(CultureInfo.InvariantCulture)}");

            metrics.AppendLine("# HELP facepage_retry_last_processed_timestamp_seconds Unix timestamp of the last retry-service action.");
            metrics.AppendLine("# TYPE facepage_retry_last_processed_timestamp_seconds gauge");
            metrics.AppendLine($"facepage_retry_last_processed_timestamp_seconds {lastProcessedUnixSeconds.ToString(CultureInfo.InvariantCulture)}");

            metrics.AppendLine("# HELP facepage_dead_letter_alert_active Indicates whether at least one message has reached the dead_letter topic since retry-service started.");
            metrics.AppendLine("# TYPE facepage_dead_letter_alert_active gauge");
            metrics.AppendLine($"facepage_dead_letter_alert_active {(snapshot.DeadLetterCount > 0 ? 1 : 0)}");

            return Content(metrics.ToString(), "text/plain; version=0.0.4; charset=utf-8");
        }
    }
}
