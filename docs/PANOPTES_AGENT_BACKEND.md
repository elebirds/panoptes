# Panoptes — 后端开发指南（Agent版）

> Turn V2 覆盖说明：凡与统一 `planning/resolving` 回合模型、Turn V2 协议、Turn V2 服务端结构冲突之处，以 `docs/TURN_V2_REFACTOR_PLAN.md` 为准。
> 本文档是后端开发的唯一权威参考。所有架构决策、接口定义、数据结构均已在此固定。
> Agent开发时以本文档为准，不得自行更改已定义的结构、命名、接口。
> 未定义的细节可自行实现，但必须符合本文档的架构原则。

---

## 目录

1. 项目概述
2. 技术栈与依赖
3. 目录结构
4. 架构原则
5. Proto定义
6. 数据结构
7. 核心接口
8. 服务说明
9. ECS组件定义
10. 事件定义
11. Engine系统定义
12. 回合状态机
13. LLM集成
14. 配置与环境变量
15. 开发顺序

---

## 1. 项目概述

**项目代号**：Panoptes
**仓库名**：`panoptes`（服务端代码位于 `server/`）
**语言**：Go 1.26
**架构**：单进程，多服务边界，WebSocket通信，ECS游戏状态，Protobuf协议

游戏类型：回合制联机城建对战。每局2人，每回合分内政和战斗两个阶段，游戏核心是AI部长系统驱动的信息不对称决策体验。

---

## 2. 技术栈与依赖

### 直接依赖（当前已引入 + 规划保留）

```
github.com/gorilla/websocket       WebSocket
github.com/redis/go-redis/v9       Redis客户端
github.com/openai/openai-go        OpenAI兼容API客户端
github.com/yohamta/donburi         ECS框架
github.com/golang-jwt/jwt/v5       JWT
github.com/xeipuuv/gojsonschema    JSON Schema校验
google.golang.org/protobuf         Protobuf运行时
github.com/jackc/pgx/v5            PostgreSQL驱动
github.com/pressly/goose/v3        数据库迁移
github.com/caarlos0/env/v11        环境变量解析
github.com/joho/godotenv           本地 .env 加载
```

### 代码生成工具（不进go.mod）

```
buf                                Protobuf代码生成
protoc-gen-go                      Go代码生成插件
protoc-gen-csharp                  C#代码生成插件
```

### 不引入的库（明确禁止）

```
❌ 任何HTTP框架（Gin/Echo/Fiber等）
❌ LangChain/LlamaIndex等Agent框架
❌ GORM或任何ORM
❌ 任何依赖注入框架
```

---

## 3. 目录结构

严格按照此结构，不得新增顶级目录，子目录可在规则内扩展。

```
panoptes/
│
├── AGENTS.md
├── Makefile
│
├── protocol/                      # Proto定义（单一数据源）
│   ├── buf.yaml
│   ├── buf.gen.yaml
│   ├── common.proto
│   ├── auth.proto
│   ├── lobby.proto
│   ├── game_state.proto
│   ├── domestic.proto
│   ├── combat.proto
│   └── minister.proto
│
├── server/
│   ├── cmd/server/
│   │   ├── main.go                # 启动入口，组装所有服务
│   │   └── app/
│   ├── go.mod
│   ├── go.sum
│   │
│   ├── db/
│   │   ├── migrations/
│   │   ├── queries/
│   │   ├── sqlc.yaml
│   │   └── internal/gen/sqlc/
│   │
│   ├── data/
│   ├── data/
│   │   ├── registry/              # 唯一作者源：注册表与投影规则
│   │   ├── content/               # 玩法内容分领域 JSON
│   │   ├── ui/                    # 展示元数据与本地化
│   │   ├── schema/                # 生成的 JSON Schema
│   │   └── generated/server/      # 服务端运行时 bundle
│   │
│   ├── internal/
│   │   ├── gen/proto/             # 自动生成，禁止手动修改
│   │   ├── config/
│   │   │   ├── config.go          # 环境变量与进程配置
│   │   │   └── env.go             # 加载 env + staticdata
│   │   ├── staticdata/            # 静态目录加载与只读查询
│   │   ├── transport/
│   │   │   ├── interface.go       # Transport interface定义
│   │   │   ├── websocket/
│   │   │   │   ├── hub.go         # 连接池管理
│   │   │   │   ├── client.go      # 单连接读写goroutine
│   │   │   │   ├── client.go      # ClientFrame解码 + Problem回包
│   │   │   │   └── transport.go   # 实现Transport interface
│   │   │   └── grpc/
│   │   │       └── .gitkeep       # 预留，暂不实现
│   │   ├── auth/
│   │   │   ├── service.go         # 注册/登录业务逻辑
│   │   │   ├── jwt.go             # Token签发/校验
│   │   │   ├── middleware.go      # WebSocket握手JWT校验
│   │   │   └── store.go           # UserStore interface + 内存实现
│   │   ├── lobby/
│   │   │   ├── service.go         # 房间创建/加入/解散
│   │   │   ├── matchmaker.go      # 匹配逻辑（Gamejam：手动房间号）
│   │   │   ├── room_manager.go    # 管理等待中的房间
│   │   │   └── store.go           # LobbyStore interface + Redis实现
│   │   ├── algo/
│   │   │   ├── pathfinding/
│   │   │   │   ├── astar.go       # A*，只依赖Grid interface
│   │   │   │   └── astar_test.go
│   │   │   ├── graph/
│   │   │   │   └── flow.go        # 最大流（资源流动用）
│   │   │   └── geometry/
│   │   │       └── distance.go    # 曼哈顿距离等工具函数
│   │   ├── llm/
│   │   │   ├── interface.go       # LLMClient interface
│   │   │   ├── anthropic.go       # Anthropic实现
│   │   │   ├── openai.go          # OpenAI兼容实现
│   │   │   └── factory.go         # 按配置创建实例
│   │   ├── domain/
│   │   │   ├── types.go           # 基础枚举和类型
│   │   │   ├── state.go           # GameState根结构
│   │   │   ├── node.go            # Node纯查询方法
│   │   │   └── unit.go            # Unit纯查询方法
│   │   ├── ecs/
│   │   │   ├── components.go      # 所有Component类型定义和注册
│   │   │   ├── factory.go         # 从config创建Entity的工厂
│   │   │   └── query.go           # 常用Query封装
│   │   ├── event/
│   │   │   ├── interface.go       # Event interface
│   │   │   ├── combat.go          # 战斗事件
│   │   │   ├── production.go      # 生产事件
│   │   │   ├── minister.go        # 部长事件
│   │   │   └── game.go            # 游戏流程事件
│   │   ├── engine/
│   │   │   ├── pipeline.go        # Pipeline：串联System，统一Apply
│   │   │   ├── combat/
│   │   │   │   ├── single_step_resolver.go # SingleStepResolver 主入口
│   │   │   │   ├── snapshot_phase.go       # 战斗快照阶段
│   │   │   │   ├── conflict_phase.go       # edge/node group conflict
│   │   │   │   ├── damage_phase.go         # 冲突/攻击伤害阶段
│   │   │   │   └── upkeep.go      # UpkeepSystem（粮食消耗）
│   │   │   ├── economy/
│   │   │   │   ├── orchestrator.go # Economy Runner + Stages
│   │   │   │   ├── build.go        # BuildSystem（建造结算）
│   │   │   │   ├── recipe.go       # 配方推进
│   │   │   │   ├── research.go     # 科研完成判定
│   │   │   │   └── validation.go   # planning/settlement 共享校验
│   │   │   └── minister/
│   │   │       ├── engine.go      # 部长调度器（异步）
│   │   │       ├── prompt.go      # 游戏状态→Prompt序列化
│   │   │       ├── parser.go      # LLM响应解析和actions执行
│   │   │       └── memory.go      # 部长记忆管理
│   │   ├── game/
│   │   │   ├── room.go            # Room：生命周期+状态机主循环
│   │   │   ├── phase/
│   │   │   │   ├── interface.go   # Phase interface
│   │   │   │   ├── domestic.go    # 内政阶段
│   │   │   │   └── combat.go      # 战斗阶段
│   │   │   └── settlement.go      # 结算：调Engine+Apply+推送
│   │   └── store/
│   │       ├── interface.go       # 所有Store interface汇总
│   │       ├── postgres/
│   │       │   └── user_store.go  # UserStore PostgreSQL实现
│   │       └── redis/
│   │           ├── game_store.go  # GameStore Redis实现
│   │           └── lobby_store.go # LobbyStore Redis实现
│
└── client/
    └── Assets/Scripts/Protocol/ # buf generate 输出的 C# 协议代码
```

### Makefile命令

```makefile
.PHONY: gen build test run

gen:
	buf generate

build:
	go build -o bin/panoptes ./main.go

test:
	go test ./...

run:
	go run main.go

lint:
	buf lint
	go vet ./...
```

### 静态数据生成与加载

当前静态数据采用“作者源”和“运行时 bundle”分离模型。

作者源目录：
- `data/registry/`：注册表、manifest、动态资源定义
- `data/content/`：单位、建筑、地形、规则、地图等玩法内容
- `data/ui/`：展示层元数据与本地化

生成命令：

```bash
make data-gen
make data-validate
```

生成结果：
- `data/generated/server/`：服务端运行时静态目录
- `client/Assets/Resources/Data/`：客户端本地静态目录
- `data/schema/`：生成的 JSON Schema
- `protocol/data_types.proto`、`protocol/data_catalog.proto`、`protocol/map_catalog.proto`

服务端运行时加载链路固定为：

```text
data/registry + data/content + data/ui
  -> server/cmd/datagen
  -> data/generated/server/
  -> config.Load()
  -> staticdata.LoadDir(DATA_ROOT/generated/server)
  -> staticdata.SetDefault(catalog)
  -> game/domain/ecs/engine 通过 staticdata.Default() 只读访问
```

禁止业务层直接读取 `data/content/*.json` 或 `data/generated/server/*.json`；统一通过 `staticdata.Default()` 查询。

对局启动时的数据流固定为：

```text
GameRoom.Start()
  -> staticdata.Default()
  -> manifest.default_map_id 或 cfg.MapID
  -> maploader.LoadMap()
  -> maploader.InitWorldFromMap()
  -> domain.NewGameState()
```

其中 `InitWorldFromMap()` 负责把 `MapRuntimeBundle` 展开为 ECS 世界，包括：
- 创建所有节点 Entity
- 应用道路、资源点、命名点
- 解析预置 owner / owner_slot
- 创建预置建筑并写入节点 owner
- 维护 `node_id -> Entity` 索引

---

## 4. 架构原则

Agent开发时必须遵守以下原则，违反视为错误实现。

### 原则一：单向依赖

```
transport → game → engine → domain
                 ↘ ecs
                 ↘ event
algo       独立，无任何业务依赖
llm        独立，只依赖标准库和SDK
config     被所有层依赖，但不依赖任何层
```

禁止反向依赖：engine不得import game，domain不得import ecs。

### 原则二：Engine是纯函数

```go
// 正确：System.Run读状态，返回事件，不修改任何数据
func (s *BuildSystem) Run(world donburi.World, state *domain.GameState) []event.Event

// 错误：System直接修改状态
func (s *BuildSystem) Run(world donburi.World) { world.Entry(...).HP -= 10 }
```

所有状态修改只在`Event.Apply()`中发生，统一在Pipeline结束后执行。

### 原则三：Transport透明

业务代码只依赖`transport.GameTransport`接口，不得import`transport/websocket`包。

### 原则四：Config是唯一数值来源

兵种攻击力、建筑消耗、地形系数等所有数值只从`staticdata.Default()`读取，禁止在代码中硬编码数值常量。

### 原则五：每个System文件不超过150行

超过150行必须拆分。System之间不互相调用，只通过事件通信。

---

## 5. Proto定义

### buf.yaml

```yaml
version: v2
modules:
  - path: .
```

### buf.gen.yaml

```yaml
version: v2
plugins:
  - plugin: go
    out: ../server/internal/gen/proto
    opt: paths=source_relative
  - plugin: csharp
    out: ../client/Assets/Scripts/Protocol
```

### common.proto

```protobuf
syntax = "proto3";
package panoptes.proto.v1;
option go_package = "github.com/elebirds/panoptes/internal/gen/proto/v1;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

message Position {
  int32 x = 1;
  int32 y = 2;
}

message Resources {
  int32 ore = 1;
  int32 wood = 2;
  int32 food = 3;
  int32 refined_ore = 4;
  int32 engineer_material = 5;
  int32 build_points = 6;
}

message ClientFrame {
  CommandMeta meta = 1;
  oneof target {
    AuthCommand auth = 10;
    LobbyCommand lobby = 11;
    GameCommand game = 12;
  }
}

message ServerFrame {
  EventMeta meta = 1;
  oneof target {
    AuthEvent auth = 10;
    LobbyEvent lobby = 11;
    GameEvent game = 12;
    Problem problem = 13;
  }
}
```

### auth.proto

```protobuf
syntax = "proto3";
package panoptes.proto.v1;
option go_package = "github.com/elebirds/panoptes/internal/gen/proto/v1;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

// 客户端→服务端
message MsgRegister {
  string username = 1;
  string password = 2;
}

message MsgLogin {
  string username = 1;
  string password = 2;
}

// 服务端→客户端
message MsgLoginSuccess {
  string token = 1;
  string player_id = 2;
  string username = 3;
}

message MsgAuthError {
  string code = 1;
  string message = 2;
}
```

### lobby.proto

```protobuf
syntax = "proto3";
package panoptes.proto.v1;
option go_package = "github.com/elebirds/panoptes/internal/gen/proto/v1;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

// 客户端→服务端
message MsgCreateRoom {
  string room_name = 1;
}

message MsgJoinRoom {
  string room_code = 1;
}

message MsgLeaveRoom {}

message MsgReadyUp {}

// 服务端→客户端
message MsgRoomCreated {
  string room_id = 1;
  string room_code = 2;  // 6位邀请码
}

message MsgRoomState {
  string room_id = 1;
  string room_code = 2;
  repeated RoomPlayer players = 3;
  string status = 4;  // "waiting|ready|starting"
}

message RoomPlayer {
  string player_id = 1;
  string username = 2;
  bool is_ready = 3;
  bool is_host = 4;
}

message MsgGameStarting {
  int32 countdown = 1;  // 秒
}

message MsgLobbyError {
  string code = 1;
  string message = 2;
}
```

### game_state.proto

```protobuf
syntax = "proto3";
package panoptes.proto.v1;
option go_package = "github.com/elebirds/panoptes/internal/gen/proto/v1;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

import "common.proto";

message NodeView {
  string id = 1;
  Position pos = 2;
  string terrain = 3;        // "plain|mountain|forest|river"
  string owner = 4;          // player_id or ""
  string building_type = 5;  // "" if none
  int32 building_hp = 6;
  int32 wall_level = 7;
  int32 my_unit_count = 8;   // 己方兵力（精确）
  int32 enemy_unit_count = 9; // 敌方兵力（可能有延迟/失真）
  bool has_road = 10;
  bool is_resource_point = 11;
  string resource_type = 12; // "ore|wood|food"
  bool is_safe_zone = 13;
}

message UnitView {
  string id = 1;
  string faction = 2;
  string unit_type = 3;
  int32 hp = 4;
  int32 max_hp = 5;
  Position pos = 6;
}

message PlayerView {
  string id = 1;
  string username = 2;
  Resources resources = 3;
  int32 tokens_left = 4;
  string current_policy = 5;
  int32 main_castle_hp = 6;
  int32 max_castle_hp = 7;
  repeated WarZone war_zones = 8;
}

message WarZone {
  string id = 1;
  string name = 2;
  repeated string node_ids = 3;
  string directive = 4;
  string target_node = 5;
}

message MinisterView {
  string role = 1;          // "military|agriculture|diplomacy"
  string name = 2;
  int32 ability = 3;        // 1-10，可见
  string personality = 4;   // 性格描述词，可见（模糊）
  // loyalty和ambition不推送给客户端
}

// 游戏初始化（仅发送一次）
message MsgGameInit {
  string game_id = 1;
  string your_player_id = 2;
  int32 turn = 3;
  string phase = 4;
  repeated NodeView nodes = 5;
  repeated UnitView units = 6;
  PlayerView my_player = 7;
  repeated MinisterView ministers = 8;
  int32 map_width = 9;
  int32 map_height = 10;
}

// 游戏结束
message MsgGameOver {
  string winner_id = 1;
  string reason = 2;     // "castle_destroyed|timeout_draw"
  string narrative = 3;  // LLM生成的本局历史总结
}
```

### domestic.proto

```protobuf
syntax = "proto3";
package panoptes.proto.v1;
option go_package = "github.com/elebirds/panoptes/internal/gen/proto/v1;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

import "common.proto";

// ===== 客户端→服务端 =====

message MsgSetPolicy {
  string policy = 1;
  // "ready_for_war|expansion|recuperation|diplomacy"
}

message MsgMinisterDirective {
  string minister_role = 1;
  string content = 2;
}

message MsgTokenBuild {
  string node_id = 1;
  string building_type = 2;
}

message MsgTokenReveal {
  string node_id = 1;
}

message MsgTokenVeto {
  string action_id = 1;
}

message MsgTokenAdjustFlow {
  string from_node = 1;
  string to_node = 2;
  string resource_type = 3;
  int32 amount = 4;
}

message MsgBuildRoad {
  string from_node = 1;
  string to_node = 2;
  // 服务端A*自动寻路
  // 若玩家指定路径点，放入waypoints
  repeated Position waypoints = 3;
}

message MsgSubmitDomestic {}

// ===== 服务端→客户端 =====

message MsgDomesticPhaseStart {
  int32 timeout = 1;
  int32 turn = 2;
  int32 tokens = 3;
  string current_policy = 4;
}

message MsgTokenResult {
  bool success = 1;
  string action = 2;
  int32 tokens_left = 3;
  string error_code = 4;   // 失败时填
}

message MsgRevealResult {
  string node_id = 1;
  NodeView true_state = 2;
  int32 tokens_left = 3;
}

message MsgMinisterAction {
  string minister = 1;
  string action_id = 2;
  repeated MinisterActionItem actions = 3;
  string report = 4;      // 叙事轨（LLM生成）
}

message MinisterActionItem {
  string type = 1;
  // "build|repair_road|move_units|redirect_flow"
  map<string, string> params = 2;
}

message MsgDomesticSettlement {
  repeated DomesticChange changes = 1;
  Resources my_resources_after = 2;
}

message DomesticChange {
  string type = 1;
  map<string, string> data = 2;
}
```

### combat.proto

```protobuf
syntax = "proto3";
package panoptes.proto.v1;
option go_package = "github.com/elebirds/panoptes/internal/gen/proto/v1;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

import "common.proto";

// ===== 客户端→服务端 =====

message MsgSetWarZone {
  string zone_id = 1;
  string name = 2;
  repeated string node_ids = 3;
}

message MsgWarZoneDirective {
  string zone_id = 1;
  string directive = 2;
  // "attack|defend|harass|flank|retreat"
  optional string target_node = 3;
}

message MsgTokenVetoCombat {
  string unit_id = 1;
}

message MsgTokenMicro {
  string unit_id = 1;
  string target_node = 2;
}

message MsgSubmitCombat {}

// ===== 服务端→客户端 =====

message MsgCombatPhaseStart {
  int32 timeout = 1;
  int32 tokens = 2;
}

// 部长战区拆解（流式推送，多条chunk）
message MsgMinisterCombatChunk {
  string chunk = 1;
  bool is_final = 2;
}

// 部长具体指令（流式结束后推送完整列表）
message MsgMinisterCombatOrders {
  repeated UnitOrder orders = 1;
}

message UnitOrder {
  string unit_id = 1;
  string action = 2;       // "move|attack|hold"
  string target_node = 3;
  string reason = 4;
}

// 战斗结算（完整事件序列）
message MsgCombatSettlement {
  repeated CombatEvent events = 1;
}

message CombatEvent {
  string type = 1;
  oneof data {
    UnitMoveEvent unit_move = 2;
    UnitDamagedEvent unit_damaged = 3;
    UnitDiedEvent unit_died = 4;
    CastleDamagedEvent castle_damaged = 5;
    CastleDestroyedEvent castle_destroyed = 6;
    ConflictEvent conflict = 7;
    RoadDestroyedEvent road_destroyed = 8;
    BuildingDamagedEvent building_damaged = 9;
  }
}

message UnitMoveEvent {
  string unit_id = 1;
  Position from = 2;
  Position to = 3;
  int32 timestamp = 4;  // 动画排序用
}

message UnitDamagedEvent {
  string unit_id = 1;
  int32 damage = 2;
  int32 hp_after = 3;
  string source = 4;  // "combat|tower|upkeep"
}

message UnitDiedEvent {
  string unit_id = 1;
  string killer_id = 2;
  Position pos = 3;
}

message CastleDamagedEvent {
  string node_id = 1;
  int32 damage = 2;
  int32 hp_after = 3;
  string attacker_id = 4;
}

message CastleDestroyedEvent {
  string node_id = 1;
  string conqueror_faction = 2;
}

message ConflictEvent {
  string unit_a_id = 1;
  string unit_b_id = 2;
  Position location = 3;
  string conflict_type = 4;  // "edge|node|chase"
}

message RoadDestroyedEvent {
  string from_node = 1;
  string to_node = 2;
  string destroyer_id = 3;
}

message BuildingDamagedEvent {
  string node_id = 1;
  int32 damage = 2;
  int32 hp_after = 3;
}
```

### minister.proto

```protobuf
syntax = "proto3";
package panoptes.proto.v1;
option go_package = "github.com/elebirds/panoptes/internal/gen/proto/v1;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

// 部长汇报（流式，多条chunk）
message MsgMinisterReportChunk {
  string minister_role = 1;
  string chunk = 2;
  bool is_final = 3;
}

// 数值轨（与叙事轨同时推送，is_final=true时一并发送）
message MsgMinisterMetrics {
  string minister_role = 1;
  repeated MetricItem metrics = 2;
}

message MetricItem {
  string label = 1;
  string value = 2;
  string trend = 3;        // "up|down|stable"
  string confidence = 4;   // "high|medium|low"
  bool is_delayed = 5;
}
```

---

## 6. 数据结构

### gamedata.json 结构

```json
{
  "$schema": "./gamedata.schema.json",
  "units": {
    "infantry": {
      "name": "步兵",
      "hp": 30,
      "attack": 10,
      "speed": 2,
      "cost": { "ore": 1, "food": 1 },
      "multipliers": { "saboteur": 1.5 },
      "can_siege": false,
      "siege_multiplier": 1.0,
      "can_destroy": false,
      "range": 1
    },
    "archer": {
      "name": "弓手",
      "hp": 20,
      "attack": 8,
      "speed": 1,
      "cost": { "wood": 1, "food": 1 },
      "multipliers": { "cavalry": 1.5 },
      "can_siege": false,
      "siege_multiplier": 1.0,
      "can_destroy": false,
      "range": 2
    },
    "cavalry": {
      "name": "骑兵",
      "hp": 25,
      "attack": 12,
      "speed": 3,
      "cost": { "refined_ore": 1, "food": 2 },
      "multipliers": { "infantry": 1.5 },
      "can_siege": false,
      "siege_multiplier": 1.0,
      "can_destroy": false,
      "range": 1,
      "road_speed_bonus": 1,
      "charge_bonus": 1.5
    },
    "siege": {
      "name": "攻城兵",
      "hp": 35,
      "attack": 5,
      "speed": 1,
      "cost": { "refined_ore": 2, "food": 1 },
      "multipliers": {},
      "can_siege": true,
      "siege_multiplier": 3.0,
      "can_destroy": false,
      "range": 1
    },
    "saboteur": {
      "name": "破坏兵",
      "hp": 20,
      "attack": 5,
      "speed": 2,
      "cost": { "ore": 1, "wood": 1 },
      "multipliers": {},
      "can_siege": false,
      "siege_multiplier": 1.0,
      "can_destroy": true,
      "destroy_multiplier": 3.0,
      "range": 1
    }
  },
  "buildings": {
    "farm": {
      "name": "农场",
      "category": "production",
      "build_cost": { "build_points": 2 },
      "production_out": { "food": 2 },
      "terrain_required": "plain_resource"
    },
    "mine": {
      "name": "矿山",
      "category": "production",
      "build_cost": { "build_points": 3 },
      "production_out": { "ore": 2 },
      "terrain_required": "mountain_resource"
    },
    "lumber": {
      "name": "伐木场",
      "category": "production",
      "build_cost": { "build_points": 2 },
      "production_out": { "wood": 2 },
      "terrain_required": "forest_resource"
    },
    "granary": {
      "name": "粮仓",
      "category": "production",
      "build_cost": { "build_points": 2, "wood": 2 },
      "food_capacity_bonus": 20
    },
    "smelter": {
      "name": "冶炼厂",
      "category": "production",
      "build_cost": { "build_points": 3, "ore": 2 },
      "production_in": { "ore": 2 },
      "production_out": { "refined_ore": 1 }
    },
    "workshop": {
      "name": "木工坊",
      "category": "production",
      "build_cost": { "build_points": 3, "wood": 2 },
      "production_in": { "wood": 2 },
      "production_out": { "engineer_material": 1 }
    },
    "barracks": {
      "name": "兵营",
      "category": "military_production",
      "build_cost": { "build_points": 3, "ore": 1 },
      "production_in": { "ore": 1, "food": 1 },
      "produces": ["infantry", "archer"]
    },
    "stable": {
      "name": "骑兵场",
      "category": "military_production",
      "build_cost": { "build_points": 4, "refined_ore": 1 },
      "production_in": { "refined_ore": 1, "food": 2 },
      "produces": ["cavalry"]
    },
    "engineer_camp": {
      "name": "工程营",
      "category": "military_production",
      "build_cost": { "build_points": 4, "engineer_material": 1 },
      "production_in": { "engineer_material": 1, "food": 1 },
      "produces": ["siege", "saboteur"]
    },
    "wall": {
      "name": "城墙",
      "category": "military",
      "build_cost": { "build_points": 3, "ore": 2 },
      "hp": 50,
      "defense_bonus_per_level": 0.1,
      "max_level": 3
    },
    "tower": {
      "name": "箭塔",
      "category": "military",
      "build_cost": { "build_points": 2, "wood": 2 },
      "upkeep": { "food": 1 },
      "attack_per_turn": 8,
      "range": 2
    },
    "watchtower": {
      "name": "烽火台",
      "category": "military",
      "build_cost": { "build_points": 1 },
      "vision_range_bonus": 3
    }
  },
  "terrain": {
    "plain": {
      "name": "平原",
      "move_cost_no_road": 2,
      "defense_bonus": 0.0,
      "attack_penalty": 0.0,
      "blocks_cavalry": false
    },
    "mountain": {
      "name": "山地",
      "move_cost_no_road": 3,
      "defense_bonus": 0.3,
      "attack_penalty": 0.2,
      "blocks_cavalry": true
    },
    "forest": {
      "name": "森林",
      "move_cost_no_road": 3,
      "defense_bonus": 0.2,
      "attack_penalty": 0.0,
      "blocks_cavalry": false
    },
    "river": {
      "name": "河流",
      "move_cost_no_road": 99,
      "defense_bonus": 0.0,
      "attack_penalty": 0.0,
      "blocks_cavalry": true,
      "passable_with_road": true
    }
  },
  "combat": {
    "wall_reduction_per_level": 0.1,
    "max_wall_reduction": 0.5,
    "tower_damage_per_tower": 8,
    "guard_bonus_per_unit": 3,
    "road_move_cost": 1
  },
  "rules": {
    "turn_time_limit_domestic": 60,
    "turn_time_limit_combat": 60,
    "tokens_per_turn": 3,
    "tokens_recuperation_bonus": 1,
    "max_turns": 30,
    "castle_base_hp": 100,
    "safe_zone_radius": 4,
    "occupy_turns": 1,
    "build_points_per_turn": 10,
    "build_points_max": 30
  },
  "ministers": {
    "pool": [
      {
        "id": "m001",
        "name": "李猛",
        "role": "military",
        "ability": 8,
        "personality": "aggressive",
        "personality_desc": "果敢激进，善于把握战机",
        "loyalty": 7,
        "ambition": 6
      },
      {
        "id": "m002",
        "name": "王守",
        "role": "military",
        "ability": 6,
        "personality": "defensive",
        "personality_desc": "老成持重，以守为攻",
        "loyalty": 9,
        "ambition": 3
      },
      {
        "id": "m003",
        "name": "陈丰",
        "role": "agriculture",
        "ability": 8,
        "personality": "conservative",
        "personality_desc": "精于算计，注重积累",
        "loyalty": 6,
        "ambition": 7
      },
      {
        "id": "m004",
        "name": "赵稔",
        "role": "agriculture",
        "ability": 7,
        "personality": "expansionist",
        "personality_desc": "热衷开拓，喜欢发展新领地",
        "loyalty": 8,
        "ambition": 5
      },
      {
        "id": "m005",
        "name": "孙谋",
        "role": "diplomacy",
        "ability": 9,
        "personality": "cunning",
        "personality_desc": "老谋深算，情报精准",
        "loyalty": 5,
        "ambition": 8
      }
    ]
  }
}
```

### maps/default.json 结构

```json
{
  "id": "default",
  "name": "中原",
  "width": 20,
  "height": 20,
  "spawn_points": [
    { "player_index": 0, "x": 2, "y": 10 },
    { "player_index": 1, "x": 17, "y": 10 }
  ],
  "nodes": [
    {
      "id": "A1",
      "x": 0, "y": 0,
      "terrain": "mountain",
      "is_resource_point": false
    },
    {
      "id": "B5",
      "x": 1, "y": 4,
      "terrain": "plain",
      "is_resource_point": true,
      "resource_type": "ore"
    }
  ],
  "central_points": ["K10", "K11"],
  "named_nodes": {
    "K10": "龙脊",
    "K11": "龙脊南"
  }
}
```

---

## 7. 核心接口

以下接口定义固定，实现可自行补充，但签名不得修改。

### transport.GameTransport

```go
// transport/interface.go
type GameTransport interface {
    Send(playerID string, msg proto.Message) error
    Broadcast(roomID string, msg proto.Message) error
    Stream(playerID string, msgs <-chan proto.Message) error
}
```

### event.Event

```go
// event/interface.go
type Event interface {
    Apply(world donburi.World, state *domain.GameState)
    ClientPayload() *pb.CombatEvent
    String() string
}
```

### engine.System

```go
// engine/pipeline.go
type System interface {
    Run(world donburi.World, state *domain.GameState) []event.Event
}
```

### game/phase.Phase

```go
// game/phase/interface.go
type Phase interface {
    Enter(room *game.Room)
    HandleMessage(room *game.Room, playerID string, msgType string, payload []byte) error
    Timeout(room *game.Room)
    Name() string
}
```

### llm.LLMClient

```go
// llm/interface.go
type CompletionRequest struct {
    System   string
    Messages []Message
    JSONMode bool
    MaxTokens int
}

type Message struct {
    Role    string
    Content string
}

type CompletionResponse struct {
    Content string
    JSON    map[string]any
}

type StreamChunk struct {
    Delta string
    Done  bool
    Err   error
}

type LLMClient interface {
    Complete(ctx context.Context, req CompletionRequest) (*CompletionResponse, error)
    Stream(ctx context.Context, req CompletionRequest) (<-chan StreamChunk, error)
}
```

### store接口

```go
// store/interface.go

type UserStore interface {
    Create(ctx context.Context, user *User) error
    GetByUsername(ctx context.Context, username string) (*User, error)
    GetByID(ctx context.Context, id string) (*User, error)
}

type GameStore interface {
    Save(ctx context.Context, gameID string, state *domain.GameState) error
    Load(ctx context.Context, gameID string) (*domain.GameState, error)
    Delete(ctx context.Context, gameID string) error
}

type LobbyStore interface {
    CreateRoom(ctx context.Context, room *lobby.Room) error
    GetRoom(ctx context.Context, roomID string) (*lobby.Room, error)
    GetRoomByCode(ctx context.Context, code string) (*lobby.Room, error)
    UpdateRoom(ctx context.Context, room *lobby.Room) error
    DeleteRoom(ctx context.Context, roomID string) error
}
```

---

## 8. 服务说明

### Auth Service

职责：注册、登录、JWT签发和校验。

Gamejam阶段使用内存UserStore实现，接口已定义，后续替换为PostgreSQL实现只需修改`main.go`的组装代码。

JWT payload包含：
```json
{ "player_id": "...", "username": "...", "exp": ... }
```

WebSocket握手时在URL参数或首条消息中传递token，middleware校验后将player_id注入连接上下文。

### Lobby Service

职责：房间管理和匹配。

房间状态：
```
waiting     → 等待玩家加入（最多2人）
ready       → 所有玩家都已ready
starting    → 倒计时3秒后创建游戏
in_game     → 游戏进行中
finished    → 游戏结束
```

Gamejam阶段：玩家创建房间获得6位邀请码，另一玩家输入邀请码加入，两人都ready后开始。不做自动匹配队列。

自动匹配（预留接口，不实现）：`matchmaker.go`中定义`Matchmaker interface`，暂时返回`ErrNotImplemented`。

### Game Service

职责：房间生命周期管理，回合状态机，结算调度。

每个游戏房间是一个独立的goroutine，通过channel接收玩家消息。

房间创建时：
1. 从`maps/default.json`加载地图
2. 按玩家选择的部长初始化Minister对象
3. 初始化ECS World，创建所有Node/Unit Entity
4. 触发Minister Engine异步生成首回合汇报
5. 推送`MsgGameInit`给双方

---

## 9. ECS组件定义

所有Component定义在`ecs/components.go`，注册变量名固定。

```go
// 位置（所有Entity都有）
type PositionComp struct { X, Y int }
var PositionC = donburi.NewComponentType[PositionComp]()

// 节点属性
type NodeComp struct {
    ID          string
    Terrain     string
    Owner       string  // player_id or ""
    HasRoad     bool
    IsResource  bool
    ResourceType string
}
var NodeC = donburi.NewComponentType[NodeComp]()

// 建筑
type BuildingComp struct {
    Type      string
    HP        int
    MaxHP     int
    WallLevel int
    Owner     string
    Towers    int
}
var BuildingC = donburi.NewComponentType[BuildingComp]()

// 兵种属性（所有单位都有）
type UnitStatsComp struct {
    ID      string
    Faction string
    Type    string
    HP      int
    MaxHP   int
    Attack  int
    Speed   int
}
var UnitStatsC = donburi.NewComponentType[UnitStatsComp]()

// 移动意图（本回合有移动指令的单位才有）
type MoveIntentComp struct {
    Target [2]int
    Path   [][2]int  // 由服务器权威路径规划填充
}
var MoveIntentC = donburi.NewComponentType[MoveIntentComp]()

// 能力型Component（按config.CanSiege等决定是否挂载）
type SiegeAbilityComp struct { Multiplier float64 }
var SiegeAbilityC = donburi.NewComponentType[SiegeAbilityComp]()

type DestroyAbilityComp struct { Multiplier float64 }
var DestroyAbilityC = donburi.NewComponentType[DestroyAbilityComp]()

type RangedAbilityComp struct { Range int }
var RangedAbilityC = donburi.NewComponentType[RangedAbilityComp]()

type ChargeAbilityComp struct { BonusMultiplier float64 }
var ChargeAbilityC = donburi.NewComponentType[ChargeAbilityComp]()

// 状态型Component（临时，可动态挂载/移除）
type PoisonEffectComp struct {
    DamagePerTurn int
    TurnsLeft     int
}
var PoisonEffectC = donburi.NewComponentType[PoisonEffectComp]()

type StarvingComp struct{ TurnsStarving int }
var StarvingC = donburi.NewComponentType[StarvingComp]()
```

---

## 10. 事件定义

所有Event实现`event.Event`接口。命名规范：`{Subject}{Verb}Event`。

### event/combat.go 包含

```
UnitMovedEvent
UnitDamagedEvent
UnitDiedEvent
CastleDamagedEvent
CastleDestroyedEvent
RoadDestroyedEvent
BuildingDamagedEvent
ConflictResolvedEvent
```

### event/production.go 包含

```
BuildingBuiltEvent
ResourceProducedEvent
ResourceFlowedEvent
RoadBuiltEvent
UnitProducedEvent
UpkeepPaidEvent
UnitStarvingEvent
```

### event/minister.go 包含

```
MinisterActedEvent      // 部长自主行动
PolicyChangedEvent      // 国策变更
MinisterDirectiveEvent  // 专项指示发出
TokenUsedEvent          // 令牌消耗
```

### event/game.go 包含

```
TurnStartedEvent
PhaseChangedEvent
GameOverEvent
PlayerDisconnectedEvent
PlayerReconnectedEvent
```

---

## 11. Engine系统定义

### 内政Pipeline（按顺序执行）

```
1. LifecycleStage           先结算建筑运行态、接管与城市陷落
2. BudgetStage              刷新 research/industry 临时预算
3. ResearchProgressStage    投入科研点数
4. ResearchCompletionStage  判定 technology_completed
5. BuildStage               处理本回合建造
6. RecipeSelectionStage     处理切配方并先落地 operation reset
7. RecipeProgressStage      推进配方、产出资源/单位
```

### 战斗Pipeline（按顺序执行）

```
1. SnapshotPhase        冻结单位/建筑/阻断快照
2. PathPlanningPhase    计算 OrderPlan
3. ConflictPhase        检测 edge conflict 与 node group conflict
4. MovementApplyPhase   按冲突结果落最终位置
5. DamagePhase          结算冲突、attack、charge 伤害
6. CleanupPhase         预留收尾挂点
7. CombatUpkeepSystem   军队粮食消耗
```

### ConflictPhase算法规范

当前主线不再使用旧 `ConflictSystem`。冲突检测由 `SingleStepResolver/ConflictPhase` 负责，规则如下：

```
边冲突：两单位在同一边上方向相反移动
        → 在边中点产生 edge conflict group
        → 双方都回到起点

节点冲突：同一候选格上存在至少两个不同阵营单位
        → 全体成员形成一个 node conflict group
        → 整组都不能占住该格，统一回退

组内伤害：对每个敌对 pair 依次结算一次冲突伤害
        → 同阵营成员不互伤
        → 结算顺序按稳定排序保证确定性
```

---

## 12. 回合状态机

```
Room.Phase枚举：
  PhaseLobby           → 等待玩家加入
  PhaseDomestic        → 内政阶段（双方同时规划）
  PhaseDomesticSettle  → 内政结算（服务端执行）
  PhaseCombat          → 战斗阶段（双方同时规划）
  PhaseCombatSettle    → 战斗结算（服务端执行）
  PhaseEnd             → 游戏结束

状态转移：
  Lobby       → Domestic      （双方都加入房间）
  Domestic    → DomesticSettle（双方提交 or 超时）
  DomesticSettle → Combat     （结算完成）
  Combat      → CombatSettle  （双方提交 or 超时）
  CombatSettle → Domestic     （结算完成，无胜负）
  CombatSettle → End          （检测到胜负）

超时处理：
  内政超时：未提交的玩家当前令牌操作作废，部长按国策全权执行
  战斗超时：未提交的玩家战区指令生效，微操窗口关闭，部长指令全部执行
```

---

## 13. LLM集成

### 部长汇报调用时机

```
回合结束后异步触发，不阻塞游戏主循环
下回合开始前汇报必须就位
超时5秒使用fallback（规则生成的简单汇报）
```

### Prompt结构规范

系统Prompt包含：
```
1. 部长角色设定（姓名、性格、职责、忠诚度影响的行为偏差）
2. 游戏状态（序列化的当前局势）
3. 历史记忆（近5回合的建议和结果）
4. 当前国策
5. 输出格式要求（JSON）
```

LLM输出格式（JSONMode=true）：
```json
{
  "report": "叙事轨文字，带主观色彩",
  "metrics": [
    { "label": "虎牢关控制值", "value": "42", "trend": "down", "confidence": "high", "is_delayed": false }
  ],
  "actions": [
    { "type": "move_units", "params": { "from": "D4", "to": "E4", "amount": "3" } }
  ],
  "action_id": "minister_action_uuid"
}
```

`actions`字段由`minister/parser.go`解析并在`minister/engine.go`中执行，执行结果作为`MinisterActedEvent`进入事件流。

### 信息失真规则（Prompt中体现）

```
核心区内的信息：accuracy=1.0，delay=0
边界/缓冲区信息：accuracy=0.7，delay=1回合
对方领土信息：accuracy=0.5，delay=1-2回合

忠诚度<5的部长：对己方不利的信息accuracy再×0.7
野心值>7的部长：资源类建议会轻微夸大需求
```

---

## 14. 配置与环境变量

```bash
# 服务器
SERVER_PORT=8080
SERVER_ENV=development          # development|production

# Redis
REDIS_ADDR=localhost:6379
REDIS_PASSWORD=
REDIS_DB=0

# PostgreSQL（Gamejam阶段可不配置，使用内存实现）
POSTGRES_DSN=postgres://user:pass@localhost/panoptes

# Lobby / Game
DEFAULT_MAX_PLAYERS=2
DEV_MODE=false
TOKENS_PER_TURN=3
TURN_TIME_LIMIT_DOMESTIC=15
TURN_TIME_LIMIT_COMBAT=20

# LLM
QWEN_API_KEY=
DEEPSEEK_API_KEY=

# JWT
JWT_SECRET=your-secret-key
JWT_EXPIRATION=86400

# 游戏数据
DATA_ROOT=../data
MAP_PATH=data/maps/default.json
```

---

## 15. 开发顺序

严格按照此顺序开发，每步完成后再进入下一步。

```
Step 1：基础框架（Day 1）
  - go.mod，依赖安装
  - buf generate跑通，三端代码生成验证
  - config加载（环境变量）+ staticdata加载（generated bundle）
  - WebSocket Hub能连接，ClientFrame/ServerFrame消息收发正常
  - 硬编码两个测试账号（alice/bob），JWT生成和校验
  - 手动房间号加入（不做匹配队列）
  - 消息路由骨架（handler签名定好，内部TODO）

Step 2：ECS和地图（Day 2上午）
  - domain类型定义
  - ECS所有Component注册
  - 地图加载（default.json → ECS World中的Node Entity）
  - factory.go：CreateUnit、CreateBuilding工厂函数

Step 3：内政阶段（Day 2下午～Day 3上午）
  - Phase interface和状态机骨架
  - DomesticPhase：收消息，令牌操作Handler
  - Economy Runner + stages
  - 内政结算Runner
  - 推送MsgDomesticSettlement

Step 4：战斗阶段（Day 3下午～Day 4）
  - CombatPhase：战区指令收集
  - algo/pathfinding A*实现和测试
  - SingleStepResolver phases
  - edge conflict / node group conflict
  - 战斗结算Runner
  - 推送MsgCombatSettlement（含动画事件序列）

Step 5：LLM部长（Day 5）
  - llm/interface和anthropic实现
  - minister/prompt序列化
  - minister/parser JSON解析和actions执行
  - minister/engine异步调度
  - 流式汇报推送（MsgMinisterReportChunk）
  - minister/memory跨回合记忆

Step 6：联调和修复（Day 6）
  - 和客户端全流程联调
  - 断线重连（Redis存状态，重连后发MsgGameInit）
  - 胜负判定和MsgGameOver

Step 7：打磨（Day 7）
  - 数值调整（只改根 `data/` 作者源）
  - 超时边界情况处理
  - 错误处理完善
  - Demo录制准备
```

---

*文档版本：1.0 | 项目代号：Panoptes | 最后更新：2026-04-04*
*本文档由人工确认，Agent开发以此为准，不得自行修改已定义的接口和结构。*
