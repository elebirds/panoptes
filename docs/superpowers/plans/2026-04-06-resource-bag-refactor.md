# Resource Bag Refactor Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将资源模型改为动态 `ResourceBag`，包括服务端内部表示和 proto 协议层，客户端同步更新以使用 `ResourceBag` 替代固定字段的 `Resources`。

**Architecture:** `config` 使用动态 map 读取所有成本/产出配置，`domain` 使用 `ResourceKey` + `ResourceBag` 作为内部统一资源表示，`game` 在 WebSocket/proto 边界直接使用 `pb.ResourceBag`（而非固定字段的 `pb.Resources`）。客户端通过 `ResourceBag.items` 列表展示动态资源，内部扩展资源只需改配置、协议和结算逻辑，无需修改客户端固定字段。

**Tech Stack:** Go 1.26, standard library JSON, protobuf

---

## Chunk 1: 红灯测试

### Task 1: 动态资源领域测试

**Files:**
- Modify: `server/internal/domain/types_test.go`

- [ ] **Step 1: 写 `ResourceBag` 的加减、比较、未知资源保留测试**
- [ ] **Step 2: 运行 `go test ./internal/domain`，确认因旧模型而失败**

### Task 2: 配置与协议边界测试

**Files:**
- Modify: `server/internal/config/gamedata_test.go`
- Modify: `server/internal/game/room_test.go`

- [ ] **Step 1: 写 `gamedata` 对未知资源 key 的保留测试**
- [ ] **Step 2: 写 proto 转换只映射已知字段的测试**
- [ ] **Step 3: 运行目标测试，确认失败点正确**

## Chunk 2: 最小实现

### Task 3: 配置与领域模型重构

**Files:**
- Modify: `server/internal/config/gamedata.go`
- Modify: `server/internal/domain/types.go`
- Modify: `server/internal/domain/state.go`

- [ ] **Step 1: 将 `config.ResourceAmount` 改为动态 map**
- [ ] **Step 2: 引入 `domain.ResourceKey` 与 `domain.ResourceBag`**
- [ ] **Step 3: 更新 `NewGameState` 初始化为动态资源袋**
- [ ] **Step 4: 运行目标测试修正实现**

### Task 4: 边界转换

**Files:**
- Modify: `server/internal/game/room.go`

- [ ] **Step 1: 将 `toProtoResources` 切到 `ResourceBag`**
- [ ] **Step 2: 明确未知资源在 proto 边界被忽略**
- [ ] **Step 3: 运行 `go test ./internal/game`**

## Chunk 3: 全量验证

### Task 5: 全量检查

**Files:**
- Modify: `server/internal/config/gamedata_test.go`
- Modify: `server/internal/domain/types_test.go`
- Modify: `server/internal/game/room_test.go`

- [ ] **Step 1: `gofmt -w` 格式化改动文件**
- [ ] **Step 2: 运行 `go test ./...`**
- [ ] **Step 3: 运行 `go build ./...`**
