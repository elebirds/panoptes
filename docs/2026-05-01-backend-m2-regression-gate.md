# Panoptes M2 后端回归门记录

> 日期：2026-05-01  
> 范围：M2 道路、连通与城市网络  
> 结论：通过，可以进入 M3 本地仓储与容量物流

## 1. 完成范围

M2 分两段完成：

- `9cac996 feat: implement m2 road connectivity`
  - 接入 `build_road` / `repair_road`
  - 新增后端 road connectivity 查询
  - 道路建造、破坏、修复进入事件与 projection

- M2.2 networked improvements and projections
  - 接入 `build_improvement` / `repair_improvement`
  - 新增 `BuildingRepairedEvent`
  - `NodeView` 新增 `road_status`, `network_status`, `network_city_id`, `is_network_connected`
  - 建筑、资源设施、城市核心可以解释自身网络连接状态

## 2. 验证命令

| 检查 | 命令 | 结果 |
|---|---|---|
| 协议/数据生成 | `PATH="/opt/homebrew/opt/protobuf/bin:$HOME/go/bin:$PATH" mise exec -- make gen` | 通过 |
| 聚焦测试 | `cd server && go test -count=1 ./internal/domain ./internal/event ./internal/game/orders ./internal/game/query ./internal/game/projection ./internal/game/planning` | 通过 |
| 全量测试 | `cd server && go test -count=1 ./...` | 通过 |
| Lint | `PATH="/opt/homebrew/opt/protobuf/bin:$HOME/go/bin:$PATH" mise exec -- make lint` | 通过 |
| diff 空白检查 | `git diff --check` | 通过 |

## 3. M2 完成定义对照

| Roadmap item | 状态 |
|---|---|
| 接入 `build_road / repair_road` | 完成 |
| 引入工程单位或等价工程行为 | 完成；当前由 civilian/engineer-capable actor 承担 |
| 城市辖区从固定模型升级为可计算模型 | 完成基础版；城市状态和 node network status 通过后端查询计算 |
| 建筑、资源点、新城接入 network connected 判定 | 完成；`NodeView` 和 domain query 均可解释 |
| 道路破坏和修复进入事件流 | 完成 |
| 投影 road status、network status、connected city | 完成 |

## 4. M3 入口边界

- M2 只表达“是否连接”和“连接到哪座城市”，不表达容量、库存、流量、优先级或路径分配。
- M3 可以在 `domain.NodeNetworkStatusForPlayer` 和 `domain.PlayerRoadNetworkStatus` 之上引入本地仓储和容量物流。
- 后续如果要把 network status 做成更复杂的 visibility/reporting 层，必须继续保持 truth/query/projection 分层。
