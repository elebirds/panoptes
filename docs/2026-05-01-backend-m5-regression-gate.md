# Panoptes M5 后端回归门记录

> 日期：2026-05-01  
> 范围：M5 科技、制度与国家修正深化  
> 结论：通过，可以进入 M6 网络化战争

## 1. 完成范围

- 新增 modifier trigger：`logistics.road_capacity`。
- `domain.EffectiveRoadBaseCapacity()` 通过统一 modifier 栈计算道路基础运量。
- M4 物流分配改为读取玩家有效道路运量，因此科技、国策、制度和建筑修正都能自然参与。
- 新增制度政策 `logistics_corps`，由 `civic_institutions` 解锁，提高道路基础运量。
- 保留既有科技完成后次回合激活、制度 loadout 次回合生效、事件审计边界。

## 2. 验证命令

| 检查 | 命令 | 结果 |
|---|---|---|
| 数据生成 | `make data-gen` | 通过 |
| 聚焦测试 | `cd server && go test ./internal/engine/economy ./internal/game/session ./internal/datagen` | 通过 |
| 全量测试 | `cd server && go test -count=1 ./...` | 通过 |
| Lint | `make lint` | 通过 |
| diff 空白检查 | `git diff --check` | 通过 |

## 3. M5 完成定义对照

| Roadmap item | 状态 |
|---|---|
| 扩展科技树依赖和分支 | 完成基础版；制度科技扩展出物流制度候选 |
| 科技显式效果覆盖建筑、配方、兵种、制度候选 | 完成；既有显式效果保留，新增制度候选 |
| 制度槽位玩法完整化 | 完成当前后端闭环：候选、槽位、loadout、次回合激活 |
| 制度影响物流优先级、运量、城市、军队和工业链 | 完成物流运量影响；优先级 profile 可被制度复用 |
| 修正系统支持更多作用域 | 完成 `logistics.road_capacity` |
| 增加制度切换成本的预留，不急于启用 | 保留未启用 |

## 4. M6 入口边界

- M6 可以直接用道路连通、仓储、物流容量和制度修正计算补给。
- 仓储破坏、前线补给和攻城/破坏兵种应继续通过事件写回权威状态。
