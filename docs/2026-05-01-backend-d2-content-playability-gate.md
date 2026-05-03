# Panoptes D2 内容可玩性门禁

> 日期：2026-05-01
> 范围：D0/D1 作者源在后端真实运行链路中的可达性
> 结论：通过

## 1. 目标

D2 不新增玩法数值或规则系统，而是在 D0/D1 的作者源扩展之后，证明真实 generated catalog 能被后端运行链路消费：建筑 modifier 生效、配方能完成、单位能从真实配方进入 ECS、真实地图能跑混合 human/PVE 长局同步。

## 2. 覆盖链路

| 内容面 | 覆盖项 | 后端证明 |
|---|---|---|
| 仓储 / 物流 | `warehouse`、`warehouse_reserve_rations` | `warehouse` 提升 `EffectiveRoadBaseCapacity`，后备口粮配方完成后城市粮食净增加 |
| 贸易 | `market`、`market_ore_contracts` | 市集矿石契约通过经济 Runner 完成，城市矿石增加 |
| 研究 | `academy` | 学宫提升 `EffectiveResearchOutput` |
| 防御 / 视野 | `watchtower`、`watchtower_scout`、`scout` | 哨塔斥候配方完成后，真实 `scout` 单位进入 ECS 并位于生产节点 |
| 职业军事 | `training_ground`、`training_ground_spearman`、`spearman` | 校场长矛兵配方完成后，真实 `spearman` 单位进入 ECS 并位于生产节点 |

## 3. 新增门禁

- `TestRealContentD2PlayabilityChainCoversAuthoredExpansion`
  - 加载 `data/generated/server`。
  - 使用真实 D1 建筑、配方和单位定义。
  - 通过 `economy.NewRunner()` 推进配方完成，不绕过经济结算。
  - 每段后调用 `AssertStateInvariants`。

- `TestHarnessD2RealContentFrontierBasinPVESoakPreservesStateInvariants`
  - 加载真实 `frontier_basin` 地图。
  - 建立 human + bot prepared room。
  - 每回合检查 planning start 和 game sync 的 information report。
  - 每回合调用 `AssertStateInvariants`。
  - 当前真实规则 `max_turns=30`，因此门禁允许运行到配置内 game over；若未来真实规则提高上限，该测试会继续跑到 50 回合。

## 4. 门禁结果

```text
cd server && go test -count=1 ./internal/debug
PASS

cd server && go test -count=1 ./...
PASS

make lint
PASS

git diff --check
PASS
```

## 5. 结论

D2 已把“作者源存在”推进到“作者源能被真实后端系统消费”。D0/D1 新增内容现在至少覆盖一条可执行的仓储、贸易、研究、侦察和职业军事链路，并且真实地图混合 PVE 同步能够维持状态不变量。
