# C0c UI Toolkit 资产烟测门禁

C0c 的目标是继续把人工 Unity 调试后移。在手工打开 prefab、调整布局和检查交互手感前，先用 Unity EditMode batchmode 确认 UI Toolkit 管理界面的真实资产没有断线。

## 覆盖范围

门禁覆盖当前 C0a 已接入的手工 UI Toolkit 管理资产：

| 资产 | Prefab / UXML | 检查内容 |
|---|---|---|
| ManagementHost | `Resources/Prefabs/UI/ManagementHost` | prefab 可加载、binder 存在、UXML/USS 序列化引用正确、导航/概览命名元素存在 |
| BuildCatalog | `Resources/Prefabs/UI/BuildCatalog` | prefab 可加载、binder 存在、UXML/USS 序列化引用正确、目录根节点/空态/列表容器存在 |
| TechTree | `Resources/Prefabs/UI/TechTree` | prefab 可加载、binder 存在、共享管理面板命名元素存在 |
| PolicyFocus | `Resources/Prefabs/UI/PolicyFocus` | prefab 可加载、binder 存在、共享管理面板命名元素存在 |
| NationalLedger | `Resources/Prefabs/UI/NationalLedger` | prefab 可加载、binder 存在、共享管理面板命名元素存在 |
| RecipeSynthesis | `Resources/Prefabs/UI/RecipeSynthesis` | prefab 可加载、binder 存在、共享管理面板命名元素存在 |
| TurnSummary | `Assets/UI/Toolkit/Turn/TurnSummary.uxml` | UXML/USS 存在，UXML 暴露 binder 需要的命名元素 |

这里刻意检查真实 Resources prefab 的序列化引用，而不是只检查 binder fallback。这样可以尽早发现：

- prefab 缺失或移动后 Resources 路径失效；
- binder 组件被删错；
- `VisualTreeAsset` / `StyleSheet` 引用丢失或接到错误资产；
- UXML 中 `name` 合约被改坏，导致 binder 查不到元素。

## 运行方式

```bash
make c0c-check
```

该命令会：

1. 编译 `client/Panoptes.Tests.EditMode.csproj`；
2. 用 Unity batchmode 运行 `Panoptes.Tests.EditMode.Presentation.C0cUiToolkitAssetSmokeTests`；
3. 执行 `git diff --check`。

只想复跑 Unity 资产烟测时可以运行：

```bash
make c0c-check-unity
```

如果需要同时跑 C0b 协议 replay 和 C0c UI 资产烟测：

```bash
make c0-ui-check
```

## 边界

C0c 不替代人工 UI 审查。它不检查视觉美观、响应式布局、鼠标/键盘手感，也不启动完整游戏场景。它只负责在更早阶段守住“资产能加载、binder 能找到约定元素、序列化引用没断”的底线。
