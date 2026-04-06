# Panoptes Core Data Layer Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为服务端落地游戏核心数据层，包括配置加载、领域模型、ECS 组件/工厂/查询、地图初始化与真实 `MsgGameInit`。

**Architecture:** 以 `config` 提供只读数值和地图路径，`domain` 固化稳定类型与纯查询，`ecs` 负责实体装配，`maploader` 负责 JSON 地图到 ECS/MapData 的转换，`game` 只负责组装运行时状态与消息下发。

**Tech Stack:** Go 1.26, donburi ECS, protobuf, standard library JSON, google/uuid

---

## Chunk 1: 测试先行

### Task 1: domain 与 config 基础测试

**Files:**
- Create: `server/internal/domain/types_test.go`
- Create: `server/internal/config/gamedata_test.go`

- [ ] **Step 1: 写 `Position` / `Resources` / `NewGameState` 行为测试**
- [ ] **Step 2: 运行测试，确认因实现缺失而失败**
- [ ] **Step 3: 写 `GameData` / 地图路径默认值 / JSON 加载测试**
- [ ] **Step 4: 运行测试，确认失败原因正确**

### Task 2: ecs 与 maploader 测试

**Files:**
- Create: `server/internal/ecs/factory_test.go`
- Create: `server/internal/engine/maploader/loader_test.go`

- [ ] **Step 1: 写节点/单位/建筑工厂测试**
- [ ] **Step 2: 写地图加载与世界初始化测试**
- [ ] **Step 3: 运行测试，确认缺少实现而失败**

### Task 3: GameRoom 真实初始化测试

**Files:**
- Modify: `server/internal/game/room_test.go`

- [ ] **Step 1: 改写初始化断言，校验真实地图、玩家状态和节点列表**
- [ ] **Step 2: 运行目标测试，确认现有假数据实现失败**

## Chunk 2: 最小实现

### Task 4: config / data / domain

**Files:**
- Modify: `server/internal/config/config.go`
- Create: `server/internal/config/gamedata.go`
- Create: `server/internal/config/loader.go`
- Create: `server/data/gamedata.json`
- Create: `server/data/maps/default.json`
- Create: `server/internal/domain/types.go`
- Create: `server/internal/domain/state.go`
- Create: `server/internal/domain/node.go`
- Create: `server/internal/domain/unit.go`

- [ ] **Step 1: 补齐配置结构与默认路径**
- [ ] **Step 2: 实现 GameData 结构和 JSON 加载**
- [ ] **Step 3: 落地枚举、值对象、GameState 与查询**
- [ ] **Step 4: 运行对应测试并修正**

### Task 5: ecs / maploader

**Files:**
- Create: `server/internal/ecs/components.go`
- Create: `server/internal/ecs/factory.go`
- Create: `server/internal/ecs/query.go`
- Create: `server/internal/engine/maploader/loader.go`

- [ ] **Step 1: 注册组件与查询封装**
- [ ] **Step 2: 实现节点/单位/建筑工厂**
- [ ] **Step 3: 实现地图文件解析与世界初始化**
- [ ] **Step 4: 运行对应测试并修正**

### Task 6: game 接线

**Files:**
- Modify: `server/internal/game/room.go`
- Modify: `server/internal/game/room_test.go`

- [ ] **Step 1: 用真实地图替换假初始化**
- [ ] **Step 2: 从 ECS World 组装 `MsgGameInit`**
- [ ] **Step 3: 只给真人玩家推送初始化消息**
- [ ] **Step 4: 运行房间测试并修正**

## Chunk 3: 整体验证

### Task 7: 完整验证

**Files:**
- Modify: `server/go.mod`
- Modify: `server/go.sum`

- [ ] **Step 1: 接入 `github.com/yohamta/donburi` 依赖**
- [ ] **Step 2: 运行 `gofmt -w` 覆盖新增和修改文件**
- [ ] **Step 3: 运行 `go test ./...`**
- [ ] **Step 4: 运行 `go build ./...`**
- [ ] **Step 5: 记录剩余 TODO，仅保留 Engine System 接入点**
