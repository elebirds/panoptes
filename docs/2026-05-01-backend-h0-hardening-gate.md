# Panoptes H0 后端硬化门禁

> 日期：2026-05-01
> 范围：M0-M9 后端主线后的第一层硬化
> 结论：通过

## 1. 目标

H0 不新增玩法机制，只补后端长期运行风险的第一道门禁：

- 可复用状态不变量检查。
- 混合 human/PVE 的 30+ 回合 headless soak。
- 每回合验证 `PlanningStart` / `GameSync` 信息报告存在。
- 让 PVE 继续走正常 planning/resolving 路径。

## 2. 本次实现

- 新增 `debug.AssertStateInvariants` 测试 helper，覆盖：
  - 玩家资源和城市仓储非负。
  - token、首都核心 HP 非负。
  - 建筑 owner、HP、MaxHP、节点索引一致性。
  - 单位 owner、HP、MaxHP、坐标落点、重复 unit id。
- 新增 `scenario.PVESoak()`：
  - `player-1` 为 human participant。
  - `bot-1` 为 autonomous bot participant。
  - 最大回合数 64，用于 40 回合 soak 避免自然超时胜负。
- 新增 `TestHarnessH0MixedPVESoakPreservesStateInvariants`：
  - 连续运行 40 回合。
  - 每回合等待 human 侧 `PlanningStart`。
  - human 提交空回合，bot 自主提交。
  - 每回合等待 `GameSync`。
  - 每回合在 planning start 后和 game sync 后运行状态不变量。

## 3. 门禁结果

```text
cd server && go test -count=1 ./internal/game/scenario
PASS

cd server && go test -count=1 ./internal/debug
PASS

cd server && go test -count=1 ./...
PASS

make lint
PASS

git diff --check
PASS
```

## 4. 结论

H0 已为后端主线补上第一层长期稳定性保护。当前覆盖的是 deterministic soak 和结构性不变量，后续 H1 可以继续扩展到 replay 一致性、随机种子矩阵、性能预算和更真实的内容长局。
