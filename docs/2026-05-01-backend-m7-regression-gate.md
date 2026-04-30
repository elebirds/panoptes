# Panoptes M7 后端回归门记录

> 日期：2026-05-01  
> 范围：M7 大臣默认执行层  
> 结论：通过，可以进入 M8 信息不对称与失真汇报

## 1. 完成范围

- 启用 `set_minister_directive` 的 accept/reject 执行路径，不再作为保留边界拒绝。
- 新增 minister default intent 应用器：大臣默认计划写入现有 planning draft 结构，不新增旁路状态。
- planning start 前为人类玩家应用 rule-based minister defaults，并跳过 `submit_turn`，让玩家仍可覆写。
- 默认计划覆盖研究、政策、制度、建筑、配方和单位订单等既有 planning intent 表面。
- 玩家手动命令仍沿用既有处理器；研究/政策覆写会让已接受大臣草案进入 stale 记录。
- 修复 M6 地图动作写入口遗漏：`destroy_road` / `raid_storage` 现在能通过正式 planning unit order 写入。
- HTTP debug sync 中嵌套 proto 消息改为 protojson 编码，避免 oneof 被 `encoding/json` 编成不可回读形态。

## 2. 验证命令

| 检查 | 命令 | 结果 |
|---|---|---|
| 聚焦测试 | `cd server && go test -count=1 ./internal/game/planning ./internal/game/turn ./internal/game/orders ./internal/transport/http` | 通过 |
| 全量测试 | `cd server && go test -count=1 ./...` | 通过 |
| Lint | `make lint` | 通过 |
| diff 空白检查 | `git diff --check` | 通过 |

## 3. M7 完成定义对照

| Roadmap item | 状态 |
|---|---|
| 大臣生成 planning intents | 完成；复用 `RuleBotProvider` 生成既有 planning intents |
| 国内、经济、军事等部门分工 | 完成基础版；国内草案承接研究/政策，默认执行层承接全局 rule-based intents |
| 大臣提出建设、物流、科技、制度、军事方案 | 完成现有后端表面：建设、配方、科研、政策、制度、单位行动 |
| 玩家批准、否决、锁定、覆写 | 完成批准/否决/覆写；锁定保留到后续细化 |
| rule-based provider 先行，LLM 只做表达或建议增强 | 保持；默认执行不调用 LLM |
| 大臣命令和玩家命令共用 planning intent 结构 | 完成；写入同一套 `TurnRuntime.Planning` |

## 4. M8 入口边界

- M8 可以在现有 `truth -> observation -> planning_start/snapshot` 链路上加入信息分层。
- 大臣默认计划已经基于 observation 生成，后续可把 reported/distorted 输入替换进去。
- 亲政令牌和信息穿透应优先影响 observation/reporting，不应绕过 M7 默认执行写入口。
