# 接入攻城器械和斥候单位

## Goal

补齐攻城器械和斥候从静态配置、客户端展示资源到后续维护说明的接入缺口，让它们能够沿用现有生产/解锁链路并在客户端以明确单位身份展示。

## What I Already Know

- `data/content/units/units.json` 已有 `siege_engine` 和 `scout` 的玩法作者源定义。
- `data/content/recipes/recipes.json` 已有 `engineer_camp_siege_engine` 和 `watchtower_scout` 生产配方。
- UI catalog 目前已有两者条目，但 `siege_engine` 复用步兵图标/战士 prefab，`scout` 复用开拓者图标/开拓者 prefab。
- 用户要求攻城器械暂无合适模型，允许占位；斥候可以复用战士模型。

## Requirements

- 补齐 `siege_engine` 和 `scout` 的客户端 UI catalog 配置，提供稳定的 `icon_key` 与 `prefab_key`。
- 为两个单位提供客户端可加载的 prefab 资源；攻城器械可使用明确占位美术，斥候复用战士/步兵模型风格。
- 为两个单位提供图标资源，并同步 recipe icon，使生产列表不再显示基础单位占位图标。
- 同步作者源、客户端 Resources 数据、服务端 generated 数据，保持 catalog bundle 一致。
- 新写一份文档记录本次新增/修改内容、资源复用关系和后续替换指引。

## Acceptance Criteria

- [x] `siege_engine` 和 `scout` 在 unit catalog 中有独立 `icon_key` 与 `prefab_key`。
- [x] 客户端 `Resources/Prefabs/Units` 下能加载攻城器械和斥候 prefab。
- [x] 客户端 `Resources/Icons/Units` 下有两个新单位图标。
- [x] recipe catalog 使用对应单位图标。
- [x] 生成侧 JSON 与作者源 JSON 保持同等内容。
- [x] 新文档说明本次接入点和后续美术替换方式。

## Definition of Done

- `git diff --check` 通过。
- JSON 文件可被解析。
- 不修改生成协议目录。
- 若 Unity/Go 工具链不可用，需要在最终说明中明确。

## Out of Scope

- 不新增协议字段。
- 不新增新的攻城/侦察服务端规则；本任务只补现有单位从生产到客户端展示的缺口。
- 不引入新的第三方依赖。

## Technical Notes

- `python3` 在本机不可用，Trellis task 目录手工创建。
- 需要检查现有 `Archer.prefab`、`Cavalry.prefab`、`BaseVehicle.prefab` 的 prefab 约定后复用。
- `Scout.prefab` 复用 `client/Assets/Prefabs/Fighter.prefab`。
- `SiegeEngine.prefab` 是临时占位，由 Unity cube mesh 部件组成，后续可在同一路径替换正式美术。
