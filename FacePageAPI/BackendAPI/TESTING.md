# Backend API testing

## Run the service

```powershell
dotnet run --project BackendAPI --launch-profile http
```

Backend API runs on:

```text
http://localhost:3000
```

Swagger:

```text
http://localhost:3000/swagger
```

## Dashboard/admin checks

```powershell
Invoke-RestMethod http://localhost:3000/api/admin/status
Invoke-RestMethod http://localhost:3000/api/admin/idempotency
Invoke-RestMethod http://localhost:3000/api/admin/failures
```

Idempotency data is stored in MySQL using the `ConnectionStrings:MySql` value from `BackendAPI/appsettings.json`.

The service creates these tables automatically if they do not exist:

```sql
sent_commands
failed_commands
```

## Kafka success/idempotency test

Publish the same message twice to `reply_commands`.

```json
{
  "commandId": "cmd-test-001",
  "idempotencyKey": "reply_comment:COMMENT_ID_TEST",
  "eventId": "COMMENT_ID_TEST",
  "commandType": "reply_comment",
  "pageId": "PAGE_ID_TEST",
  "message": "Cam on ban da quan tam!",
  "createdAt": "2026-05-23T00:00:00Z",
  "retryCount": 0
}
```

Expected result:

1. First delivery calls Facebook Graph API.
2. On success, `reply_comment:COMMENT_ID_TEST` is saved in the MySQL `sent_commands` table.
3. Second delivery is skipped and does not call Facebook again.
4. `GET /api/admin/idempotency` shows one record for that key.

You can also verify directly in MySQL:

```sql
SELECT * FROM sent_commands ORDER BY sent_at DESC;
```

## Kafka failure test

Publish a command with an invalid `eventId`.

Expected result:

1. Facebook Graph API returns an error.
2. Backend API records the failure.
3. Backend API publishes a `send_failed` event.
4. `GET /api/admin/failures` shows the failed command.

You can also verify directly in MySQL:

```sql
SELECT * FROM failed_commands ORDER BY failed_at DESC;
```

## Retry input test

Publish a valid command to `send_retry` with the same shape as `reply_commands`.

Expected result:

1. Backend API consumes it from `send_retry`.
2. If the idempotency key already exists, it skips.
3. If the key does not exist, it sends to Facebook and stores the key after success.
