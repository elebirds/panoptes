# Panoptes 项目全局 Agent 指南

> 本文档是所有 AI Agent 开发 Panoptes 项目时的行为准则。
> 开始任何任务前必须完整阅读本文档。
> 本文档优先级高于任何其他文档，发生冲突时以本文档为准。

---

## 项目概述

**项目代号**：Panoptes
**类型**：回合制联机城建对战游戏
**主题**：AI 部长系统驱动的信息不对称决策体验

核心定位：P社让你当上帝，我们让你当人。玩家通过信息残缺的 AI 部长感知世界、执行意志，在不完整信息下做不可逆决策。

---

## 仓库结构

```
panoptes/                          # Monorepo 根目录
├── AGENTS.md                      # 本文件，Agent 行为准则
├── README.md                      # 项目概述
├── Makefile                       # 顶层命令
│
├── protocol/                      # Proto 定义（单一数据源）
│   ├── buf.yaml
│   ├── buf.gen.yaml
│   ├── common.proto               # Position、MsgClientRuntimeConfig
│   ├── data_types.proto           # ResourceBag、StaticCatalogManifest
│   ├── data_catalog.proto         # 静态目录消息
│   ├── map_catalog.proto          # 地图目录消息
│   ├── auth.proto
│   ├── lobby.proto
│   ├── game_state.proto
│   ├── domestic.proto
│   ├── combat.proto
│   └── minister.proto
│
├── server/                        # Go 服务端
│   ├── cmd/server/
│   │   ├── main.go
│   │   └── app/                   # App 结构体，启动装配
│   ├── internal/
│   │   ├── auth/
│   │   ├── config/
│   │   ├── observe/
│   │   ├── store/
│   │   ├── transport/
│   │   ├── algo/
│   │   ├── llm/
│   │   ├── domain/
│   │   ├── ecs/
│   │   ├── event/
│   │   ├── engine/
│   │   ├── game/
│   │   └── gen/proto/             # buf generate 生成，禁止手动修改
│   └── go.mod
│
└── client/                        # Unity 客户端
    ├── Assets/
    │   ├── Scripts/Protocol/         # buf generate 生成，禁止手动修改
    │   ├── Scripts/Runtime/Core/     # 客户端 Core 层
    │   ├── Scripts/Runtime/Presentation/ # 客户端 Presentation 层
    │   ├── Scripts/
    │   ├── Scenes/
    │   ├── Prefabs/
    │   └── Art/
    └── Packages/
```

---

## 绝对禁止（任何情况下都不得违反）

```
❌ 在 `server/internal/gen/proto/` 或 `client/Assets/Scripts/Protocol/` 下手动修改生成文件
❌ 在 engine/ 的 System 里直接修改游戏状态（必须通过 Event）
❌ 在 transport/websocket/ 里 import game 包
❌ 在 game/ 里 import transport/websocket 包
❌ 在 UI 脚本里直接调用 NetworkManager
❌ 在客户端做任何游戏逻辑计算或合法性校验
❌ 在代码中硬编码任何游戏数值（必须从根 `data/` 作者源生成）
❌ 引入文档中未列出的第三方依赖
❌ 修改已定义的 proto 消息字段名或字段编号
❌ 修改已定义的消息类型名称字符串
```

---

## 技术栈

### 服务端

| 层 | 技术 |
|---|---|
| 语言 | Go 1.22+ |
| WebSocket | github.com/gorilla/websocket |
| ECS | github.com/yohamta/donburi |
| Redis | github.com/redis/go-redis/v9 |
| PostgreSQL | github.com/jackc/pgx/v5 |
| JWT | github.com/golang-jwt/jwt/v5 |
| LLM | github.com/openai/openai-go |
| Protobuf | google.golang.org/protobuf |
| 协议生成 | buf |

### 客户端

| 层 | 技术 |
|---|---|
| 引擎 | Unity 2022.3 LTS，URP |
| WebSocket | NativeWebSocket |
| Protobuf | Google.Protobuf.dll |
| UI | uGUI + TextMeshPro |

### 禁止引入

```
服务端：Gin/Echo/Fiber、GORM、任何ORM、LangChain/LlamaIndex
客户端：DOTween、Zenject、UniRx、Photon、Mirror、任何付费插件
```

---

## 协议规范

### Transport V2 格式

所有 WebSocket 消息使用 `ClientFrame` / `ServerFrame`，全程 protojson（明文 JSON）：

```json
{
  "meta": { "requestId": "req-17" },
  "game": {
    "planning": {
      "buildStructure": {
        "nodeId": "C3",
        "buildingType": "farm"
      }
    }
  }
}
```

- 入站：`ClientFrame.meta + oneof target { auth | lobby | game }`
- 出站：`ServerFrame.meta + oneof target { auth | lobby | game | problem }`
- `request_id` 由客户端生成并随命令发送，服务端在关联响应中回传

### 修改协议的唯一方式

```bash
# 1. 修改 protocol/*.proto
# 2. 在根目录执行
make gen
# 3. 提交 `server/internal/gen/proto/` 和 `client/Assets/Scripts/Protocol/` 下的变更
```

禁止只改一端的生成代码而不改 proto 源文件。

---

## 服务端架构原则

### 依赖方向（严格遵守）

```
transport → game → engine → domain
                 ↘ ecs
                 ↘ event
algo    零业务依赖
llm     零业务依赖
config  被所有层依赖，不依赖任何层
```

### Engine System 规范

```go
// 正确：System 只读状态，返回事件
func (s *SiegeSystem) Run(world donburi.World, state *domain.GameState) []event.Event

// 错误：System 直接修改状态
func (s *SiegeSystem) Run(world donburi.World) {
    // 直接修改 ❌
}
```

所有状态修改只在 `Event.Apply()` 中发生，由 Pipeline 统一在所有 System 执行完后批量 Apply。

### System 文件规范

- 单个 System 文件不超过 150 行
- 超过必须拆分
- System 之间不互相调用，只通过事件通信
- 文件名即触发条件描述（siege.go = 处理攻城兵攻城）

### GameTransport interface

```go
// server/internal/transport/interface.go
type GameTransport interface {
    Send(playerID string, msg proto.Message) error
    Broadcast(roomID string, msg proto.Message) error
    Stream(playerID string, msgs <-chan proto.Message) error
}
```

`game` 包依赖此 interface，不依赖 `transport/websocket`。

---

## 客户端架构原则

### 客户端是纯展示层

```
✅ 展示服务端推送的状态
✅ 收集玩家输入，原样发给服务端
✅ 按服务端事件序列播放动画
❌ 不做任何游戏逻辑计算
❌ 不做任何操作合法性校验
❌ 不维护本地游戏状态（只维护展示缓存）
```

### 调用链规范

```
UI 脚本
  → Service 层（AuthService 等）
  → MessageSender.Send<T>()
  → NetworkManager.Instance.Send()
  → WebSocket

❌ UI 脚本不得直接调用 NetworkManager
```

### 消息处理规范

```csharp
// 在场景的 MonoBehaviour.Awake() 里注册
MessageDispatcher.Instance.Register<MsgGameInit>("MsgGameInit", OnGameInit);

// 在 OnDestroy() 里取消注册
MessageDispatcher.Instance.Unregister("MsgGameInit");
```

### UI 脚本职责

```
✅ 处理用户输入（点击、输入）
✅ 调用 Service 层
✅ 更新自己的显示状态
✅ 调用 AppManager.TransitionTo() 切换场景
❌ 不直接调用 NetworkManager
❌ 不包含游戏业务逻辑
❌ 不维护游戏状态
```

---

## 静态数据规范

所有静态玩法数据从根 `data/` 生成，服务端运行时只读取 `data/generated/server/`：

```go
// 正确
dmg := staticdata.Default().Rules().CastleBaseHP

// 错误
dmg := stats.Attack * 3  // 硬编码 ❌
```

修改静态数据只改 `data/registry`、`data/content`、`data/ui`，再运行 `make data-gen` / `make gen`。

---

## 错误处理规范

### 服务端错误码（完整列表，不得在代码外新增）

```
invalid_request       请求体解析失败或参数缺失
invalid_credentials   用户名或密码错误
unauthorized          未携带 Token 或 Token 无效
user_not_found        用户不存在
user_exists           用户名已被占用
internal_error        服务端内部错误
insufficient_resources 资源不足
invalid_target        目标无效
no_tokens_left        令牌已用完
building_exists       此处已有建筑
outside_safe_zone     超出安全区范围
unit_not_found        找不到该单位
invalid_directive     无效的指令
phase_mismatch        当前阶段不支持此操作
game_not_found        游戏不存在
room_full             房间已满
room_not_found        房间不存在
already_in_room       你已在房间中
auth_failed           认证失败
```

### 服务端错误响应格式

```json
{ "error": "error_code" }
```

### 客户端错误码显示映射

```
invalid_request      → "请求格式错误"
invalid_credentials  → "用户名或密码错误"
user_exists          → "用户名已被占用"
internal_error       → "服务器错误，请稍后重试"
unauthorized         → "请重新登录"
（其他）             → "未知错误"
```


---

## 参考文档

| 文档 | 说明 |
|---|---|
| `docs/PANOPTES_AGENT_BACKEND.md` | 服务端完整开发指南，含所有接口定义 |
| `docs/PANOPTES_AGENT_FRONTEND.md` | 客户端完整开发指南 |
| `docs/HTTP_DESIGN.md` | HTTP API 设计规范 |
| `protocol/*.proto` | 当前消息协议定义 |

---

## 遇到歧义时的决策原则

```
1. 查阅本文档和参考文档，文档有定义的按文档来
2. 文档没有定义的，选择最简单的实现方式
3. 不确定的架构决策，在代码里留 TODO 注释并说明问题
4. 不要为了"优雅"引入额外的抽象层
5. Gamejam 期间：能跑通 > 代码漂亮
```

---

*版本：1.0 | 项目代号：Panoptes | 最后更新：2026-04-04*
