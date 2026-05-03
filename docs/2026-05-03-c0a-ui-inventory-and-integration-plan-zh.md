# C0a UI 界面盘点与接入计划

日期：2026-05-03

## 目标

在进入 C0a 之前，先明确 Unity 客户端需要制作和接入哪些真实 UI 界面。这份盘点以最终客户端架构为标准：

```text
Store / Service -> ViewModel -> Binder -> 手工制作的 uGUI prefab 或 UI Toolkit UIDocument
```

这份文档的目的，是避免 C0a 开始时 UI 归属不清。C0a 应该把真实后端状态接到真实客户端界面上，而不是继续做架构清理，也不应该依赖运行时生成的 fallback UI。

## 当前状态摘要

- 运行时代码架构已经为主要 HUD 和管理面板建立了最终形态的 Store / ViewModel / Binder 切片。
- 大部分地图、HUD、世界空间 UI 已经有 uGUI prefab 资产。
- 当前已手工制作的 UI Toolkit 管理资产包括 `ManagementHost`、`BuildCatalog`、`RecipeSynthesis`、`TechTree`、`PolicyFocus`、`NationalLedger`；`TurnSummary` 仍是早期试点资产。
- `BuildCatalog`、`TechTree`、`RecipeSynthesis`、`PolicyFocus`、`NationalLedger` 已经有 ViewModel/Binder 和手工 UI Toolkit prefab；`MinisterReport` 仍依赖 composition 注册出来的 fallback `UIDocument` 树。
- `RecipeSynthesis` 已经有通向 `PlanningIntentService` 的命令路径。
- `BuildCatalog` 已经通过 `PlanningToolService` 进入建造放置模式。
- `TechTree`、`PolicyFocus` 已经完成行操作命令接线；`MinisterReport` 的行状态里已有操作按钮文案，但后续接受/拒绝/指令行为尚未定稿。
- `PolicyFocus`、`NationalLedger` 已经接入 `ManagementPanelVisibilityStore`；`MinisterReport` 尚未作为 C0a 交互面板接入。

## 已有 UI 资产

### uGUI / Prefab 资产

当前已有的运行时 UI prefab 包括：

| 资产 | 当前职责 |
|---|---|
| `client/Assets/Resources/Prefabs/UI/TokenHUD.prefab` | 规划令牌与阶段状态 HUD |
| `client/Assets/Prefabs/UI/ResourcePanel.prefab` | 资源 HUD 界面 |
| `client/Assets/Prefabs/UI/TrunPanel.prefab` | 回合/倒计时面板 |
| `client/Assets/Prefabs/UI/UnitInfoPanel.prefab` | 选中单位/建筑信息与直接操作 |
| `client/Assets/Resources/Prefabs/UI/GameChatPanel.prefab` | 局内聊天/表情面板 |
| `client/Assets/Resources/Prefabs/UI/SettlementTimeline.prefab` | 结算事件时间线 |
| `client/Assets/Resources/Prefabs/UI/TurnReportPanel.prefab` | 回合结算摘要 |
| `client/Assets/Resources/Prefabs/UI/GameOverOverlay.prefab` | 游戏结束结果遮罩 |
| `client/Assets/Resources/Prefabs/UI/BuildingConstructionOverlay.prefab` | 世界/HUD 建造进度浮层 |
| `client/Assets/Resources/Prefabs/UI/CityCoreHpBarOverlay.prefab` | 城市核心 HP 浮层 |
| `client/Assets/Resources/Prefabs/UI/CastleHPBar.prefab` | 城堡 HP 浮层 |
| `client/Assets/Prefabs/UI/ConfirmDialog.prefab` | 确认弹窗 |
| `client/Assets/Prefabs/UI/ErrorToast.prefab` | 错误/提示 toast |
| `client/Assets/Prefabs/UI/PlayerSlot.prefab` | 大厅玩家槽位 |

除非某个界面明确属于信息密集型管理 UI，否则 C0a 阶段这些界面应继续保留 uGUI。

### UI Toolkit 资产

当前已有的手工 UI Toolkit 资产：

| 资产 | 当前职责 |
|---|---|
| `client/Assets/UI/Toolkit/Turn/TurnSummary.uxml` | 回合摘要试点界面 |
| `client/Assets/UI/Toolkit/Turn/TurnSummary.uss` | 回合摘要样式 |

当前缺失的手工 UI Toolkit 资产：

| 需要的资产组 | 当前脚本支持 |
|---|---|
| 建筑目录 | `BuildCatalogViewModel`、`BuildCatalogUiToolkitBinder` |
| 科技树 | `TechTreeViewModel`、`TechTreeUiToolkitBinder` |
| 配方合成/生产 | `RecipeSynthesisViewModel`、`RecipeSynthesisUiToolkitBinder` |
| 大臣报告 | `MinisterReportViewModel`、`MinisterReportUiToolkitBinder` |
| 政策/国策 | `PolicyFocusViewModel`、`PolicyFocusUiToolkitBinder` |
| 国家账本 | `NationalLedgerViewModel`、`NationalLedgerUiToolkitBinder` |
| 管理面板宿主/导航 | 只有部分 `ManagementPanelVisibilityStore`；还没有手工宿主 |

## UI 界面目录

### 游戏外壳与会话界面

| 界面 | 技术 | 已有资产 | 展示信息 | 玩家操作 | C0a 状态 |
|---|---|---|---|---|---|
| 登录 | uGUI prefab / 场景 | 已有脚本和场景 | 用户名/密码、认证错误、加载状态 | 登录、注册 | 保留现状；不是 C0a 重点 |
| 大厅/房间 | uGUI prefab / 场景 | 已有脚本和 `PlayerSlot` prefab | 房间、玩家、准备状态、bot | 创建/加入/离开/准备/加 bot/开始 | 保留现状；只有阻塞 C0a 流程时才修 |
| 加载/错误 | uGUI prefab | `ErrorToast`、`ConfirmDialog`、LoadingOverlay 脚本 | 错误、进度、确认文案 | 确认、取消 | 保留 uGUI |

### 地图、HUD 与世界空间 UI

这些界面与场景、空间位置或高频交互强绑定，C0a 应继续使用 uGUI / prefab。

| 界面 | 技术 | 已有资产/脚本 | 展示信息 | 玩家操作 | C0a 状态 |
|---|---|---|---|---|---|
| 地图渲染 | 场景 GameObject + prefab | `MapRenderer`、地图 prefab | 地形、可见节点、道路、建筑、单位、战争迷雾 | 选择节点/单位、预览移动/建造 | 已是最终架构脚本路径；C0a 直接使用 |
| 规划输入浮层 | 场景/prefab uGUI + 世界对象 | `MapPlanningInputController`、`Planning/Input`、反馈 presenter | 移动路径、建造 ghost、领土高亮、攻击范围 | 选择目标节点/单位，隐式确认地图意图 | 已有路径；需要用真实后端同步验证 |
| 资源 HUD | uGUI prefab | `ResourceHUD`、`ResourceHudViewModel` | 资源、点数、图标行、变化提示 | 打开科技树 | 已有；保留 uGUI |
| 令牌 HUD | uGUI prefab | `TokenHUD`、`TokenHudViewModel` | 剩余令牌、阶段、已提交/结算中状态 | 无 | 已有；保留 uGUI |
| 回合 HUD | uGUI prefab / 场景面板 | `TurnHUD` | 回合数、倒计时、当前是否可操作 | 提交回合 | 已有；保留 uGUI |
| 单位信息 | uGUI prefab | `UnitInfoPanelController`、`UnitInfoViewModel`、`UnitInfoUguiBinder` | 选中单位/建筑名称、描述、HP、规划摘要、头像、可用操作 | 移动、攻击、坚守、冲锋、上下文操作按钮 | 已有；保留 uGUI |
| 建造进度浮层 | uGUI/世界浮层 prefab | `BuildingConstructionOverlayController` | 建造/生产进度、建筑状态 | 无 | 已有；保留 uGUI |
| 城市/城堡 HP 条 | 世界空间 uGUI prefab | `CityCoreHPBar`、`CastleHPBar`、相关 overlay | HP 与受损状态 | 无 | 已有；保留 uGUI |
| 聊天面板 | uGUI prefab | `GameChatPanelController` | 最近聊天/表情记录 | 发送表情 | 已有；保留 uGUI |
| 游戏结束遮罩 | uGUI prefab | `GameOverOverlay` | 胜者、败者、原因、叙事文本 | 如果 prefab 提供，可做赛后导航 | 已有；保留 uGUI |

### 管理 UI

这些界面是信息密集、列表/表格/树状结构导向的界面，应使用 UI Toolkit，并通过手工 UXML/USS 与显式 Binder 接入。

| 界面 | 技术 | 当前脚本支持 | 展示信息 | 玩家操作 | C0a 状态 |
|---|---|---|---|---|---|
| 管理宿主/导航 | UI Toolkit | `ManagementHostUiToolkitBinder`、`NationalOverviewViewModel`、`Prefabs/UI/ManagementHost` | 当前面板、概览、共享面板入口、关闭状态 | 打开/关闭/切换面板 | C0a 已完成 |
| 回合摘要 | UI Toolkit | `TurnSummaryViewModel`、`TurnSummaryUiToolkitBinder`、已有 UXML/USS | 回合、阶段、令牌、可见节点、已知单位、最近 planning-start 事件 | 只读 | 试点已存在；后续可整合进管理宿主 |
| 国家概览 | UI Toolkit | `NationalOverviewViewModel`、`ManagementHostUiToolkitBinder`、`Prefabs/UI/ManagementHost` | 简明状态：回合、阶段、资源、单位、城市、当前国策/研究 | 初期只读 | C0a 已完成 |
| 国家账本 | UI Toolkit | `NationalLedgerViewModel`、`NationalLedgerUiToolkitBinder`、`Prefabs/UI/NationalLedger` | 总览计数、资源、目录数量 | 只读 | C0a 已完成：手工资产、prefab composition、可见性 |
| 建筑目录 | UI Toolkit | `BuildCatalogViewModel`、`BuildCatalogUiToolkitBinder`、`Prefabs/UI/BuildCatalog` | 建筑分组、名称、描述、放置类型、pending 状态 | 选择建筑，进入地图放置模式 | C0a 已完成：手工资产和命令路径 |
| 科技树 | UI Toolkit | `TechTreeViewModel`、`TechTreeUiToolkitBinder`、`Prefabs/UI/TechTree` | 分支、阶级、成本、描述、计划研究状态 | 设置研究目标 | C0a 已完成：手工资产和命令接线 |
| 配方合成/生产 | UI Toolkit | `RecipeSynthesisViewModel`、`RecipeSynthesisUiToolkitBinder`、`Prefabs/UI/RecipeSynthesis` | 选中建筑的配方、工作量/基础进度、预览/选中状态 | 设置建筑配方 | C0a 已完成：手工资产和命令路径 |
| 政策/国策 | UI Toolkit | `PolicyFocusViewModel`、`PolicyFocusUiToolkitBinder`、`Prefabs/UI/PolicyFocus` | 国策选项、制度政策、激活时机、计划状态 | 采用国策、设置制度 loadout | C0a 已完成：手工资产、可见性、命令接线 |
| 大臣报告 | UI Toolkit | `MinisterReportViewModel`、`MinisterReportUiToolkitBinder` | 按角色分组的大臣草案、摘要、理由、状态 | 审阅；后续接受/拒绝/指令 | C0a 可先只读或隐藏 |
| 回合报告/结算复盘 | 长期可用 UI Toolkit；当前已有 uGUI | uGUI `TurnReportPanel`、`SettlementTimeline`；UI Toolkit `TurnSummary` | 结算计数、事件、警告、生产/建造结果 | 只读 | C0a 可保留现有 uGUI，后续再整合 |

## C0a 必要 UI 工作

### 要让 C0a 真正可用，必须先完成

1. 制作手工 **Management UI Host**。
   - 推荐技术：UI Toolkit。
   - 职责：面板外壳、tab/侧边栏、关闭行为、共享面板区域。
   - 它应该成为第一个替代 `RegisterComponentOnNewGameObject` fallback 的真实 UI 资产。

2. 至少制作一个生产级 UI Toolkit 管理面板。
   - 推荐第一个面板：`NationalOverview` 或 `TurnSummary`。
   - 原因：命令风险低，适合验证 Store/ViewModel/Binder 与真实服务端状态的接入。

3. 明确管理面板如何从现有 uGUI HUD 打开。
   - `ResourceHUD` 已经可以打开 `TechTree`。
   - 城市/建筑上下文操作已经可以打开 `BuildCatalog` 和 `RecipeSynthesis`。
   - C0a 仍需要一个稳定的全局入口，用于 `NationalOverview`、`NationalLedger`、`PolicyFocus`，以及后续 `MinisterReport`。

4. 扩展 `ManagementPanelVisibilityStore`。
   - 已完成：`TurnSummary`、`NationalOverview`、`NationalLedger`、`PolicyFocus` 已纳入管理面板可见性。
   - 后续可选：`MinisterReport` 需要等大臣报告产品行为定稿后再纳入。

5. 接上缺失的行操作命令。
   - 已完成：`TechTree` 行操作调用 `GameIntentService.SetResearchTarget`。
   - 已完成：`PolicyFocus` national 行调用 `GameIntentService.SetPolicy`，institution 行调用 `GameIntentService.SetInstitutionLoadout`。
   - 后续：`MinisterReport` 目前建议只读或隐藏；接受/拒绝应等大臣产品需求重新打开后再做。

### 第一段 C0a 不需要做

- 删除所有旧 uGUI HUD prefab。
- 用 UI Toolkit 重写地图/世界空间 UI。
- 实现完整大臣交互。
- 做最终版科技树图布局。第一版分组列表可以接受。
- 使用 UI Toolkit 自动 data binding。继续保持显式 Binder 渲染。

## 推荐 C0a 顺序

### C0a-0：制作管理宿主

交付物：

- `client/Assets/UI/Toolkit/Management/ManagementHost.uxml`
- `client/Assets/UI/Toolkit/Management/ManagementHost.uss`
- `client/Assets/Resources/Prefabs/UI/ManagementHost.prefab`
- `ManagementHostUiToolkitBinder` 或等价场景组件，用于切换 `ManagementPanelVisibilityStore`

展示信息：

- 当前激活面板标题
- tab/侧边栏入口
- 关闭按钮

玩家操作：

- 打开、切换、关闭管理面板

### C0a-1：国家概览 / 回合摘要

交付物：

- 可以直接使用现有 `TurnSummary` 作为最小验证界面。
- 如果希望第一屏更像产品界面，则新增 `NationalOverviewViewModel`，组合资源、回合、单位、城市、研究、政策等状态。

展示信息：

- 回合与阶段
- 剩余令牌
- 资源/点数快照
- 可见节点 / 已知单位 / 城市数量
- 当前或计划中的研究目标
- 当前或计划中的国策
- 最近结算/规划事件

玩家操作：

- 初期只读
- 可选按钮：打开 `TechTree`、`PolicyFocus`、`NationalLedger`

### C0a-2：建筑目录与配方合成

交付物：

- 建筑目录和配方合成的手工 UI Toolkit 资产。
- 用具名 UXML 元素替代 fallback tree 依赖。

展示信息：

- 按放置类型分组的建筑目录
- 建筑描述和 pending 状态
- 选中建筑的配方列表
- 配方工作量/基础进度，以及选中/预览状态

玩家操作：

- 进入建筑放置模式
- 选择建筑配方

### C0a-3：科技树

状态：已完成。当前实现提供 `TechTree.uxml` / `TechTree.uss` 与 `Prefabs/UI/TechTree`，并通过 `GameIntentService.SetResearchTarget` 发送研究目标命令。

交付物：

- 手工 UI Toolkit 科技列表/科技树。
- 行操作接入 `GameIntentService.SetResearchTarget`。

展示信息：

- 分支
- 阶级
- 研究成本
- 描述
- 当前计划研究目标
- 当 Store 暴露后，展示已完成/已激活/待激活状态

玩家操作：

- 设置研究目标

### C0a-4：政策 / 国策

状态：已完成。当前实现提供 `PolicyFocus.uxml` / `PolicyFocus.uss` 与 `Prefabs/UI/PolicyFocus`，接入 `ManagementPanelVisibilityStore`，并将国策行转发到 `SetPolicy`、制度行转发到 `SetInstitutionLoadout`。

交付物：

- 手工 UI Toolkit 政策面板。
- 接入可见性。
- 接入命令。

展示信息：

- 国策候选
- 制度政策候选
- 当前激活/计划状态
- 激活时机
- 当目录数据暴露时，展示优先级/物流影响摘要

玩家操作：

- 采用国策
- 设置制度 loadout

### C0a-5：国家账本

状态：已完成。当前实现提供 `NationalLedger.uxml` / `NationalLedger.uss` 与 `Prefabs/UI/NationalLedger`，接入 `ManagementPanelVisibilityStore`，保持只读展示。

交付物：

- 手工 UI Toolkit 账本面板。
- 接入可见性。

展示信息：

- 资源与点数
- 地图/节点/单位数量
- 建筑/配方/科技/政策/单位目录数量
- 后续扩展：按城市、按网络的摘要

玩家操作：

- 只读

### C0a-Later：大臣报告

交付物：

- 手工 UI Toolkit 大臣报告。
- 决定审阅/接受/拒绝的产品行为。

展示信息：

- 大臣角色
- 草案标题
- 摘要
- 理由
- 状态

玩家操作：

- C0a：只读或隐藏
- 后续：通过 `MinisterCommandService` 接受/拒绝/下达指令

## 直接建议

C0a 从以下内容开始：

1. `ManagementHost`
2. 基于现有 Store 的 `NationalOverview`
3. 把已有 `TurnSummary` 作为 authored UI 接入到 host 内

然后再进入交互型管理界面：

1. `BuildCatalog`
2. `RecipeSynthesis`
3. `TechTree`
4. `PolicyFocus`

这样团队可以先稳定 UI Toolkit 的制作与接入工作流，再引入更多命令型面板。

## 本盘点的验收标准

- [x] 已列出现有 UI 资产。
- [x] 已列出需要制作的 UI 界面。
- [x] 每个界面都明确归属：uGUI prefab 或 UI Toolkit。
- [x] 每个界面都列出展示信息和玩家操作。
- [x] C0a 顺序明确。
- [x] 当前已知缺口已记录。
