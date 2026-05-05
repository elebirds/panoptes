# Research: manual-barracks-recipe-chain

- Query: 排查手动兵营 recipe 选择到产兵链路，确认 `MsgSetBuildingRecipe` 是否真的发送、手动与大臣是否共用后端路径、产兵是否依赖额外条件，并给出最可能剩余根因排序。
- Scope: mixed
- Date: 2026-05-05

## Findings

### 1. `MsgSetBuildingRecipe` 的手动客户端链是通的，不像之前的“客户端上下文断链”

- `RecipeSynthesisUiToolkitBinder.RequestRecipeSelection()` 会在拿到当前 `nodeId` 后直接调用 `_planningIntentService.SetBuildingRecipe(nodeId, recipeId)`。
  - 证据: `client/Assets/Scripts/Runtime/Presentation/Binders/UiToolkit/RecipeSynthesisUiToolkitBinder.cs:28-40`
- `PlanningIntentService.SetBuildingRecipe()` 会构造 `MsgSetBuildingRecipe` 并走注入的 `IClientMessageSender`。
  - 证据: `client/Assets/Scripts/Runtime/Core/Application/Services/PlanningIntentService.cs:17-24`
- `NetworkMessageSender.Send()` 直接转发到 `NetworkManager.Send()`；`TransportFrames.TryCreateClientFrame()` 会把 `MsgSetBuildingRecipe` 包成 `PlanningCommand.SetBuildingRecipe`。
  - 证据: `client/Assets/Scripts/Runtime/Core/Infrastructure/Network/NetworkMessageSender.cs:16-32`
  - 证据: `client/Assets/Scripts/Runtime/Core/Infrastructure/Network/NetworkManager.cs:164-180`
  - 证据: `client/Assets/Scripts/Runtime/Core/Infrastructure/Network/TransportFrames.cs:68-70`
- 现有测试已经覆盖“配方面板选择会发出 `MsgSetBuildingRecipe`”。
  - 证据: `client/Assets/Scripts/Tests/EditMode/Presentation/ManagementPanelUiToolkitBinderTests.cs:265-281`

结论:
- 这条链路和之前那类“UI 有动作但没真正发到消息发送层”的问题不同。
- 至少从代码和测试看，手动 recipe 选择会真正发消息。

### 2. 服务端对手动 recipe 的处理是：校验 -> 记录 planning draft -> 回 `MsgSetBuildingRecipeResult` + snapshot

- `handleSetBuildingRecipe()` 先 `evaluateRecipeCommand()`，成功后 `QueueRecipeSelection(...)`，再 `sendWithSnapshot(&pb.MsgSetBuildingRecipeResult{Success:true,...})`。
  - 证据: `server/internal/game/planning/build_recipe.go:54-77`
- `QueueRecipeSelection()` 只是把 draft upsert 到 `state.TurnRuntime.Planning.RecipeSelections`。
  - 证据: `server/internal/game/room.go:169-172`
  - 证据: `server/internal/domain/policy.go:112-123`
- planning 测试明确验证：重复设置 recipe 时，服务端保留最后一次 draft，并通过 planning snapshot 回给客户端。
  - 证据: `server/internal/game/planning/service_rules_test.go:517-538`

补充:
- 客户端对 `MsgSetBuildingRecipeResult` 的默认处理只在失败时发布 feedback；成功时没有额外本地状态推进逻辑。
  - 证据: `client/Assets/Scripts/Runtime/Core/Application/Stores/StoreMessageHydrator.cs:287-293`
  - 证据: `client/Assets/Scripts/Runtime/Core/Application/Handler/GameMessageHandler.cs:420-435`
- 这意味着“手动点击后 UI 上没明显成功提示”是可能的，但它不构成后端不产兵的根因。

### 3. 手动 recipe 与大臣 recipe draft 在后端最终走同一条路径

- 大臣默认意图 `applyDefaultRecipe()` 在校验成功后，同样调用 `room.QueueRecipeSelection(...)`。
  - 证据: `server/internal/game/planning/default_intent.go:122-133`
- 大臣 draft 序列化为命令信封时，recipe draft 也是 `MsgSetBuildingRecipe`。
  - 证据: `server/internal/game/query/minister_draft.go:95-101`
- 因此手动与大臣在“进入 planning draft”之前或之时没有后端分叉；二者都会落到 `TurnRuntime.Planning.RecipeSelections`，随后由 economy runner 统一结算。

结论:
- 如果大臣 recipe 能产兵、手动 recipe 不能，问题大概率不在 `MsgSetBuildingRecipe` 的发送和 planning 接收层。

### 4. recipe 真正生效要经过 resolving，且新建兵营本回合不会立刻开工

- 游戏命令只有在 `PhasePlanning` 才会进入 planning handler；真正 economy 结算发生在所有玩家 `Submit` 或超时后，由 turn coordinator 进入 `PhaseResolving`。
  - 证据: `server/internal/game/turn/command_handler.go:18-41`
  - 证据: `server/internal/game/turn/coordinator.go:68-90`
- 新建建筑的 build 结算在 economy `BuildStage`；`BuildingBuiltEvent.Apply()` 会把建筑状态写成 `disabled/pending_activation`，`OnlineOnTurn = state.Turn + 1`。
  - 证据: `server/internal/engine/economy/build.go:94-102`
  - 证据: `server/internal/event/production_building.go:34-43`
- 生命周期状态里，如果当前 turn 还没到 `OnlineOnTurn`，建筑会一直被视为 `disabled`。
  - 证据: `server/internal/domain/lifecycle.go:115-120`
- 集成 harness 也明确验证：turn 4 建兵营后还是 `disabled`，turn 5 才出现 `recipe_progressed`，turn 6 才出现 `recipe_completed`。
  - 证据: `server/internal/debug/harness_test.go:385-435`

结论:
- 如果“手动兵营无法造兵”指的是“刚造完立刻选 recipe，但本回合/未 submit/未到下回合没出兵”，这是符合现有后端设计的，不是 recipe 手动链路坏了。

### 5. 手动选 recipe 后，后续 recipe progress / completion / unit output 还有额外条件

- `RecipeSelectionStage` 会把 draft 再校验一次，然后产出 `RecipeSelectionChangedEvent`，把 `BuildingOperation.SelectedRecipeID` 和 `RequiredTurns` 写回运行态。
  - 证据: `server/internal/engine/economy/recipe_selection.go:21-48`
  - 证据: `server/internal/event/recipe.go:37-53`
- `runRecipeProgress()` 只处理已有 `BuildingOperationC` 的建筑，按 owner/recipe 优先级逐个推进。
  - 证据: `server/internal/engine/economy/recipe_progress.go:35-61`
- 单个建筑要推进 recipe，至少还要满足这些条件:
  - 建筑当前 turn 必须 operational，不能仍处于 `disabled/pending_activation`。
    - 证据: `server/internal/engine/economy/recipe_progress_entry.go:22-31`
  - 所属服务城市必须在线。
    - 证据: `server/internal/engine/economy/recipe_progress_entry.go:29-33`
  - recipe 必须仍解锁且合法。
    - 证据: `server/internal/engine/economy/recipe_progress_entry.go:34-43`
    - 证据: `server/internal/engine/economy/validation.go:91-133`
  - 必须有足够资源和点数；否则可能变成 `blocked`，常见 reason 是 `insufficient_resources` / `insufficient_points`。
    - 证据: `server/internal/engine/economy/recipe_progress_entry.go:45-71`
    - 证据: `server/internal/engine/economy/recipe_progress_entry.go:97-112`
- 真实内容里 `barracks` 默认 recipe 就是 `barracks_infantry`，且每次训练要 `food=1 + ore=1 + industry_output=1`，`work_amount=2`，`base_progress=1`。
  - 证据: `data/content/buildings/buildings.json` 中 `barracks.default_recipe_id = barracks_infantry`
  - 证据: `data/content/recipes/recipes.json` 中 `barracks_infantry`
- 新建建筑时如果配置了 `DefaultRecipeID`，`ecs.CreateBuilding()` 会自动附带默认 `BuildingOperation`；对兵营来说，手动 `SetBuildingRecipe` 不是开工必要条件。
  - 证据: `server/internal/ecs/factory.go:143-150`

结论:
- “接受了 recipe 但不产兵”更像是 resolve 阶段的条件未满足，而不是手动设置本身没进入后端。

### 6. 即使 recipe `completed`，单位产出也可能被静默吞掉

- `RecipeCompletedEvent.Apply()` 在产资源后，会尝试为每个 unit 调 `ResolveUnitSpawnPosition()`；如果找不到安全出生点，就 `continue`，不会报错，也不会补偿。
  - 证据: `server/internal/event/recipe.go:161-187`
- `ResolveUnitSpawnPosition()` 要求:
  - 候选格在 1..3 ring 内；
  - 该格无建筑、无单位、地形可通行；
  - 且从该格还能逃离 3 ring 外；
  - 同时本回合 `Planning.BuildOrders` 里的待建节点也会被当作保留格，阻止出生。
  - 证据: `server/internal/domain/spawn_resolver.go:15-37`
  - 证据: `server/internal/domain/spawn_resolver.go:40-52`
  - 证据: `server/internal/domain/spawn_resolver.go:85-135`
- 测试明确验证：没有安全出生位时，`RecipeCompletedEvent.Apply()` 会直接不产兵。
  - 证据: `server/internal/event/production_unit_test.go:80-116`

结论:
- 这是当前代码里最值得怀疑的“后端看起来完成了 recipe，但玩家看到没出兵”的静默失败点。

## Most Likely Remaining Problems

按概率排序:

1. **产兵出生位解析失败，单位被静默跳过**
   - 原因: `RecipeCompletedEvent.Apply()` 找不到安全出生点时不会报错；手动玩家更可能同时在兵营周围下待建草案或已有建筑/单位，触发 `ResolveUnitSpawnPosition()` 失败。
   - 关键证据: `server/internal/event/recipe.go:176-186`, `server/internal/domain/spawn_resolver.go:23-35`, `server/internal/event/production_unit_test.go:80-116`

2. **玩家观察时机不对：手动新建兵营本回合仍是 `pending_activation`，且 recipe 只有在 submit 后 resolving 才推进**
   - 原因: 新建兵营不会同回合立即产兵；必须至少过到下回合 resolving，且需要 submit。
   - 关键证据: `server/internal/event/production_building.go:37-43`, `server/internal/domain/lifecycle.go:115-120`, `server/internal/debug/harness_test.go:403-435`

3. **手动兵营所在城市或供应网络不满足 recipe 输入条件，导致 recipe blocked 或低效停滞**
   - 原因: `barracks_infantry` 需要 `food + ore + industry_output`；若服务城市离线、无 ore、无 industry point、或物流不可达，会卡在 progress 前。
   - 关键证据: `server/internal/engine/economy/recipe_progress_entry.go:29-71`, `server/internal/engine/economy/recipe_progress_entry.go:97-112`

4. **客户端成功反馈不明显，造成“点了但没生效”的错觉，但这更像显示问题，不是后端产兵根因**
   - 原因: `MsgSetBuildingRecipeResult` 成功时没有额外 hydrate，只在失败时发 feedback。
   - 关键证据: `client/Assets/Scripts/Runtime/Core/Application/Stores/StoreMessageHydrator.cs:287-293`

## Related Specs

- `.trellis/spec/backend/index.md`
- `.trellis/spec/frontend/index.md`

## Caveats / Not Found

- `python3 ./.trellis/scripts/task.py current --source` 在本机会话里没有成功返回当前任务，我按用户明确指定的 `.trellis/tasks/05-05-building-placement-movement-bugs/` 目录执行研究并写入。
- 本次只做静态排查，没有跑服务端/客户端复现，所以无法直接判定实际现场是卡在:
  - `MsgSetBuildingRecipeResult` 已成功但玩家没 submit，
  - `recipe_progressed/recipe_completed` 已发生但无出生位，
  - 还是 `blocked_reason=insufficient_resources/insufficient_points`。
- 现有代码里没有发现“手动 recipe 与大臣 recipe 在服务端走不同分支”的证据；如果现场确实只有手动失败、AI 成功，最值得优先抓的运行态字段是:
  - `Planning.RecipeSelections`
  - 节点 `building_status / building_reason / online_on_turn`
  - `BuildingOperation.selected_recipe_id / blocked_reason / progress_turns`
  - `recipe_completed` 后是否实际出现单位
  - 兵营周围 1..3 ring 是否有单位、建筑、待建草案阻塞出生
