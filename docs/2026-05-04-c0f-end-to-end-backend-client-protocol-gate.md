# C0f 后端到 Unity 协议端到端门禁

## 定位

C0f 是 C0 自动化链路的最后一段协议门禁。它验证的不是单个 UI 面板，也不是单个 Store，而是后端真实入口输出的 Transport V2 `ServerFrame`，能否被 Unity 当前的 Reactive Presentation Architecture 正确回放和吸收。

## 覆盖范围

- HTTP 注册和登录。
- WebSocket JWT 鉴权连接。
- Lobby 创建房间、准备、开始游戏。
- Game bootstrap：静态目录 manifest、section chunk、sync complete、config、game init、planning start。
- Planning 指令：设置研究目标并提交回合。
- Resolution 输出：研究完成、下一回合科技激活、部长/规则草案生成建造建议。
- Unity EditMode 回放：StaticCatalogStore、GameStateStore、PlanningDraftStore、TurnStore、TechTreeViewModel、BuildCatalogViewModel、NationalOverviewViewModel。

## 设计决策

C0f fixture 生成器使用 Go `httptest` 启动真实 HTTP/WebSocket/lobby/game 装配，但使用内存 UserStore/LobbyStore 和确定性 `research_unlock_build` 场景。这样既走生产协议入口，又避免外部 Postgres/Redis 状态导致 fixture 不稳定。

Docker 可以保持开启，便于人工调试真实 server；但 C0f 自动门禁不依赖 Docker。后续如需增加“真实 docker-compose server smoke”，应作为单独门禁追加，而不是让 C0f fixture 受本机数据库状态影响。

## 运行方式

```bash
make c0f-check
```

完整 C0 UI 自动化链路：

```bash
make c0-ui-check
```

## 维护规则

- 修改后端协议行为后，运行 `make c0f-fixture` 重新生成 fixture。
- fixture 变更必须与对应后端/客户端断言一起提交。
- Unity 测试只回放 `ServerFrame`，不直接调用 `NetworkManager`。
- C0f 不修改 proto 源或生成文件。
