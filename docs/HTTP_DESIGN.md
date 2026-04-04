# Panoptes — HTTP API 设计文档

> 本文档描述 Panoptes 服务端的 HTTP API 设计规范。
> 覆盖范围：认证系统（注册/登录）、健康检查。
> WebSocket 游戏通信不在本文档范围内，见 AGENT_BACKEND.md。

---

## 目录

1. 总体设计原则
2. 目录结构
3. 请求与响应规范
4. 错误处理规范
5. 认证规范（JWT）
6. API 端点定义
7. 依赖注入规范
8. 如何新增一个 API 端点
9. 测试规范

---

## 1. 总体设计原则

**不引入任何 HTTP 框架。** 使用 Go 1.22 标准库 `net/http`，其内置的 `ServeMux` 已支持：

```
Method 路由    "POST /api/login"
路径参数       "/api/users/{username}"  → r.PathValue("username")
```

满足这两个需求后，不需要 Gin/Echo/chi。

**Handler 只做三件事：**

```
1. 解析请求体（Decode）
2. 调用 Service
3. 序列化响应（writeJSON / writeError）
```

任何业务逻辑（密码校验、token 生成、数据库操作）都不在 Handler 里，一律放在 Service 层。

**依赖通过构造函数注入，不在 Handler 内部 new 任何对象。**

---

## 2. 目录结构

```
transport/http/
├── server.go      # Server 结构体，路由注册，NewServer 构造函数
├── handler.go     # 所有 Handler 方法（按 Handler struct 组织）
└── helpers.go     # writeJSON / writeError 两个私有函数
```

**命名规范：**

```
Handler struct 命名    →  {Domain}Handler（如 AuthHandler）
构造函数命名           →  New{Domain}Handler
Handler 方法命名       →  动词+名词（如 Register、Login、GetUser）
路由注册               →  在 server.go 的 registerRoutes() 中统一注册
```

---

## 3. 请求与响应规范

### 请求

Content-Type 统一为 `application/json`。

请求体用私有 struct 定义，只在 handler.go 内使用：

```go
type registerRequest struct {
    Username string `json:"username"`
    Password string `json:"password"`
}
```

解析失败统一返回 `400 invalid_request`，不暴露具体错误原因给客户端。

### 响应

响应体统一为 JSON，使用 `writeJSON` 函数：

```go
// helpers.go
func writeJSON(w http.ResponseWriter, status int, data any) {
    w.Header().Set("Content-Type", "application/json")
    w.WriteHeader(status)
    json.NewEncoder(w).Encode(data)
}
```

成功响应直接返回业务数据，不做额外包装：

```json
// 正确
{ "token": "...", "player_id": "..." }

// 错误（不做这种包装）
{ "success": true, "data": { "token": "..." } }
```

---

## 4. 错误处理规范

### 错误响应格式

所有错误响应统一格式：

```json
{ "error": "error_code" }
```

error_code 用下划线命名，客户端根据 error_code 决定显示什么文字，不依赖服务端的 message 字段。

```go
// helpers.go
func writeError(w http.ResponseWriter, status int, errorCode string) {
    writeJSON(w, status, map[string]string{"error": errorCode})
}
```

### 错误码列表（完整，不得在代码中使用列表外的错误码）

| error_code | HTTP Status | 含义 |
|---|---|---|
| `invalid_request` | 400 | 请求体解析失败或参数缺失 |
| `invalid_credentials` | 401 | 用户名或密码错误 |
| `unauthorized` | 401 | 未携带 Token 或 Token 无效 |
| `user_not_found` | 404 | 用户不存在 |
| `user_exists` | 409 | 用户名已被占用 |
| `internal_error` | 500 | 服务端内部错误 |

### 错误处理原则

Service 返回的 error 在 Handler 中做 switch 转换为对应的 error_code，不把内部错误信息直接返回给客户端：

```go
user, err := h.authSvc.Login(r.Context(), req.Username, req.Password)
if err != nil {
    switch err {
    case auth.ErrUserNotFound, auth.ErrInvalidCredentials:
        // 两种错误统一返回同一个 code，防止用户枚举攻击
        writeError(w, http.StatusUnauthorized, "invalid_credentials")
    default:
        // 内部错误不暴露细节
        writeError(w, http.StatusInternalServerError, "internal_error")
    }
    return
}
```

---

## 5. 认证规范（JWT）

### Token 格式

使用 `github.com/golang-jwt/jwt/v5`，HS256 签名。

Payload：

```json
{
    "player_id": "uuid",
    "username": "string",
    "exp": "unix timestamp"
}
```

过期时间：24小时。Secret 从环境变量 `JWT_SECRET` 读取，不硬编码。

### 密码存储

使用 `golang.org/x/crypto/bcrypt`，cost = `bcrypt.DefaultCost`（10）。

```go
// 注册时
hash, err := bcrypt.GenerateFromPassword([]byte(password), bcrypt.DefaultCost)

// 登录时
err := bcrypt.CompareHashAndPassword([]byte(user.PasswordHash), []byte(password))
```

明文密码绝对不存储，不打日志，不传递到 Service 层之外。

### WebSocket 鉴权

WebSocket 握手时在 URL Query 参数中携带 Token：

```
ws://host/ws?token=<jwt>
```

`transport/http/middleware.go` 中的 `AuthMiddleware` 校验 Token，校验通过后将 `player_id` 注入 Context：

```go
// 后续 handler 从 context 取 player_id
playerID := r.Context().Value(PlayerIDKey).(string)
```

---

## 6. API 端点定义

### POST /api/register

注册新用户。

**请求体：**

```json
{
    "username": "string, 必填, 3-20字符",
    "password": "string, 必填, 最少8字符"
}
```

**成功响应 201：**

```json
{
    "player_id": "uuid",
    "username": "string"
}
```

**失败响应：**

| 情况 | Status | error_code |
|---|---|---|
| 请求体格式错误 | 400 | `invalid_request` |
| 用户名已存在 | 409 | `user_exists` |
| 服务端错误 | 500 | `internal_error` |

---

### POST /api/login

用户登录，返回 JWT Token。

**请求体：**

```json
{
    "username": "string, 必填",
    "password": "string, 必填"
}
```

**成功响应 200：**

```json
{
    "token": "jwt string",
    "player_id": "uuid",
    "username": "string"
}
```

**失败响应：**

| 情况 | Status | error_code |
|---|---|---|
| 请求体格式错误 | 400 | `invalid_request` |
| 用户名或密码错误 | 401 | `invalid_credentials` |
| 服务端错误 | 500 | `internal_error` |

注意：用户不存在和密码错误统一返回 `invalid_credentials`，不区分，防止用户枚举攻击。

---

### GET /api/users/{username}

查询用户信息。需要携带有效 JWT Token。

**路径参数：**

```
username    string    要查询的用户名
```

**成功响应 200：**

```json
{
    "player_id": "uuid",
    "username": "string"
}
```

**失败响应：**

| 情况 | Status | error_code |
|---|---|---|
| 未携带 Token | 401 | `unauthorized` |
| 用户不存在 | 404 | `user_not_found` |
| 服务端错误 | 500 | `internal_error` |

---

### GET /health

健康检查，不需要鉴权。用于监控和部署就绪检测。

**成功响应 200：**

```json
{
    "status": "ok",
    "postgres": { "connected": true },
    "redis": { "connected": true }
}
```

**降级响应 503（任意依赖不可用时）：**

```json
{
    "status": "degraded",
    "postgres": { "connected": false, "error": "connection refused" },
    "redis": { "connected": true }
}
```

Health check 通过 `Pinger` interface 检查依赖，不直接依赖具体实现：

```go
type Pinger interface {
    Ping(ctx context.Context) error
}

func NewServer(authSvc *auth.Service, dbPinger Pinger, redisPinger Pinger) *Server
```

---

## 7. 依赖注入规范

### 组装方式

所有依赖在 `main.go` 中组装，从底层到上层：

```go
// main.go
func main() {
    cfg := config.Load()

    // 存储层
    pgDB := postgres.NewDB(cfg.PostgresDSN)
    redisClient := redis.NewClient(cfg.RedisAddr)

    // Store（interface 实现）
    userStore := postgres.NewUserStore(pgDB)

    // Service
    authSvc := auth.NewService(userStore, cfg.JWTSecret)

    // Transport
    httpServer := httpTransport.NewServer(authSvc, pgDB, redisClient)
    wsHub := websocket.NewHub()

    // 启动
    mux := http.NewServeMux()
    mux.Handle("/api/", httpServer.Handler())
    mux.HandleFunc("/ws", wsHub.HandleWS)
    http.ListenAndServe(cfg.Port, mux)
}
```

### Service 依赖 interface，不依赖具体实现

```go
// auth/service.go
type Service struct {
    userStore store.UserStore  // interface，不是 *postgres.UserStore
    jwtSecret string
}

// store/interface.go
type UserStore interface {
    Create(ctx context.Context, user *auth.User) error
    GetByUsername(ctx context.Context, username string) (*auth.User, error)
    GetByID(ctx context.Context, id string) (*auth.User, error)
}
```

---

## 8. 如何新增一个 API 端点

以新增 `GET /api/profile`（获取当前登录用户信息）为例，完整步骤：

**Step 1：在 handler.go 里添加 handler 方法**

```go
func (h *AuthHandler) GetProfile(w http.ResponseWriter, r *http.Request) {
    // 从 context 取 player_id（由 AuthMiddleware 注入）
    playerID, ok := r.Context().Value(PlayerIDKey).(string)
    if !ok {
        writeError(w, http.StatusUnauthorized, "unauthorized")
        return
    }

    user, err := h.authSvc.GetUserByID(r.Context(), playerID)
    if err != nil {
        writeError(w, http.StatusInternalServerError, "internal_error")
        return
    }

    writeJSON(w, http.StatusOK, user)
}
```

**Step 2：在 server.go 的 registerRoutes() 里注册路由**

```go
func (s *Server) registerRoutes() {
    s.mux.HandleFunc("POST /api/register", s.authHandler.Register)
    s.mux.HandleFunc("POST /api/login", s.authHandler.Login)
    s.mux.HandleFunc("GET /api/users/{username}", s.authHandler.GetUser)
    // 新增这一行，需要鉴权的路由套 AuthMiddleware
    s.mux.HandleFunc("GET /api/profile", AuthMiddleware(s.authHandler.GetProfile))
}
```

**Step 3：如果 Service 层没有对应方法，先补 Service**

```go
// auth/service.go
func (s *Service) GetUserByID(ctx context.Context, id string) (*User, error) {
    return s.userStore.GetByID(ctx, id)
}
```

**Step 4：在本文档的「API 端点定义」章节补充新端点的文档**

---

## 9. 测试规范

### Handler 测试

使用 `net/http/httptest`，mock Service 接口：

```go
func TestRegisterHandler(t *testing.T) {
    // mock Service
    mockSvc := &mockAuthService{
        registerFn: func(ctx context.Context, username, password string) (*auth.User, error) {
            return &auth.User{ID: "test-id", Username: username}, nil
        },
    }

    handler := NewAuthHandler(mockSvc)

    body := `{"username":"alice","password":"password123"}`
    req := httptest.NewRequest("POST", "/api/register", strings.NewReader(body))
    req.Header.Set("Content-Type", "application/json")
    w := httptest.NewRecorder()

    handler.Register(w, req)

    assert.Equal(t, http.StatusCreated, w.Code)
}
```

### Service 测试

使用 mock UserStore，不依赖真实数据库：

```go
func TestLoginService(t *testing.T) {
    mockStore := &mockUserStore{...}
    svc := auth.NewService(mockStore, "test-secret")

    _, token, err := svc.Login(context.Background(), "alice", "password123")
    assert.NoError(t, err)
    assert.NotEmpty(t, token)
}
```

### 不做集成测试

Gamejam 阶段不做真实数据库的集成测试，所有测试通过 mock 完成。

---

*文档版本：1.0 | 项目代号：Panoptes | 最后更新：2026-04-04*
*新增 API 端点前必须先更新本文档，以本文档为准。*