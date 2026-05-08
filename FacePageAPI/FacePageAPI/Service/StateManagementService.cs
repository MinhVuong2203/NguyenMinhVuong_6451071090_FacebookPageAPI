using FacePageAPI.Model;
using System.Collections.Concurrent;

namespace FacePageAPI.Service
{
    public class StateManagementService
    {
        private readonly ILogger<StateManagementService> _logger;

        // In-memory state tracking (can be replaced with Redis for production)
        private readonly ConcurrentDictionary<string, ProcessedEvent> _eventStates;

        public StateManagementService(ILogger<StateManagementService> logger)
        {
            _logger = logger;
            _eventStates = new ConcurrentDictionary<string, ProcessedEvent>();
        }

        public void UpdateState(ProcessedEvent evt)
        {
            _eventStates.AddOrUpdate(evt.EventId, evt, (key, old) => evt);
            _logger.LogInformation($"State updated for event {evt.EventId}: {evt.State}");
        }

        public ProcessedEvent? GetEventState(string eventId)
        {
            _eventStates.TryGetValue(eventId, out var evt);
            return evt;
        }

        public EventState? GetState(string eventId)
        {
            if (_eventStates.TryGetValue(eventId, out var evt))
            {
                return evt.State;
            }
            return null;
        }

        public List<ProcessedEvent> GetEventsByState(EventState state)
        {
            return _eventStates.Values.Where(e => e.State == state).ToList();
        }

        public List<ProcessedEvent> GetFailedEvents()
        {
            return GetEventsByState(EventState.Failed);
        }

        public int GetEventCount(EventState state)
        {
            return _eventStates.Values.Count(e => e.State == state);
        }

        public Dictionary<EventState, int> GetStateSummary()
        {
            return _eventStates.Values
                .GroupBy(e => e.State)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        // Cleanup old events (older than 7 days)
        public void CleanupOldEvents()
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-7);
            var oldEvents = _eventStates.Values
                .Where(e => e.CreatedTime < cutoffDate)
                .Select(e => e.EventId)
                .ToList();

            foreach (var eventId in oldEvents)
            {
                _eventStates.TryRemove(eventId, out _);
            }

            _logger.LogInformation($"Cleaned up {oldEvents.Count} old events");
        }

        // Get statistics
        public string GetStatistics()
        {
            var summary = GetStateSummary();
            var stats = string.Join(", ", summary.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            return $"[STATS] {stats}";
        }
    }
}
