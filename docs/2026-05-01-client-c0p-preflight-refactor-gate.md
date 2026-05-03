# Panoptes C0p 客户端脚本预重构门禁

> 日期：2026-05-01
> 范围：C0a 前置客户端脚本债务清理
> 结论：通过静态门禁与 Unity 脚本导入/编译门禁；EditMode XML 未产出，需在可用 Unity TestRunner 环境复跑

## 1. 目标

C0p 的目标不是生成最终 UI prefab，而是把客户端脚本整理成更适合人工 prefab 绑定、后续 C0a 接入真实后端状态的结构。所有改动保持客户端纯展示层约束：Presentation 不直接使用 Protocol，UI 不直接调用 NetworkManager。

## 2. 完成内容

| 阶段 | 结果 |
|---|---|
| C0p0 护栏与基线 | 新增静态边界测试，覆盖 Presentation/Protocol、UI/NetworkManager 和高风险文件行数基线 |
| C0p1 prefab-ready 生命周期 | `GameSceneController` 改用 `SceneObjectFinder`；`ResourceHUD`、`TurnHUD` 用 `EventSubscriptionBag` 收口按钮/事件绑定 |
| C0p2 UI panel 拆分 | `BuildCommandPanel` 列表渲染拆到 `BuildCommandListRenderer`；`RecipeSynthesisPanel` 渲染项注册/清理拆到 `RecipeSynthesisRenderedItemRegistry` |
| C0p3 Planning Input | move/deploy helper 从 `MapPlanningInputController` 拆入 `Presentation/Planning/Input/Modes` |
| C0p4 Map Rendering | 地图源解析、camera context、render token helper 从 `MapRenderer` 拆出，`MapRenderer` 保持 facade |
| C0p5 Unit / Action | `SquadUnitVisualController` renderer budget 拆出；`CityCoreBuildingActionRegistrar` action resolution 拆出 |
| C0p6 GameStateCache | 只读资源/点数/字符串列表 snapshot helper 从 `GameStateCache` 拆出，不引入客户端规则判断 |

## 3. 行数变化

| 文件 | C0p 前 | C0p 后 |
|---|---:|---:|
| `MapPlanningInputController.cs` | 3376 | 3308 |
| `MapRenderer.cs` | 2014 | 1514 |
| `BuildCommandPanel.cs` | 1633 | 1497 |
| `RecipeSynthesisPanel.cs` | 1392 | 1377 |
| `CityCoreBuildingActionRegistrar.cs` | 1063 | 896 |
| `SquadUnitVisualController.cs` | 1788 | 1675 |
| `GameStateCache.cs` | 1542 | 1352 |

## 4. 门禁结果

```text
git diff --check
PASS

rg "Panoptes\\.Protocol" client/Assets/Scripts/Runtime/Presentation
PASS: no matches

rg "NetworkManager\\.Instance" client/Assets/Scripts/Runtime/Presentation/UI
PASS: no matches

git status --porcelain -- server/internal/gen/proto client/Assets/Scripts/Protocol
PASS: no generated protocol changes

Unity 6000.4.1f1 batchmode script import/compile
PASS: exited 0, no error CS entries in log
```

Unity TestRunner did not produce `/tmp/panoptes-c0p-editmode-results.xml` in this shell run, so EditMode test result XML remains a follow-up gate on a fully interactive/licensed Unity environment.

## 5. C0a 入口判断

C0a 可以开始。当前客户端已经具备更明确的 facade/helper 边界：人工 prefab 继续绑定现有 MonoBehaviour，新增后端状态接入优先喂给 Core cache/DTO，再由 Presenter/Binder/helper 更新表现层。
