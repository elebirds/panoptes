# Panoptes 静态配置修改手册

本文是给策划、程序和需要改玩法配置的人看的最小操作手册。

目标只有一个：让你知道**应该改哪个作者源文件、什么时候要顺带改 UI/registry、改完如何同步到服务端和客户端**。

## 1. 先记住三条硬规则

1. 只改作者源，不改生成产物。
2. 改静态数据时，优先看根 `data/registry/`、`data/content/`、`data/ui/`。
3. 改完先校验，再生成。

不要手改下面这些生成目录：

- `data/generated/server/`
- `client/Assets/Resources/Data/`

静态数据的标准链路是：

```text
data/registry + data/content + data/ui
  -> make data-validate
  -> make data-gen
  -> data/generated/server/
  -> client/Assets/Resources/Data/
```

其中：

- 服务端运行时读取 `data/generated/server/`
- 客户端运行时读取 `client/Assets/Resources/Data/`

如果你只是改配置，通常只需要：

```bash
make data-validate
make data-gen
```

`make gen` 不是配置改动的常规命令；它会额外做 proto 和其他代码生成，通常只在你改协议或生成逻辑时才需要。

## 2. 目录分工

### 2.1 `data/content/`

这里放**玩法真相**，也就是服务端真正执行的静态规则。

例如：

- 科技的研究成本、前置、显式效果、修正效果
- 建筑的放置规则、消耗、默认配方、血量
- 配方的输入、输出、工时、基础推进

### 2.2 `data/ui/`

这里放**展示元数据**，也就是客户端和投影层会用到的名称、描述、图标、排序、布局。

例如：

- 科技/建筑/配方的中文名
- 描述文本
- 图标 key
- `sort_order`
- 科技树节点坐标和连线

### 2.3 `data/registry/`

这里放**底层注册表和全局键空间**。

当你不是在“改现有科技/建筑/配方”，而是在“新增一个新的资源 key / 点数 key / manifest 信息”时，应该先看这里。

常见文件：

- `data/registry/resources.json`
- `data/registry/points.json`
- `data/registry/manifest.json`

## 3. 我想改科技，应该改哪里

### 3.1 改科技玩法规则

改这里：

- `data/content/technologies/technologies.json`

这里负责：

- `id`
- `branch`
- `tier`
- `research_cost`
- `prerequisites`
- `explicit_effects`
- `modifier_effects`

适用场景：

- 调整研究成本
- 改前置科技/政策
- 让科技解锁建筑、配方、政策
- 给科技增加数值修正

最小示例：

```json
{
  "id": "agrarian_foundations",
  "branch": "agriculture",
  "tier": 1,
  "research_cost": 1,
  "prerequisites": [],
  "explicit_effects": [
    { "type": "unlock_building", "target_id": "farm" },
    { "type": "unlock_recipe", "target_id": "farm_food" }
  ],
  "modifier_effects": []
}
```

### 3.2 改科技显示名、描述、图标、排序

改这里：

- `data/ui/catalogs/technologies.json`

这里负责：

- `name`
- `description`
- `icon_key`
- `sort_order`
- `tags`

适用场景：

- 改中文文案
- 改图标
- 调整列表顺序

### 3.3 改科技树上的位置和连线

改这里：

- `data/ui/layouts/technology_tree.json`

这里负责：

- 节点 `technology_id`
- 节点标题和描述
- 节点坐标 `x/y`
- 节点尺寸
- 连线 `edges`

适用场景：

- 新增一个科技后，要把它放进科技树
- 调整科技树排版
- 调整前端显示的连线关系

### 3.4 科技改动时最容易漏掉的事

- 如果科技解锁建筑，`target_id` 必须对应 `data/content/buildings/buildings.json` 里的真实建筑 `id`
- 如果科技解锁配方，`target_id` 必须对应 `data/content/recipes/recipes.json` 里的真实配方 `id`
- 新增科技后，如果不改 `data/ui/layouts/technology_tree.json`，它可能在科技树界面里没有可见节点

## 4. 我想改生产配方，应该改哪里

### 4.1 改配方玩法规则

改这里：

- `data/content/recipes/recipes.json`

这里负责：

- `id`
- `building_id`
- `resource_inputs`
- `point_inputs`
- `work_amount`
- `base_progress`
- `outputs`

适用场景：

- 改消耗资源
- 改消耗点数
- 改生产速度
- 改产出资源/单位

最小示例：

```json
{
  "id": "farm_food",
  "building_id": "farm",
  "resource_inputs": {},
  "point_inputs": { "industry_output": 1 },
  "work_amount": 1,
  "base_progress": 1,
  "outputs": { "resources": { "food": 2 } }
}
```

### 4.2 改配方显示名、描述、图标、排序

改这里：

- `data/ui/catalogs/recipes.json`

这里负责：

- `name`
- `description`
- `icon_key`
- `sort_order`
- `tags`

### 4.3 配方改动时最容易漏掉的事

- `building_id` 必须对应真实建筑 `id`
- 如果 `outputs.units` 里写了单位 ID，这些单位必须已经存在
- 如果配方里引用了新的点数 key 或资源 key，必须先确认这些 key 已经存在于 `data/registry/points.json` 或 `data/registry/resources.json`
- 新增配方后，通常还要把它挂到对应建筑的 `recipe_ids` 和 `default_recipe_id` 上，否则建筑不会使用它

## 5. 我想改建筑，应该改哪里

### 5.1 改建筑玩法规则

改这里：

- `data/content/buildings/buildings.json`

这里负责：

- `id`
- `placement_kind`
- `building_scope`
- `required_resource_type`
- `resource_costs`
- `point_costs`
- `recipe_ids`
- `default_recipe_id`
- `explicit_effects`
- `modifier_effects`
- `max_hp`
- `takeover_mode`
- `tags`

适用场景：

- 调整建筑消耗
- 调整建筑可建位置
- 绑定或切换默认配方
- 调整耐久和标签

最小示例：

```json
{
  "id": "farm",
  "placement_kind": "resource_node",
  "building_scope": "out_of_city",
  "required_resource_type": "food",
  "resource_costs": { "wood": 1 },
  "point_costs": { "industry_output": 1 },
  "recipe_ids": ["farm_food"],
  "default_recipe_id": "farm_food",
  "explicit_effects": [],
  "modifier_effects": [],
  "max_hp": 80,
  "takeover_mode": "delayed",
  "tags": ["extraction"]
}
```

### 5.2 改建筑显示名、描述、图标、Prefab、排序

改这里：

- `data/ui/catalogs/buildings.json`

这里负责：

- `name`
- `description`
- `icon_key`
- `prefab_key`
- `sort_order`
- `tags`

适用场景：

- 改文案
- 改图标
- 让客户端用新的 `prefab_key`
- 调整建造菜单顺序

### 5.3 建筑改动时最容易漏掉的事

- `recipe_ids` 里的每个配方都必须真实存在
- `default_recipe_id` 必须存在，且必须包含在 `recipe_ids` 里
- `required_resource_type` 如果不是空字符串，就必须对应真实资源 key
- 新增建筑后，若它需要被科技解锁，记得同步到科技的 `explicit_effects`

## 6. 最常见的三种改法

### 6.1 只改现有数值

例如：

- 科技研究成本
- 建筑 HP
- 配方产出数量

通常只需要改：

- 对应的 `data/content/...`

如果名字、图标、排序不变，就不用改 `data/ui/...`。

### 6.2 新增一个科技

通常至少要改：

- `data/content/technologies/technologies.json`
- `data/ui/catalogs/technologies.json`
- `data/ui/layouts/technology_tree.json`

如果该科技会解锁建筑或配方，还要保证目标已经存在于：

- `data/content/buildings/buildings.json`
- `data/content/recipes/recipes.json`

### 6.3 新增一个建筑或配方

新增建筑通常至少要改：

- `data/content/buildings/buildings.json`
- `data/ui/catalogs/buildings.json`

新增配方通常至少要改：

- `data/content/recipes/recipes.json`
- `data/ui/catalogs/recipes.json`

如果建筑和配方是一组一起新增，通常要同时改：

- `data/content/buildings/buildings.json`
- `data/content/recipes/recipes.json`
- `data/ui/catalogs/buildings.json`
- `data/ui/catalogs/recipes.json`

并确认：

- 建筑的 `recipe_ids` 包含该配方
- 建筑的 `default_recipe_id` 指向该配方
- 如果它不是默认开放内容，科技里已经配置了解锁关系

## 7. 通用同步流程

改完作者源后，按这个顺序执行：

```bash
make data-validate
make data-gen
```

### 7.1 `make data-validate`

用途：

- 校验 JSON 是否符合 schema
- 校验跨文件引用是否正确

它会帮你发现这类问题：

- 配方引用了不存在的建筑
- 建筑引用了不存在的默认配方
- 科技解锁了不存在的建筑或配方
- 点数 key / 资源 key 写错

### 7.2 `make data-gen`

用途：

- 生成服务端 bundle
- 生成客户端本地静态目录

生成结果会写到：

- `data/generated/server/`
- `client/Assets/Resources/Data/`

## 8. 其他配置怎么找

如果你要改的不是科技、配方、建筑，可以先按下面的规则找：

### 8.1 改玩法规则

先看 `data/content/`

常见文件：

- `data/content/units/units.json`
- `data/content/policies/policies.json`
- `data/content/terrains/terrains.json`
- `data/content/rules/rules.json`
- `data/content/ministers/ministers.json`
- `data/content/maps/<map_id>/definition.json`

### 8.2 改显示名、描述、图标、排序

先看 `data/ui/catalogs/`

常见文件：

- `data/ui/catalogs/units.json`
- `data/ui/catalogs/policies.json`
- `data/ui/catalogs/terrains.json`
- `data/ui/catalogs/resources.json`
- `data/ui/catalogs/points.json`
- `data/ui/catalogs/maps/<map_id>.json`

### 8.3 改布局或纯界面顺序

先看 `data/ui/layouts/`

当前常见的是：

- `data/ui/layouts/technology_tree.json`

另外有一部分界面顺序不是手写 layout，而是生成时从 `sort_order` 推出来的。当前已知规则：

- 建筑菜单顺序来自 `data/ui/catalogs/buildings.json` 的 `sort_order`
- 配方顺序来自 `data/ui/catalogs/recipes.json` 的 `sort_order`

### 8.4 改资源 key、点数 key、版本元信息

先看 `data/registry/`

常见文件：

- `data/registry/resources.json`
- `data/registry/points.json`
- `data/registry/manifest.json`

当你要新增一个全新的资源或点数时，通常要同时维护：

- `data/registry/resources.json` 或 `data/registry/points.json`
- `data/ui/catalogs/resources.json` 或 `data/ui/catalogs/points.json`

## 9. 常见坑

### 9.1 手改生成产物

这是最常见的错误。

你在下面目录里看到的内容都是产物，不是作者源：

- `data/generated/server/`
- `client/Assets/Resources/Data/`

手改它们会被下次生成覆盖。

### 9.2 只改 content，不改 ui

这会导致：

- 服务端规则已经变了
- 但客户端名字、图标、排序、树布局还是旧的

如果你新增的是“可见内容”，通常都要同时检查对应的 `data/ui/...`。

### 9.3 新增了配方，但没有挂到建筑上

只在 `data/content/recipes/recipes.json` 里新增配方还不够。

你还要检查对应建筑的：

- `recipe_ids`
- `default_recipe_id`

### 9.4 新增了建筑，但没有解锁入口

如果这个建筑不是开局默认可用，就必须检查它的解锁路径是否已经存在，例如：

- 某个科技的 `unlock_building`
- 某个政策效果
- 其他显式效果

### 9.5 只改了科技目录，没有改科技树布局

`data/ui/catalogs/technologies.json` 只负责目录元数据，不负责树上的排版。

科技树上的节点和连线要去改：

- `data/ui/layouts/technology_tree.json`

### 9.6 引用了不存在的 key

这类错误最常见于：

- `resource_inputs`
- `resource_costs`
- `point_inputs`
- `point_costs`
- `required_resource_type`

遇到这种需求，先确认对应 key 是否已经存在于：

- `data/registry/resources.json`
- `data/registry/points.json`

## 10. 一张速查表

| 我想改什么 | 先改哪里 |
| --- | --- |
| 科技成本、前置、效果 | `data/content/technologies/technologies.json` |
| 科技名称、描述、图标、排序 | `data/ui/catalogs/technologies.json` |
| 科技树位置和连线 | `data/ui/layouts/technology_tree.json` |
| 配方输入、输出、工时 | `data/content/recipes/recipes.json` |
| 配方名称、图标、排序 | `data/ui/catalogs/recipes.json` |
| 建筑成本、放置、默认配方、HP | `data/content/buildings/buildings.json` |
| 建筑名称、图标、Prefab、排序 | `data/ui/catalogs/buildings.json` |
| 新增资源 key | `data/registry/resources.json`，通常再看 `data/ui/catalogs/resources.json` |
| 新增点数 key | `data/registry/points.json`，通常再看 `data/ui/catalogs/points.json` |
| 改静态数据版本信息 | `data/registry/manifest.json` |

## 11. 推荐工作流

每次改配置，建议按这个顺序做：

1. 先确认你改的是玩法规则、展示元数据，还是 registry。
2. 只改作者源文件。
3. 跑 `make data-validate`。
4. 跑 `make data-gen`。
5. 如果是可见内容，再进客户端确认名字、图标、排序、科技树排版是否符合预期。

如果你拿不准一个字段属于“玩法”还是“展示”，用这个判断：

- 会影响服务端裁决、消耗、产出、合法性判断的，放 `data/content/`
- 只影响名称、描述、图标、顺序、布局的，放 `data/ui/`

