# Panoptes

> P社让你当上帝，我们让你当人。

回合制联机城建对战游戏。玩家扮演信息残缺的君主，通过 AI 部长感知世界、执行意志，在不完整信息下做不可逆决策。

Gamejam 参赛作品，主题：**我的 AI 队友**。

---

## 核心玩法

- **AI 部长系统**：玩家不直接操控游戏世界，而是发布宏观政策，由 AI 部长自主执行。部长有性格、私心、记忆，会犯错、会互相冲突。
- **信息不对称**：玩家感知世界的唯一方式是部长汇报，汇报经过延迟、过滤、失真处理。
- **控制网络**：战争本质是破坏对方的基础设施拓扑，切断生产管道是核心战术。
- **亲政令牌**：每回合有限次数的亲自干预配额，其余交由 AI 执行。

---

## 快速开始

### 环境要求

```
Go 1.26+
Unity 6000.4.1f1
buf（Protobuf 代码生成）
Redis
PostgreSQL
```

### 一键生成协议代码

```bash
make gen
```

生成结果：
- `server/internal/gen/proto/` → Go 代码
- `client/Assets/Scripts/Protocol/` → Unity C# 代码

### 一键生成静态数据

```bash
make data-gen
```

数据加载链路：
- 作者源只维护在根 `data/registry`、`data/content`、`data/ui`
- `make data-gen` 将其编译为 `data/generated/server/` 和 `client/Assets/Resources/Data/`
- 服务端启动时从 `DATA_ROOT/generated/server` 加载静态目录到 `staticdata.Default()`
- 客户端启动时从 `Resources/Data/` 加载本地静态目录，并用服务端下发的 `MsgStaticCatalogManifest` 做版本握手

### 启动服务端

```bash
# 复制并填写配置
cp server/.env.example server/.env

# 启动
make server
```

服务端默认监听：
- HTTP API：`http://localhost:8080/api`
- WebSocket：`ws://localhost:8080/ws`

### 启动客户端

用 Unity Hub 打开 `client/` 目录，运行 Boot 场景。

---

## 项目结构

```
panoptes/
├── protocol/          # Proto 定义（单一数据源）
├── server/            # Go 服务端
│   ├── cmd/server/    # 启动入口
│   ├── internal/      # 内部实现
│   └── cmd/datagen/   # 静态数据生成入口
├── data/              # 静态数据作者源与生成产物
└── client/            # Unity 客户端
    └── Assets/
        ├── Scripts/
        ├── Scenes/
        └── Resources/Data/ # 生成的静态目录 bundle
```

---

## 技术架构

### 服务端

```
Go 1.26 · WebSocket · ECS（donburi）· Redis · PostgreSQL · Qwen/DeepSeek 兼容 LLM · Protobuf
```

- **ECS + Data-Driven**：游戏状态用 ECS 管理，所有静态数据从根 `data/` 作者源生成
- **Event Sourcing**：Engine 层纯函数产生事件，统一 Apply 修改状态
- **AI 部长**：服务端可异步调用 LLM API 生成部长汇报与草案文案；当前 MVP 不让部长 action 直接写入游戏状态
- **Transport 抽象**：WebSocket 现在，gRPC 将来，业务代码零修改

### 客户端

```
Unity 6000.4.1f1 · URP · NativeWebSocket · Google.Protobuf · uGUI
```

- **纯展示层**：不包含任何游戏逻辑，所有状态以服务端为准
- **Protobuf + protojson**：消息统一使用 `ClientFrame` / `ServerFrame`，明文 JSON 传输，方便调试

---

## 游戏机制

### 回合结构

```
规划阶段 planning（35秒）→ 统一结算 resolving → 下一回合
```

### 兵种

| 兵种 | 特攻 | 移动力 |
|---|---|---|
| 步兵 | 主战场均衡 | 2格/回合 |
| 弓手 | 远程压制（射程2格） | 1格/回合 |
| 开拓者 | 建城与扩张 | 4格/回合 |

> 注：骑兵、攻城兵、破坏兵属于后续内容扩展，当前 MVP 内容包尚未定版。

### 胜负条件

攻破对方主城（血量归零）判胜。最多 30 回合，超时判平局。

### AI 部长

| 职位 | 职责 |
|---|---|
| 内政部长 | 研究/国策/建设建议、局势汇报 |
| 军事部长（后续） | 战区指令拆解、前线汇报、自主调兵 |
| 外交部长（加分项） | 敌方情报分析、信息干扰 |

---

## 开发指南

### 对于 AI Agent

开始任何任务前必须阅读 `AGENTS.md`。

### 对于人类开发者

| 文档 | 说明 |
|---|---|
| `AGENTS.md` | Agent 行为准则，也是架构决策记录 |
| `docs/PANOPTES_AGENT_BACKEND.md` | 服务端完整开发指南 |
| `docs/PANOPTES_AGENT_FRONTEND.md` | 客户端完整开发指南 |
| `docs/HTTP_DESIGN.md` | HTTP API 设计规范 |

### 修改协议

只改 `protocol/panoptes/proto/v1/*.proto`，然后 `make gen`，不要手动修改生成代码。

### 修改游戏数值

只改根 `data/registry`、`data/content`、`data/ui`，不改业务代码。

---

## 分工

| 职责 | 内容 |
|---|---|
| 服务端架构 | 游戏状态机、战斗引擎、LLM 集成、WebSocket 通信 |
| Unity 客户端 | 地图渲染、UI 系统、网络层、动画 |
| 策划 & 辅助 | 数值设计、地图设计、部长角色池、测试 |

---

## MVP 范围

**必做**

- 2人联机对战
- planning/resolving 统一回合
- 内政部长建议与汇报（规则基线，LLM 可选润色）
- 资源、点数、科技、建筑、配方基础闭环
- 最小单位闭环（开拓者/步兵/弓手）
- 攻占主城胜利判定

**加分项**

- 外交部长
- 地图程序生成
- 部长记忆系统
- 关键节点自动命名
- 胜负叙事收尾

---

## 环境变量

```bash
# 服务器
PORT=8080

# Redis
REDIS_ADDR=localhost:6379

# PostgreSQL（必选）
POSTGRES_DSN=postgres://user:pass@localhost/panoptes

# LLM（可选）
MINISTER_LLM_ENABLED=false
MINISTER_LLM_PROVIDER=qwen
QWEN_API_KEY=
DEEPSEEK_API_KEY=
MINISTER_LLM_MODEL=
MINISTER_LLM_TIMEOUT_MS=5000

# JWT
JWT_SECRET=your-secret-key
JWT_EXPIRATION=86400

# 大厅配置
DEFAULT_MAX_PLAYERS=2
```

---

*项目代号：Panoptes | Gamejam 参赛作品 | 2026*
