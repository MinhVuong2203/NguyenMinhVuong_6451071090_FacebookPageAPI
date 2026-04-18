# 📘 Facebook Page API - ASP.NET Core

## 📌 Giới thiệu

Dự án này xây dựng một RESTful API bằng ASP.NET Core để tương tác với Facebook Page thông qua Facebook Graph API.

API cho phép:

* Lấy thông tin Page
* Lấy danh sách bài viết
* Đăng bài viết
* Xoá bài viết
* Lấy comment, like
* Lấy thống kê (insights)

---

## 🧱 Công nghệ sử dụng

* ASP.NET Core Web API
* Facebook Graph API
* Swagger (API Testing)
* HttpClient

---

## ⚙️ Cấu hình

### 📁 appsettings.json

```json
{
  "Facebook": {
    "PageAccessToken": "YOUR_PAGE_ACCESS_TOKEN",
    "BaseUrl": "https://graph.facebook.com/v25.0"
  }
}
```

---

## 🚀 Các API đã xây dựng

### 1. Lấy thông tin Page

```http
GET /api/page/{pageId}
```

---

### 2. Lấy danh sách bài viết

```http
GET /api/page/{pageId}/posts
```

---

### 3. Đăng bài viết

```http
POST /api/page/{pageId}/posts
```

**Body:**

```json
{
  "message": "Hello từ API"
}
```

---

### 4. Xoá bài viết

```http
DELETE /api/page/post/{postId}
```

---

### 5. Lấy danh sách comment

```http
GET /api/page/post/{postId}/comments
```

---

### 6. Lấy danh sách like

```http
GET /api/page/post/{postId}/likes
```

---

### 7. Lấy thống kê Page (Insights)

```http
GET /api/page/{pageId}/insights
```

Ví dụ metric:

* `page_fans`
* `page_engaged_users`

---

## 🔑 Lấy Page Access Token

Sử dụng công cụ:

* Graph API Explorer

### Các bước:

1. Generate User Token
2. Cấp quyền:

   * pages_show_list
   * pages_read_engagement
   * pages_manage_posts
3. Gọi API:

```http
GET /me/accounts
```

4. Lấy:

* Page ID
* Page Access Token

---

## 🧪 Test API

Truy cập Swagger:

```
https://localhost:{port}/swagger
```

---

## ⚠️ Lưu ý

* Page phải là Fanpage (không phải profile)
* Token phải là Page Access Token
* Một số API (insights) cần Page có dữ liệu thực tế
* Token có thể hết hạn

---

## 🎯 Kết quả đạt được

* Xây dựng thành công RESTful API kết nối Facebook Graph API
* Thực hiện CRUD bài viết trên Facebook Page
* Hiển thị dữ liệu tương tác và thống kê

---

## 👨‍💻 Tác giả

* Nguyễn Minh Vương

---
