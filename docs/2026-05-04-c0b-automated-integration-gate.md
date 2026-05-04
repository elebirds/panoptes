# C0b 自动化集成门禁

## 目标

C0b 的目标是把人工 Unity 调试尽量后移。进入手工检查 prefab、布局和交互手感前，先用自动化链路确认：

- 服务端能生成当前协议下的 `ServerFrame` protojson。
- 客户端生成的 C# proto 能解析这些帧。
- `MessageDispatcher -> StoreMessageHydrator -> Store -> ViewModel` 路径能消费真实形态的后端帧。
- C0a 管理界面的关键投影能从 hydrated read model 中拿到信息。

这不是视觉验收，也不是替代 Unity Editor 的最终检查；它是“先把接线错误挡在门外”的门禁。

## 实现

新增服务端 fixture 生成器：

```bash
cd server && go run ./cmd/c0bgatefixture -out ../client/Assets/Scripts/Tests/EditMode/Fixtures/C0b/server_frames.jsonl
```

生成内容是 JSONL，每行一个 `ServerFrame`。当前覆盖：

- 静态目录快照：资源、点数、建筑、配方、科技、制度、单位。
- `MsgGameInit`
- 第 1 回合 `MsgPlanningStart`
- 研究目标 `MsgPlanningSnapshot`
- `MsgResearchResult`
- 第 1 回合 `MsgGameSync`
- 第 2 回合 `MsgPlanningStart`

新增客户端 EditMode 测试：

```text
client/Assets/Scripts/Tests/EditMode/Presentation/C0bProtocolReplayGateTests.cs
```

测试会读取 fixture，使用 `JsonParser.Default.Parse<ServerFrame>()` 解析，然后通过 `MessageDispatcher.Dispatch()` 回放。静态目录由 `StaticCatalogStoreHydrator` 写入 `StaticCatalogStore`，游戏帧由 `StoreMessageHydrator` 写入对应 stores。

## 自动门禁命令

```bash
make c0b-check
```

该命令执行：

```text
1. 重新生成 C0b fixture
2. 检查 fixture 是否与仓库版本一致
3. 跑服务端 debug/codec 聚焦测试
4. 编译客户端 EditMode 测试项目
5. 运行 dotnet EditMode 测试项目
6. 运行过滤后的 Unity EditMode replay 测试
7. 执行 git diff --check
```

这里保留 `dotnet test` 是为了兼容当前已有门禁习惯，但真正执行 Unity NUnit 断言的是 `make c0b-check-unity`。如果只想快速复跑 replay 测试，可以直接运行：

```bash
make c0b-check-unity
```

如果协议或投影结构变化，先运行 `make c0b-fixture` 更新 fixture，再修复客户端 replay 测试。这能迫使“服务端输出字段变化”和“客户端 read model 变化”在同一次改动中被看见。

## 边界

- Presentation 仍然不得引用 `Panoptes.Protocol`。
- 客户端仍然只做展示和指令提交，不做规则推演或合法性判断。
- fixture 只是合同样本，不是作者源；游戏数值仍来自根 `data/` 作者源和服务端静态数据系统。
- UI Toolkit/uGUI 的实际 prefab 布局仍需要 Unity batchmode/import 和后续人工检查。
