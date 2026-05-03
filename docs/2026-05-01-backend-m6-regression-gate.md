# Panoptes M6 后端回归门记录

> 日期：2026-05-01  
> 范围：M6 网络化战争  
> 结论：通过，可以进入 M7 大臣默认执行层

## 1. 完成范围

- 新增 `destroy_road` 单位地图动作，破坏行为经 `RoadDestroyedEvent` 写回权威状态。
- 新增 `raid_storage` 单位地图动作和 `StorageRaidedEvent`，劫掠城市本地仓储并进入事件审计。
- 战斗维护阶段接入道路补给：已有城市道路补给网络时，离网单位会产生 `unit_starving`。
- 补给约束保留旧场景兼容性：没有城市道路补给网络的玩家仍按传统维护粮结算。
- 静态目录新增 `raider` 与 `siege_engine`，覆盖破路/袭扰和攻城谱系。
- 征服胜利路径未扩展，主城摧毁仍是当前唯一主动胜利闭环。

## 2. 验证命令

| 检查 | 命令 | 结果 |
|---|---|---|
| 数据生成 | `make data-gen` | 通过 |
| 聚焦测试 | `cd server && go test -count=1 ./internal/game/orders ./internal/engine/combat ./internal/event ./internal/datagen` | 通过 |
| 全量测试 | `cd server && go test -count=1 ./...` | 通过 |
| Lint | `make lint` | 通过 |
| diff 空白检查 | `git diff --check` | 通过 |

## 3. M6 完成定义对照

| Roadmap item | 状态 |
|---|---|
| 五个核心谱系：开拓者、步兵、弓手、攻城、破坏/袭扰 | 完成；新增 `raider` 与 `siege_engine` |
| 攻城单位对核心、防御、城墙有特殊规则 | 保留既有结构攻击能力；城墙/防御细化后续扩展 |
| 破坏单位可破坏道路、设施、仓储、补给节点 | 完成道路破坏和仓储劫掠；设施破坏复用既有结构攻击/摧毁事件 |
| 前线补给影响单位战斗能力和恢复 | 完成维护阶段缺补给饥饿惩罚 |
| 防御建筑先强化被动控制，再预留主动防区 | 预留；不在 M6 启用主动防区 |
| 征服胜利仍为唯一当前胜利 | 保持不变 |

## 4. M7 入口边界

- M7 大臣可以复用 M2-M6 的真实国家机器：道路、仓储、物流优先级、制度修正、破路和劫掠。
- 大臣默认执行层应优先生成 planning intents，不新增旁路规则。
- 军事大臣可先以 rule-based provider 生成建设、补给、防御、袭扰与攻城建议，LLM 只做表达增强。
