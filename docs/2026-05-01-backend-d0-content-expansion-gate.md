# Panoptes D0 内容扩展门禁

> 日期：2026-05-01
> 范围：M0-M9 后端主线后的静态内容扩展
> 结论：通过

## 1. 目标

D0 不新增规则代码，只扩展 `data/` 作者源，让已有国家机器系统拥有更接近长局的内容素材，并同步生成服务端与客户端静态目录。

## 2. 本次扩展

| 类型 | 变更 |
|---|---|
| 建筑 | 新增 `granary`、`smelter`、`stable`、`engineer_camp` |
| 配方 | 新增 `granary_rations`、`smelter_refined_ore`、`stable_cavalry`、`engineer_camp_raider`、`engineer_camp_siege_engine` |
| 单位 | 新增 `cavalry`，可由 ECS 装配冲锋能力 |
| 科技 | 新增 `supply_depots`、`metallurgy`、`mounted_logistics`、`siegecraft` |
| 制度 | 新增 `foundry_directives` |
| 地图 | 新增 `frontier_basin` 30x30 中型边境地图 |
| UI | 补齐新增建筑、配方、单位、科技、制度和科技树节点/连线 |
| 生成产物 | 同步更新 `data/generated/server/` 与 `client/Assets/Resources/Data/` |

扩展后作者源规模：

```text
buildings:    13
recipes:      15
technologies: 13
units:        6
policies:     7
maps:         3
```

## 3. 验收覆盖

- `TestRealContentCatalogSupportsExpandedMVPContent` 现在校验：
  - `frontier_basin` 地图、资源点和初始道路节点。
  - D0 新建筑与默认配方绑定。
  - `cavalry` 被真实目录加载，并能装配 `ChargeAbilityC`。
  - D0 新科技存在，`mounted_logistics` 解锁马厩与骑兵配方。
  - `foundry_directives` 为制度层政策并携带物流优先级。
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

D0 内容扩展完成。当前内容仍不是最终平衡稿，但已经让物流、工业、军事机动、破坏、攻城和地图长局验收拥有更宽的静态素材面。后续可以在不改规则主干的前提下继续做 D1 平衡、AI 偏好、地图池和客户端可读性补强。
