# FacePageAPI - Hệ thống quản lý Facebook Page phân tán

## 1. Tổng quan

FacePageAPI là hệ thống quản lý tương tác Facebook Page theo kiến trúc microservices. Hệ thống nhận bình luận hoặc tin nhắn từ Facebook Webhook, đưa dữ liệu vào Kafka, xử lý nội dung bằng AI và rule automation, sau đó gửi phản hồi hoặc ẩn bình luận thông qua Facebook Graph API.

Hệ thống gồm 4 service chính:

- `webhook-service`: nhận webhook từ Facebook và publish event vào Kafka.
- `core-service`: phân tích AI, sentiment, intent và áp dụng rule automation.
- `backend-api`: gọi Facebook Graph API và lưu idempotency vào MySQL.
- `retry-service`: retry command gửi Facebook thất bại và đưa lỗi cuối cùng vào `dead_letter`.

## 2. Luồng xử lý

Luồng chính:

```text
Facebook Page
-> webhook-service
-> Kafka raw_events
-> core-service
-> Kafka reply_commands
-> backend-api
-> Facebook Graph API
```

Luồng retry khi gọi Facebook thất bại:

```text
backend-api
-> Kafka send_failed
-> retry-service
-> Kafka send_retry
-> backend-api
```

Nếu retry hết số lần cho phép:

```text
retry-service
-> Kafka dead_letter
```

## 3. Cấu trúc thư mục

```text
FacePageAPI/
|-- BackendAPI/
|   |-- Controllers/
|   |-- Data/
|   |-- Models/
|   |-- Services/
|
|-- CoreService/
|   |-- Model/
|   |-- Service/
|
|-- WebhookProcessing/
|   |-- Controllers/
|   |-- Model/
|   |-- Service/
|
|-- RetryService/
|   |-- Controllers/
|   |-- Models/
|   |-- Services/
|
`-- artifacts/
```

## 4. Vai trò từng service

### 4.1. BackendAPI

Thông tin:

```text
Port: 3000
Tên service: backend-api
```

Chức năng:

- Consume Kafka topic `reply_commands` và `send_retry`.
- Kiểm tra idempotency key trước khi gửi request.
- Gọi Facebook Graph API để reply comment, hide comment hoặc block user.
- Lưu command gửi thành công vào MySQL.
- Lưu command lỗi vào MySQL và publish sang topic `send_failed`.
- Cung cấp REST API cho trang quản trị.

Endpoint chính:

```text
GET /api/admin/status
GET /api/admin/idempotency
GET /api/admin/failures
```

### 4.2. WebhookProcessing

Thông tin:

```text
Port: 3001
Tên service: webhook-service
```

Chức năng:

- Nhận webhook từ Facebook Page.
- Verify token khi cấu hình webhook.
- Verify chữ ký HMAC-SHA256 nếu có App Secret.
- Parse payload JSON từ Facebook.
- Normalize comment/message về schema nội bộ.
- Publish event vào Kafka topic `raw_events`.

Endpoint chính:

```text
GET /webhook
POST /webhook
```

### 4.3. CoreService

Thông tin:

```text
Port: 3002
Tên service: core-service
```

Chức năng:

- Consume Kafka topic `raw_events`.
- Gọi Gemini AI để phân tích nội dung.
- Phân loại intent: hỏi giá, khiếu nại, khen ngợi, tương tác thường.
- Phân tích sentiment: tích cực, trung tính, tiêu cực.
- Phát hiện spam, link, bot hoặc hành vi lặp lại.
- Áp dụng rule automation để reply, hide comment hoặc blacklist nội bộ.
- Publish command vào Kafka topic `reply_commands`.

### 4.4. RetryService

Thông tin:

```text
Port: 3003
Tên service: retry-service
```

Chức năng:

- Consume Kafka topic `send_failed`.
- Đọc `retryCount` trong message lỗi.
- Tính thời gian chờ theo exponential backoff.
- Nếu còn lượt retry, publish command vào topic `send_retry`.
- Nếu hết lượt retry, publish message vào topic `dead_letter`.

Endpoint chính:

```text
GET /api/retry/status
```

## 5. Kafka topics

| Topic            | Producer        | Consumer       | Mục đích                           |
| ---------------- | --------------- | -------------- | ---------------------------------- |
| `raw_events`     | webhook-service | core-service   | Event đã chuẩn hóa từ Facebook     |
| `reply_commands` | core-service    | backend-api    | Lệnh reply/hide/block cần thực thi |
| `send_retry`     | retry-service   | backend-api    | Lệnh cần gửi lại Facebook          |
| `send_failed`    | backend-api     | retry-service  | Lệnh gọi Facebook thất bại         |
| `dead_letter`    | retry-service   | Không bắt buộc | Message thất bại sau khi hết retry |

Tất cả giao tiếp nội bộ giữa các service đều đi qua Kafka. Các service không gọi HTTP trực tiếp lẫn nhau.

## 6. Database MySQL

BackendAPI sử dụng MySQL để lưu idempotency và lỗi gửi Facebook.

Các bảng chính:

| Bảng              | Mục đích                                                   |
| ----------------- | ---------------------------------------------------------- |
| `sent_commands`   | Lưu idempotency key của command đã gửi Facebook thành công |
| `failed_commands` | Lưu command gọi Facebook thất bại                          |

Ý nghĩa của idempotency:

```text
Cùng một command nếu bị Kafka redeliver nhiều lần
-> backend-api kiểm tra idempotency key
-> nếu đã xử lý thì bỏ qua
-> nếu chưa xử lý thì mới gọi Facebook Graph API
```

Nhờ vậy hệ thống tránh gửi cùng một reply nhiều lần.

## 7. Cách chạy hệ thống

Yêu cầu:

- .NET 8 SDK
- MySQL
- Kafka broker
- Kafka UI hoặc Kafka CLI
- Facebook Page Access Token nếu test thật với Facebook
- Ngrok nếu cần public webhook local

Build solution:

```powershell
dotnet build FacePageAPI.sln
```

Chạy 4 service ở 4 terminal riêng:

```powershell
dotnet run --project BackendAPI --launch-profile http
```

```powershell
dotnet run --project WebhookProcessing --launch-profile http
```

```powershell
dotnet run --project CoreService --launch-profile http
```

```powershell
dotnet run --project RetryService --launch-profile http
```

URL service:

```text
backend-api:       http://localhost:3000
webhook-service:   http://localhost:3001
core-service:      http://localhost:3002
retry-service:     http://localhost:3003
```

Swagger:

```text
http://localhost:3000/swagger
http://localhost:3001/swagger
http://localhost:3002/swagger
http://localhost:3003/swagger
```

## 8. Kiểm tra nhanh

Kiểm tra backend-api:

```powershell
Invoke-RestMethod http://localhost:3000/api/admin/status
```

Kiểm tra retry-service:

```powershell
Invoke-RestMethod http://localhost:3003/api/retry/status
```

Kiểm tra webhook verify token:

```powershell
Invoke-RestMethod "http://localhost:3001/webhook?hub.mode=subscribe&hub.verify_token=vuong123&hub.challenge=123456"
```

Kiểm tra MySQL:

```sql
USE facebook_page;
SHOW TABLES;
SELECT * FROM sent_commands ORDER BY sent_at DESC;
SELECT * FROM failed_commands ORDER BY failed_at DESC;
```

## 9. Test bằng Kafka UI

Test BackendAPI:

```text
Kafka UI
-> topic reply_commands
-> Produce Message
-> backend-api consume
-> gọi Facebook Graph API
-> thành công thì lưu sent_commands
-> lỗi thì publish send_failed và lưu failed_commands
```

Test RetryService:

```text
Kafka UI
-> topic send_failed
-> Produce Message
-> retry-service consume
-> nếu retryCount còn nhỏ hơn giới hạn thì publish send_retry
-> nếu retryCount đạt giới hạn thì publish dead_letter
```

Khi test riêng `retry-service`, nên tạm dừng `backend-api` để message trong `send_retry` không bị consume quá nhanh.

## 10. Tóm tắt

```text
webhook-service:
Nhận webhook từ Facebook, xác thực request, normalize event và publish raw_events.

core-service:
Consume raw_events, phân tích AI, áp dụng automation rule và publish reply_commands.

backend-api:
Consume reply_commands/send_retry, kiểm tra idempotency, gọi Facebook Graph API và publish send_failed nếu lỗi.

retry-service:
Consume send_failed, retry bằng exponential backoff, publish send_retry hoặc dead_letter.
```

Hệ thống đảm bảo luồng xử lý phân tán, giao tiếp nội bộ qua Kafka, tránh gửi reply trùng bằng idempotency và xử lý lỗi gửi Facebook bằng retry có giới hạn.
