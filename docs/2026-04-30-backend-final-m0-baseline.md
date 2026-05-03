# Panoptes 终版后端 M0 基线冻结

> 状态：M0 产物草案  
> 日期：2026-04-30  
> 输入：`docs/2026-04-30-backend-final-target.md`、`docs/2026-04-30-backend-final-roadmap.md`、新版 GDD、服务端运行时现状  
> 输出：GDD 对照矩阵、后端模块责任矩阵、验收矩阵、M1 进入条件

## 1. M0 结论

M0 冻结以下裁决：

- 终版后端路线先做国家机器，再做大臣与信息不对称。
- 国家机器完整性至少包括城市网络、本地仓储、半显式物流、工业链、制度、专业战争和补给/破坏后果。
- 物流终版目标是优先级驱动的容量/成本流分配，不是逐批货物模拟。
- 政策/国策先影响物流优先级；大臣细化优先级留到国家机器完成后。
- 当前胜利只保留征服；战略崩溃和经济胜利是长期规划，不进入 M1。
- 信息层终版必须支持 `truth / observed / reported`，但 M1 不实现失真或迷雾。

## 2. GDD 对照矩阵

| GDD 系统 | 终版后端目标 | 当前状态 | 路线阶段 | M1 处理 |
|---|---|---|---|---|
| 回合结构 | 单一 planning，统一 resolving，所有真实变更可审计 | 已有 `planning / resolving` 主链 | M1 | 固化 stage contract 和事件审计 |
| 地图与城市 | 多城市、城市核心、可扩展辖区、城市网络 | 主城、建城、固定辖区、city state 已有 | M2 | 只记录责任边界，不改辖区 |
| 建筑系统 | 城内/城外双轨、生命周期、接管、废墟 | 已有 binding、lifecycle、takeover、capture | M1/M2 | 审计事件和运行态边界 |
| 资源系统 | 本地仓储、网络调拨、容量限制 | 当前以全局库存为主 | M3 | 明确保留迁移边界 |
| 点数系统 | 非库存国家能力，支持制度/物流/建设作用域 | `research_output`、`industry_output` 已有 | M5 | 保持现状，避免扩为库存 |
| 修正系统 | 科技/政策/制度/建筑统一 modifier，作用域扩展 | 统一入口已存在 | M1/M5 | 审计来源、避免重复入口 |
| 科技系统 | 解锁国家能力，次回合正式生效 | 研究、完成、激活已闭环 | M1/M5 | 固化时序合同 |
| 政策/制度 | 国策影响短期方向，制度塑造长期国家结构 | 国策和 institution 后端已有基础 | M5 | 保留 policy priority profile 预留 |
| 配方系统 | 生产和转化统一语言，受物流和优先级影响 | 单建筑单配方、低效推进已闭环 | M3/M4 | 标注未来输入从可达库存扣除 |
| 单位系统 | 当前直接指挥，终版大臣默认执行 + 玩家覆写 | move/attack/charge/settle 已闭环 | M6/M7 | M1 不接大臣执行 |
| 军事系统 | 网络化战争，破坏道路/设施/补给 | 单位战斗和主城判负已有 | M6 | 只清理旧壳/未接线动作 |
| 道路/物流 | 半显式 flow，容量、成本、优先级 | road 数据存在，默认 resolving 未接线 | M2/M3/M4 | 标记未接线入口 |
| 大臣系统 | 大臣生成默认 intents，解释 observed 状态 | 代码存在但默认不影响裁决 | M7 | M1 继续隔离，不提前启用 |
| 信息系统 | `truth / observed / reported` 分层，可配置透明度 | 投影层已有，仍接近 truth | M8 | 只保留架构约束 |
| PVE | 复用玩家规则的服务器控制国家 | bot/rulebot 雏形 | M9 | M1 不扩展 PVE |
| 胜利条件 | 当前征服，长期战略崩溃/多胜利预留 | 主城摧毁判负 | M6+ | M1 不新增胜利类型 |

## 3. 后端模块责任矩阵

| 模块 | 当前责任 | 终版责任 | M1 关注点 |
|---|---|---|---|
| `domain` | 权威状态、玩家、地图、runtime、类型定义 | 保存 truth 层长期状态和规则必要状态 | 标注哪些状态是 durable，哪些是 turn runtime |
| `ecs` | 地图节点、单位、建筑组件与查询 | 继续承载地图实体与可结算对象 | 避免在 ECS helper 中隐式写状态 |
| `event` | 状态变更唯一正式入口之一 | 所有真实变更可审计、可投影、可回放 | 补齐事件审计规则和命名规范 |
| `engine/economy` | 点数、研究、建造、配方 | 未来承接本地仓储和物流输入 | 固化 stage 顺序和 apply 时机 |
| `engine/combat` | 单步 WEGO 战斗、路径、伤害 | 未来扩展补给、攻城、破坏、网络战争 | 明确 SingleStepResolver 是主入口 |
| `building/orchestration` | lifecycle、takeover、city capture | 继续维护建筑状态和城市陷落规则 | 明确和 economy/build 的边界 |
| `game/planning` | 接收玩家 planning commands | 未来接收玩家和大臣 intents | 保持未接线命令显式拒绝 |
| `game/resolution` | resolving stage 编排 | 终版所有后端裁决的主流程 | 输出 stage contract 文档 |
| `game/session` | 初始化、planning start、bootstrap、projection bridge | 未来承接模式配置和可见性入口 | M1 不引入信息失真 |
| `game/projection` / `game/query` | NodeView、GameSync、Observation | 未来拆出 observed/reported | 记录 truth 投影假设 |
| `staticdata` / `datagen` | 作者源生成和运行时 catalog | 终版所有玩法数值来源 | M1 不新增数据 schema，记录后续需求 |
| `algo/graph` | 简单 max-flow | 未来提供 flow/min-cost-flow 通用算法 | M1 不实现新算法，只确认边界 |
| `transport` | WebSocket/http/dispatch/protojson | 保持透明传输，不承载玩法逻辑 | 继续防止 transport import game 反向依赖 |

## 4. M0 验收矩阵

| 验收项 | 状态 | 证据 |
|---|---|---|
| 终版后端目标已存档 | 完成 | `docs/2026-04-30-backend-final-target.md` |
| 分阶段路线图已存档 | 完成 | `docs/2026-04-30-backend-final-roadmap.md` |
| GDD 对照矩阵已建立 | 完成 | 本文档第 2 节 |
| 后端模块责任矩阵已建立 | 完成 | 本文档第 3 节 |
| 近期非目标已冻结 | 完成 | 目标文档第 11 节 |
| M1 可执行边界已明确 | 完成 | `docs/2026-04-30-backend-m1-rule-foundation-plan.md` |
| 代码实现未开始 | 完成 | M0 仅新增文档和任务拆分 |

## 5. M1 进入条件

M1 可以开始，但必须遵守以下边界：

- 只做规则底座长期化，不实现道路物流、本地仓储、大臣、迷雾、战略崩溃。
- 允许清理或显式拒绝未接线入口。
- 允许补充测试、harness、事件审计和文档。
- 任何 proto 或数据 schema 变更都必须单独列为 M1 子任务，并说明为什么不能等到 M2。

