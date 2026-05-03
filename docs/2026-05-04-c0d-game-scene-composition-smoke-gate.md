# C0d Game 场景装配烟测门禁

C0d 延续 C0b/C0c 的目标：继续把人工 Unity 调试后移。C0b 保护后端帧到 Store/ViewModel 的协议 replay，C0c 保护 UI Toolkit 手工资产，C0d 保护真实 `Game.unity` 场景和 composition 注册的 Resources prefab。

## 覆盖范围

`C0dGameSceneCompositionSmokeTests` 会在 Unity EditMode batchmode 中检查：

- `Assets/Scenes/Game.unity` 可以被 Unity 打开；
- Game 场景只序列化一个 `GameLifetimeScope`，且对象名为 `Game Composition`；
- `ProjectLifetimeScope` 不被序列化进 Game 场景，继续由 bootstrap 创建；
- Game 场景存在 `GameSceneController`、`MapRenderer`、`MapPlanningInputController`、`SettlementPlaybackController`、`CinemachineMapCameraController`、`ResourceHUD`、`TurnHUD`、`UnitInfoPanelController`、`CityCoreBuildingActionRegistrar`、`SettlerUnitActionRegistrar`；
- `ClientCompositionInstaller.RegisterGame` 依赖的 Resources prefab 路径能加载，并且 prefab 上有目标组件。

## 运行方式

```bash
make c0d-check
```

该命令会：

1. 编译 `client/Panoptes.Tests.EditMode.csproj`；
2. 用 Unity batchmode 运行 `Panoptes.Tests.EditMode.Composition.C0dGameSceneCompositionSmokeTests`；
3. 执行 `git diff --check`。

只想复跑 Unity 场景装配烟测时可以运行：

```bash
make c0d-check-unity
```

需要同时跑 C0b/C0c/C0d 时：

```bash
make c0-ui-check
```

## 边界

C0d 不进入 PlayMode，也不证明所有 VContainer 注入链路都已运行。它只守住更靠前的资产/场景底线：场景能打开、关键 scene component 存在、composition 所需 prefab 路径没有断。
