# C0e PlayMode 装配启动烟测门禁

C0e 补上 C0d 没覆盖的一层：真实 PlayMode 启动时，runtime bootstrap、`ProjectLifetimeScope`、加载 `Game` 场景后的 `GameLifetimeScope` 是否能实际 Build，并 resolve 当前最终架构的关键 Store、Service、ViewModel、Binder。

## 覆盖范围

`C0ePlayModeCompositionBootstrapTests` 会在 Unity PlayMode batchmode 中检查：

- runtime bootstrap 会创建唯一 `ProjectLifetimeScope`；
- project scope 能 resolve `AppManager`、`IClientMessageSender`、`StaticCatalogStore`；
- `Game` 场景在 build settings 中可加载；
- `GameLifetimeScope` 会作为 project scope 的 child scope 构建；
- game scope 能 resolve C0a 管理界面和地图/HUD 依赖的关键 Store、command Service、ViewModel、UI Toolkit Binder、`TokenHUD`。

## 运行方式

```bash
make c0e-check
```

该命令会：

1. 编译 `client/Panoptes.Tests.EditMode.csproj`，先捕捉共享 Runtime 编译问题；
2. 用 Unity batchmode 运行 PlayMode 过滤测试；
3. 执行 `git diff --check`。

需要同时跑 C0b/C0c/C0d/C0e 时：

```bash
make c0-ui-check
```

## 边界

C0e 不登录、不连接服务器、不推进回合、不校验视觉像素，也不证明所有按钮流程可用。它只证明当前最终装配根在真实 PlayMode 生命周期下可以启动、加载 Game 场景，并解析关键对象。
