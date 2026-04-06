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
Unity 6.4
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
- `client/Assets/Scripts/Runtime/Protocol/` → Unity C# 代码

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
│   └── data/          # 游戏数值配置
└── client/            # Unity 客户端
    └── Assets/
        ├── Scripts/
        ├── Scenes/
        └── Generated/ # 自动生成，不要手动修改
```

---

## 技术架构

### 服务端

```
Go 1.22 · WebSocket · ECS（donburi）· Redis · PostgreSQL · Anthropic API · Protobuf
```

- **ECS + Data-Driven**：游戏状态用 ECS 管理，所有静态数据从根 `data/` 作者源生成
- **Event Sourcing**：Engine 层纯函数产生事件，统一 Apply 修改状态
- **AI 部长**：服务端异步调用 LLM API，部长决策返回结构化 JSON 直接执行
- **Transport 抽象**：WebSocket 现在，gRPC 将来，业务代码零修改

### 客户端

```
Unity 2022.3 LTS · URP · NativeWebSocket · Google.Protobuf · uGUI
```

- **纯展示层**：不包含任何游戏逻辑，所有状态以服务端为准
- **Protobuf + protojson**：消息用 Envelope 包装，明文 JSON 传输，方便调试

---

## 游戏机制

### 回合结构

```
内政阶段（60秒）→ 内政结算 → 战斗阶段（60秒）→ 战斗结算 → 下一回合
```

### 兵种

| 兵种 | 特攻 | 移动力 |
|---|---|---|
| 步兵 | 主战场均衡 | 2格/回合 |
| 弓手 | 远程压制（射程2格） | 1格/回合 |
| 骑兵 | 快速机动 | 3格/回合 |
| 攻城兵 | 城堡/城墙（×3） | 1格/回合 |
| 破坏兵 | 道路/建筑（×3） | 2格/回合 |

### 胜负条件

攻破对方主城（血量归零）判胜。最多 30 回合，超时判平局。

### AI 部长

| 职位 | 职责 |
|---|---|
| 军事部长 | 战区指令拆解、前线汇报、自主调兵 |
| 农业部长 | 生产线管理、资源流动、自主建造 |
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

只改 `protocol/*.proto`，然后 `make gen`，不要手动修改生成代码。

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
- 内政+战斗两阶段完整回合
- 军事部长+农业部长（LLM驱动）
- 管道式生产线基础版
- 5种兵种（步兵/弓手/骑兵/攻城兵/破坏兵）
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

# LLM
LLM_PROVIDER=anthropic
LLM_API_KEY=sk-...
LLM_MODEL=claude-sonnet-4-20250514

# JWT
JWT_SECRET=your-secret-key
JWT_EXPIRATION=86400

# 大厅配置
DEFAULT_MAX_PLAYERS=2
```

---

*项目代号：Panoptes | Gamejam 参赛作品 | 2026*
