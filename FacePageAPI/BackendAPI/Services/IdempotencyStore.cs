using BackendAPI.Models;
using System.Text.Json;

namespace BackendAPI.Services
{
    public class IdempotencyStore
    {
        private readonly string _databasePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private IdempotencyDatabase _database;

        public IdempotencyStore(IConfiguration configuration, IHostEnvironment environment)
        {
            var configuredPath = configuration["Database:Path"] ?? "Data/idempotency-store.json";
            _databasePath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            _database = LoadDatabase();
        }

        public async Task<bool> HasProcessedAsync(string idempotencyKey)
        {
            await _lock.WaitAsync();
            try
            {
                return _database.SentCommands.ContainsKey(idempotencyKey);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task MarkProcessedAsync(string idempotencyKey, BackendCommand command, string facebookResponse)
        {
            await _lock.WaitAsync();
            try
            {
                _database.SentCommands[idempotencyKey] = new SentCommandRecord
                {
                    IdempotencyKey = idempotencyKey,
                    CommandId = command.CommandId,
                    CommandType = command.CommandType,
                    EventId = command.EventId,
                    UserId = command.UserId,
                    SentAt = DateTime.UtcNow,
                    FacebookResponse = facebookResponse
                };

                await SaveDatabaseAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task RecordFailureAsync(SendFailedEvent failedEvent)
        {
            await _lock.WaitAsync();
            try
            {
                _database.FailedCommands.Add(new FailedCommandRecord
                {
                    FailureId = failedEvent.FailureId,
                    IdempotencyKey = failedEvent.Command.IdempotencyKey,
                    CommandId = failedEvent.Command.CommandId,
                    CommandType = failedEvent.Command.CommandType,
                    ErrorMessage = failedEvent.ErrorMessage,
                    FailedAt = failedEvent.FailedAt,
                    RetryCount = failedEvent.Command.RetryCount
                });

                await SaveDatabaseAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IdempotencyDatabase> GetSnapshotAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(_database, _jsonOptions);
                return JsonSerializer.Deserialize<IdempotencyDatabase>(json, _jsonOptions) ?? new IdempotencyDatabase();
            }
            finally
            {
                _lock.Release();
            }
        }

        private IdempotencyDatabase LoadDatabase()
        {
            if (!File.Exists(_databasePath))
            {
                return new IdempotencyDatabase();
            }

            var json = File.ReadAllText(_databasePath);
            return JsonSerializer.Deserialize<IdempotencyDatabase>(json, _jsonOptions) ?? new IdempotencyDatabase();
        }

        private async Task SaveDatabaseAsync()
        {
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_database, _jsonOptions);
            await File.WriteAllTextAsync(_databasePath, json);
        }
    }
}
