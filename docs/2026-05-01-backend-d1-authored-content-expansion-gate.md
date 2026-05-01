# Panoptes D1 作者源扩展门禁

> 日期：2026-05-01
> 范围：D0 后继续扩展建筑、配方、科技、制度和单位作者源
> 结论：通过

## 1. 目标

D1 延续 D0 的内容扩展方向，不新增规则代码，只用现有静态数据能力继续拓宽长局内容面。

## 2. 本次扩展

| 类型 | 变更 |
|---|---|
| 建筑 | 新增 `warehouse`、`academy`、`market`、`watchtower`、`training_ground` |
| 配方 | 新增 `warehouse_reserve_rations`、`market_grain_contracts`、`market_lumber_contracts`、`market_ore_contracts`、`watchtower_scout`、`training_ground_spearman` |
| 单位 | 新增 `scout`、`spearman` |
| 科技 | 新增 `centralized_storage`、`trade_levies`、`scholastic_bureaucracy`、`sentry_networks`、`professional_drill` |
| 制度 | 新增 `mercantile_charter`、`research_mandate` |
| UI | 补齐新增建筑、配方、单位、科技、制度和科技树节点/连线 |
| 生成产物 | 同步更新 `data/generated/server/` 与 `client/Assets/Resources/Data/` |

扩展后作者源规模：

```text
buildings:    18
recipes:      21
technologies: 18
units:        8
policies:     9
```

## 3. 验收覆盖

- `TestRealContentCatalogSupportsExpandedMVPContent` 现在额外校验：
  - D1 新建筑与默认配方绑定。
  - `academy` 提供研究点产出修正。
  - `scout` 是高速高视野 civilian 单位，并可由 ECS 装配为纯 civilian 能力。
  - `spearman` 是可占领、可攻击建筑的 melee 单位。
  - D1 新科技全部存在，`trade_levies` 解锁市集、资源契约和 `mercantile_charter`。
  - `mercantile_charter` 与 `research_mandate` 是制度层 modifier policy。
- `make data-validate` 校验作者源 schema、跨引用和科技树 prerequisite edge。
- `make data-gen` 生成服务端与客户端静态目录。

## 4. 门禁结果

```text
make data-validate
PASS

make data-gen
PASS

cd server && go test -count=1 ./internal/debug ./internal/datagen ./internal/staticdata
PASS

cd server && go test -count=1 ./...
PASS

make lint
PASS

git diff --check
PASS
```

## 5. 结论

D1 作者源扩展完成。当前内容已经覆盖更清晰的仓储物流、学术研究、市集调拨、哨塔侦察和职业军训线，足以继续支撑后端长局、PVE 和后续客户端可读性接入。
