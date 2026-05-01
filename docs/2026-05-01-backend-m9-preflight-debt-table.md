# M9 前置后端债务表

> 日期：2026-05-01  
> 范围：进入 M9 PVE 国家前必须收口或明确边界的后端债务

## 1. 结论

M0-M8 已经完成国家机器、大臣默认执行和信息分层的基础闭环。进入 M9
前不需要把所有终版深化项一次性补完，但必须明确三条边界：

- PVE 国家只能复用既有 planning intent 和 resolving 规则。
- M9 阶段的 AI 决策输入使用 `observed`，`reported` 只作为可解释元数据。
- 至少保留一条跨 M2-M8 的 headless 长局回归，防止 PVE 开发放大已有系统间缝隙。

## 2. 债务表

| 优先级 | 债务项 | 当前状态 | 风险 | M9 前处理 |
|---|---|---|---|---|
| P0 | PVE 国家未实现 | M9 未开始 | 没有服务器控制势力，终版对手/环境压力缺位 | M9 主任务处理 |
| P0 | PVE intent 复用契约未显式记录 | 玩家、大臣、bot provider 已有 planning intent 表面 | PVE 若绕过 validation/resolving，会破坏“AI 不作弊”原则 | 本文冻结契约 |
| P0 | 跨 M2-M8 长局回归不足 | 各 milestone 有单点 gate | PVE 长局中物流、战争、大臣、信息层可能互相踩踏 | 本轮补 headless preflight |
| P1 | `reported` 仍是报告元数据，不是真正内容改写层 | M8 有 `InformationReportView` | 若 M9 直接让 AI 消费扭曲文本，规则可解释性会下降 | 本文冻结 M9 边界 |
| P1 | 大臣默认执行仍是 rule-based 基础版 | M7 可写入 planning drafts | 大臣不像有能力/性格/忠诚的部门 | M9 后深化 |
| P1 | 大臣锁定机制未完成 | 批准/否决/覆写已完成 | 玩家无法长期锁住某类方案 | 大臣深化处理 |
| P1 | 信息透明度没有阶段曲线 | clear/standard/high_distortion 已有 | 中后期不会自然降低透明度 | 信息层深化处理 |
| P2 | 主动防区/城墙细化未完成 | 结构攻击、破路、袭仓已可用 | PVE 攻防策略偏薄 | 战争深化处理 |
| P2 | 物流仍是半显式优先级分配 | M3/M4 可用 | 复杂网络下不是完整 min-cost flow | 规模扩大后处理 |
| P2 | 制度切换成本未启用 | M5 预留 | 治理代价不足 | 制度深化处理 |
| P3 | 客户端尚未消费信息报告 | 协议字段已生成 | 后端信息层暂时只在协议/测试可见 | 前端阶段处理 |

## 3. PVE Intent 复用契约

M9 PVE 国家必须遵守以下后端契约：

- PVE 决策器只输出 `planning.Intent` 或等价 `PlanningCommand`/`CommandEnvelope`。
- PVE 不直接修改 `domain.GameState`、ECS 组件、city storage、road state 或 event collector。
- PVE 不绕过 `game/planning` 和 `game/orders` 的 validation。
- PVE 不直接调用 engine system 来制造结果；真实状态变化只能发生在 resolving stage 和 `event.Apply()`。
- PVE 难度只能影响目标、偏好、初始条件、资源环境或 provider scoring，不能绕过玩家规则。
- PVE 和 bot/human minister defaults 共用同一套 planning intent 表面；如果需要新能力，先扩 planning intent，再由所有控制者共同使用。

推荐 M9 数据流：

```text
truth(GameState)
  -> query.BuildObservation(playerID)
  -> ai/pve provider reads observed snapshot
  -> planning.IntentEnvelope
  -> game/planning validation and draft writes
  -> resolving runner
  -> event.Apply()
  -> projection/GameSync
```

## 4. M9 信息边界

M9 阶段默认规则：

- AI/PVE provider 使用 `observed` 作为决策输入。
- `reported` (`InformationReportView`) 作为解释、风险和未来大臣偏差的元数据。
- PVE 不在 M9 主任务里消费自由文本失真报告来决定合法动作。
- 高失真模式可以改变 provider scoring 或保守度，但不能让 provider 看见未观察 truth。
- 亲政/clear inspection 只改变 observation/reporting，不绕过 planning/resolving。

这让 PVE 可以先复用玩家规则，同时给后续“大臣能力、性格、忠诚、误报文本”留出位置。

## 5. M9 入口 Gate

进入 M9 前至少保持以下检查通过：

```bash
cd server && go test -count=1 ./internal/debug ./internal/game/ai ./internal/game/turn
cd server && go test -count=1 ./...
make lint
git diff --check
```

其中 `./internal/debug` 应包含一条覆盖扩张、物流短缺、网络战争扰动、大臣 planning draft/default 表面和信息报告的长局回归。
