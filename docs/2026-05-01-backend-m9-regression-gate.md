# Panoptes M9 后端回归门记录

> 日期：2026-05-01  
> 范围：M9 PVE 国家  
> 结论：基础闭环完成，服务器控制国家可以进入 planning/resolving 主线

## 1. 完成范围

- `scenario.Definition` 支持显式声明 `human`、`bot`、`ai` participant。
- 保留旧 `PlayerIDs` / `Usernames` 作为 human fallback，不破坏既有场景。
- debug `Harness` 可启动混合人类/PVE 对局，并且只为 human participant 执行客户端 catalog sync。
- 新增 `PVESkirmish` 场景：人类国家与 bot 国家共存，bot 使用 `RuleBotProvider`。
- 新增 headless 回归，验证 bot 无客户端输入也能：
  - 获得 observation；
  - 生成 planning intent；
  - 自动提交回合；
  - 经 resolving 对敌方城市核心造成正常事件伤害；
  - 由 human 侧 `GameSync` 观察到事件。
- PVE 仍复用既有 planning validation、resolving runner 和 `event.Apply()`，不引入直接改 state 的旁路。

## 2. M9 完成定义对照

| Roadmap item | 状态 |
|---|---|
| AI player 使用同一套 planning intent | 完成基础闭环；`AutonomousController` 通过 `planning.IntentEnvelope` 提交 |
| AI 使用城市、物流、科技、制度、战争规则 | 完成接线；现有 `RuleBotProvider` 已可覆盖建设、科研、政策、制度、单位、战争意图 |
| 脚本只影响偏好和目标，不绕过规则 | 完成契约；见 M9 preflight debt table 和 backend spec |
| 支持 scenario authoring | 完成基础版；scenario 可声明 participant kind |
| 支持未来 PVE 信息层和大臣层 | 完成入口；PVE 读取 observed，reported 保持 advisory metadata |

## 3. 仍然保留的深化项

- 更强的 PVE 战略目标和难度 profile。
- PVE 长局经济/扩张/战争策略调优。
- PVE 受 `reported` 内容失真影响的二阶段决策。
- PVE 专用 scenario authoring 文件格式或调试 CLI。

## 4. 验证命令

```bash
cd server && go test -count=1 ./internal/game/scenario ./internal/debug ./internal/game/turn
cd server && go test -count=1 ./...
make lint
git diff --check
```

结果：全部通过。
