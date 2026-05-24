using BackendAPI.Models;
using MySqlConnector;

namespace BackendAPI.Services
{
    public class IdempotencyStore
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private bool _schemaReady;

        public IdempotencyStore(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("MySql")
                ?? throw new InvalidOperationException("Connection string 'MySql' is missing.");
        }

        public async Task<bool> HasProcessedAsync(string idempotencyKey)
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand("""
                SELECT EXISTS(
                    SELECT 1
                    FROM sent_commands
                    WHERE idempotency_key = @idempotencyKey
                );
                """, connection);
            command.Parameters.AddWithValue("@idempotencyKey", idempotencyKey);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) == 1;
        }

        public async Task MarkProcessedAsync(string idempotencyKey, BackendCommand command, string facebookResponse)
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var mysqlCommand = new MySqlCommand("""
                INSERT INTO sent_commands
                    (idempotency_key, command_id, command_type, event_id, user_id, sent_at, facebook_response)
                VALUES
                    (@idempotencyKey, @commandId, @commandType, @eventId, @userId, @sentAt, @facebookResponse)
                ON DUPLICATE KEY UPDATE
                    command_id = VALUES(command_id),
                    command_type = VALUES(command_type),
                    event_id = VALUES(event_id),
                    user_id = VALUES(user_id),
                    facebook_response = VALUES(facebook_response);
                """, connection);

            mysqlCommand.Parameters.AddWithValue("@idempotencyKey", idempotencyKey);
            mysqlCommand.Parameters.AddWithValue("@commandId", command.CommandId);
            mysqlCommand.Parameters.AddWithValue("@commandType", command.CommandType);
            mysqlCommand.Parameters.AddWithValue("@eventId", (object?)command.EventId ?? DBNull.Value);
            mysqlCommand.Parameters.AddWithValue("@userId", (object?)command.UserId ?? DBNull.Value);
            mysqlCommand.Parameters.AddWithValue("@sentAt", DateTime.UtcNow);
            mysqlCommand.Parameters.AddWithValue("@facebookResponse", facebookResponse);

            await mysqlCommand.ExecuteNonQueryAsync();
        }

        public async Task RecordFailureAsync(SendFailedEvent failedEvent)
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand("""
                INSERT INTO failed_commands
                    (failure_id, idempotency_key, command_id, command_type, error_message, failed_at, retry_count)
                VALUES
                    (@failureId, @idempotencyKey, @commandId, @commandType, @errorMessage, @failedAt, @retryCount)
                ON DUPLICATE KEY UPDATE
                    error_message = VALUES(error_message),
                    failed_at = VALUES(failed_at),
                    retry_count = VALUES(retry_count);
                """, connection);

            command.Parameters.AddWithValue("@failureId", failedEvent.FailureId);
            command.Parameters.AddWithValue("@idempotencyKey", string.IsNullOrWhiteSpace(failedEvent.Command.IdempotencyKey)
                ? DBNull.Value
                : failedEvent.Command.IdempotencyKey);
            command.Parameters.AddWithValue("@commandId", failedEvent.Command.CommandId);
            command.Parameters.AddWithValue("@commandType", failedEvent.Command.CommandType);
            command.Parameters.AddWithValue("@errorMessage", failedEvent.ErrorMessage);
            command.Parameters.AddWithValue("@failedAt", failedEvent.FailedAt);
            command.Parameters.AddWithValue("@retryCount", failedEvent.Command.RetryCount);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<IdempotencyDatabase> GetSnapshotAsync()
        {
            await EnsureSchemaAsync();

            var database = new IdempotencyDatabase();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using (var command = new MySqlCommand("""
                SELECT idempotency_key, command_id, command_type, event_id, user_id, sent_at, facebook_response
                FROM sent_commands
                ORDER BY sent_at DESC;
                """, connection))
            {
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var idempotencyKeyOrdinal = reader.GetOrdinal("idempotency_key");
                    var commandIdOrdinal = reader.GetOrdinal("command_id");
                    var commandTypeOrdinal = reader.GetOrdinal("command_type");
                    var eventIdOrdinal = reader.GetOrdinal("event_id");
                    var userIdOrdinal = reader.GetOrdinal("user_id");
                    var sentAtOrdinal = reader.GetOrdinal("sent_at");
                    var facebookResponseOrdinal = reader.GetOrdinal("facebook_response");

                    var record = new SentCommandRecord
                    {
                        IdempotencyKey = reader.GetString(idempotencyKeyOrdinal),
                        CommandId = reader.GetString(commandIdOrdinal),
                        CommandType = reader.GetString(commandTypeOrdinal),
                        EventId = reader.IsDBNull(eventIdOrdinal) ? null : reader.GetString(eventIdOrdinal),
                        UserId = reader.IsDBNull(userIdOrdinal) ? null : reader.GetString(userIdOrdinal),
                        SentAt = reader.GetDateTime(sentAtOrdinal),
                        FacebookResponse = reader.IsDBNull(facebookResponseOrdinal)
                            ? string.Empty
                            : reader.GetString(facebookResponseOrdinal)
                    };

                    database.SentCommands[record.IdempotencyKey] = record;
                }
            }

            await using (var command = new MySqlCommand("""
                SELECT failure_id, idempotency_key, command_id, command_type, error_message, failed_at, retry_count
                FROM failed_commands
                ORDER BY failed_at DESC;
                """, connection))
            {
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var failureIdOrdinal = reader.GetOrdinal("failure_id");
                    var idempotencyKeyOrdinal = reader.GetOrdinal("idempotency_key");
                    var commandIdOrdinal = reader.GetOrdinal("command_id");
                    var commandTypeOrdinal = reader.GetOrdinal("command_type");
                    var errorMessageOrdinal = reader.GetOrdinal("error_message");
                    var failedAtOrdinal = reader.GetOrdinal("failed_at");
                    var retryCountOrdinal = reader.GetOrdinal("retry_count");

                    database.FailedCommands.Add(new FailedCommandRecord
                    {
                        FailureId = reader.GetString(failureIdOrdinal),
                        IdempotencyKey = reader.IsDBNull(idempotencyKeyOrdinal)
                            ? string.Empty
                            : reader.GetString(idempotencyKeyOrdinal),
                        CommandId = reader.GetString(commandIdOrdinal),
                        CommandType = reader.GetString(commandTypeOrdinal),
                        ErrorMessage = reader.GetString(errorMessageOrdinal),
                        FailedAt = reader.GetDateTime(failedAtOrdinal),
                        RetryCount = reader.GetInt32(retryCountOrdinal)
                    });
                }
            }

            return database;
        }

        private async Task EnsureSchemaAsync()
        {
            if (_schemaReady)
            {
                return;
            }

            await _schemaLock.WaitAsync();
            try
            {
                if (_schemaReady)
                {
                    return;
                }

                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var createSentCommands = new MySqlCommand("""
                    CREATE TABLE IF NOT EXISTS sent_commands (
                        idempotency_key VARCHAR(255) NOT NULL PRIMARY KEY,
                        command_id VARCHAR(100) NOT NULL,
                        command_type VARCHAR(50) NOT NULL,
                        event_id VARCHAR(255) NULL,
                        user_id VARCHAR(255) NULL,
                        sent_at DATETIME(6) NOT NULL,
                        facebook_response TEXT NULL
                    );
                    """, connection);
                await createSentCommands.ExecuteNonQueryAsync();

                await using var createFailedCommands = new MySqlCommand("""
                    CREATE TABLE IF NOT EXISTS failed_commands (
                        id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        failure_id VARCHAR(100) NOT NULL UNIQUE,
                        idempotency_key VARCHAR(255) NULL,
                        command_id VARCHAR(100) NOT NULL,
                        command_type VARCHAR(50) NOT NULL,
                        error_message TEXT NOT NULL,
                        failed_at DATETIME(6) NOT NULL,
                        retry_count INT NOT NULL DEFAULT 0,
                        INDEX ix_failed_commands_failed_at (failed_at),
                        INDEX ix_failed_commands_idempotency_key (idempotency_key)
                    );
                    """, connection);
                await createFailedCommands.ExecuteNonQueryAsync();
                _schemaReady = true;
            }
            finally
            {
                _schemaLock.Release();
            }
        }
    }
}
