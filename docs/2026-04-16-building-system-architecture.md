# 建筑系统架构（2026-04-16）

## 目标

本轮把建筑从“散落在 `ecs / economy / event / query / map` 的横切关注点”收口为独立主模块。

当前建筑主线已经按下面五层组织：

1. `building/rules`
2. `building/runtime`
3. `building/events`
4. `building/query`
5. `building/orchestration`

这里记录的是**重构后的当前真相**，不是未来设想。

## 1. 单一绑定真相

建筑与城市/服务城市的隶属关系，统一使用 `BuildingBindingComp` 表达：

- `Scope`
  - `city_core`
  - `in_city`
  - `out_of_city`
- `CityID`
- `ServiceCityID`

`BuildingComp` 只保留建筑本体属性：

- `Type`
- `HP`
- `MaxHP`
- `Owner`

旧的：

- `BuildingComp.CityID`
- `CityCoreComp`
- `ServiceCityComp`
- `FacilityBindingComp`

已经退出主线。

所有读取统一走：

- `building.Binding()`
- `building.ResolveCityID()`
- `building.ResolveServiceCityID()`
- `building.Scope()`

## 2. 规则层

建筑放置与建城规则统一收口在 `server/internal/building/rules.go`。

### 2.1 建造放置

`building.ValidatePlacement()` 统一处理：

- 建筑作用域是否合法
- 城市上下文是否合法
- 城市是否 online
- 节点地形是否可建造
- 节点是否满足 `placement_kind`

### 2.2 资源点开发

当前正式规则是：

- 己方辖区内资源点可直接开发
- 己方安全区内资源点可直接开发
- 前线资源点要求当回合己方单位独占驻守
- 多方混战时不能开发
- 不再额外要求“先占满 1 回合，再下一回合开发”

### 2.3 城市陷落后的建筑命运

`building.CapturedLifecycleForBuilding()` 是城市陷落后城内建筑命运的唯一规则真相：

- `defense / governance` 标签建筑 -> `ruined`
- 其他城内建筑 -> `pending_activation`
- `out_of_city` 建筑不参与城市陷落批处理

producer 不再额外提前发 `building_ruined` 来决定命运。

## 3. 运行态层

建筑运行态统一由：

- `building.RuntimeState()`
- `domain.BuildingLifecycleStateAtTurn()`

解释。

核心状态包括：

- `idle`
- `active`
- `blocked`
- `disabled`
- `contested`
- `takeover`
- `ruined`

`pending_activation` 不是单独状态枚举，而是：

- `disabled`
- `reason=pending_activation`
- `online_on_turn`

## 4. 编排层

建筑生命周期已经从 `economy` 拆出，成为 `TurnResolutionRunner` 的独立阶段：

1. `PlanningCommitStage`
2. `OrderFreezeStage`
3. `UnitResolutionStage`
4. `MapActionStage`
5. `BuildingStage`
6. `EconomyStage`

其中：

- `BuildingStage` 负责
  - 前线设施接管
  - 非主城城市陷落
  - 建筑运行态解释
- `EconomyStage` 只消费已经稳定下来的建筑状态
  - budget
  - research
  - build
  - recipe

## 5. 事件层

建筑状态写回仍统一通过 `event.Apply()` 完成。

关键事件：

- `BuildingBuiltEvent`
- `BuildingStatusChangedEvent`
- `FacilityTakeoverProgressedEvent`
- `FacilityTakeoverCompletedEvent`
- `CityCapturedEvent`
- `BuildingRuinedEvent`

当前约束：

- producer 负责判定并产出事件
- 真正的建筑 owner / binding / lifecycle 写回只发生在 `Apply()`

## 6. 查询与投影

`NodeView` 中和建筑有关的字段，统一由 binding + runtime + operation 推导：

- `building_status`
- `city_id`
- `service_city_id`
- `takeover_progress`
- `takeover_required`
- `operation`

查询入口仍在 `game/query/views.go`，但其建筑真相已经收口到 `building` 模块，不再自己拼接多份 `CityID` 来源。

## 7. 当前收益

这次重构完成后，建筑系统已经具备后续扩展的稳定落点：

- 新的放置规则 -> `building/rules`
- 新的建筑运行态解释 -> `building/runtime`
- 新的接管/陷落写回 -> `event + building rules`
- 新的建筑展示字段 -> `building/query`
- 新的建筑阶段行为 -> `building/orchestration`

也就是说，后续再扩展道路、改良、物流、更多设施接管规则时，不需要再把建筑逻辑塞回 `economy` 或 `ecs/query`。
