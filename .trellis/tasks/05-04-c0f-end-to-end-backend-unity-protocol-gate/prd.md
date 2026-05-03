# C0f End-to-End Backend Unity Protocol Gate

## 背景

C0a-C0e 已经覆盖了管理 UI 资产、Store/ViewModel 投影、GameScene composition、PlayMode bootstrap 等前端侧风险。当前剩余风险是：后端真实 HTTP/WebSocket/lobby/game 链路输出的 Transport V2 `ServerFrame`，能否被 Unity 客户端当前 Store/ViewModel 架构稳定回放和吸收。

## 目标

建立一个可重复运行的 C0f 门禁：

- 从真实后端入口产生 fixture：HTTP 注册/登录、WebSocket 鉴权连接、lobby 创建/准备/开始游戏、game 静态目录同步、planning 指令、turn submit。
- 将后端输出固化为 JSONL `ServerFrame` fixture，并要求 fixture 生成确定性。
- Unity EditMode 回放该 fixture，验证 StaticCatalog、GameState、PlanningDraft、Turn、管理面板 ViewModel 的关键状态。
- 接入 `make c0-ui-check`，成为 C0 自动化门禁的一部分。

## 非目标

- 不做人工 prefab/视觉调试。
- 不启动真实 Unity 场景。
- 不依赖外部 Postgres/Redis 状态。
- 不在客户端补业务合法性校验。
- 不修改 proto 或生成代码。

## 验收标准

- `make c0f-check` 可独立通过。
- `make c0-ui-check` 包含 C0f。
- C0f fixture 覆盖 `MsgClientRuntimeConfig`、lobby room flow、`MsgStaticCatalogManifest`、section chunk sync、`MsgGameInit`、`MsgPlanningStart`、`MsgResearchResult`、`MsgGameSync`、下一回合 `MsgPlanningStart`。
- Unity 测试能断言研究目标、科技激活、farm 解锁后的建造草案、国家概览/科技树/建造目录 ViewModel 投影。
- 文档说明 C0f 的定位、运行方式和与 C0a-C0e 的关系。
