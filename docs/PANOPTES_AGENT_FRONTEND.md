# Panoptes — 前端开发指南（Agent版）

> Turn V2 覆盖说明：凡与统一 `planning/resolving` 前端消息流、缓存模型、UI 结构冲突之处，以 `docs/TURN_V2_REFACTOR_PLAN.md` 为准。
> 本文档是Unity客户端开发的唯一权威参考。
> 客户端是纯展示层：不包含任何游戏逻辑，不做任何合法性校验。
> 所有状态以服务端推送为准，客户端只负责渲染、输入收集、动画播放。
> Agent开发时以本文档为准，不得自行更改已定义的结构、命名、消息类型。

---

## 目录

1. 项目概述
2. 技术栈与依赖
3. 目录结构
4. 架构原则
5. 消息协议
6. 场景结构
7. 核心组件
8. UI系统
9. 网络层
10. 状态缓存
11. 动画系统
12. 开发顺序

---

## 1. 项目概述

**项目代号**：Panoptes
**Unity仓库名**：`panoptes-client`
**Unity版本**：6000.4.1f1
**渲染管线**：URP（Universal Render Pipeline）
**目标平台**：PC（Windows/Mac）

客户端职责：
```
✅ 展示服务端推送的游戏状态
✅ 收集玩家输入，原样打包发给服务端
✅ 按服务端下发的事件序列播放动画
✅ 流式显示部长汇报文字
❌ 不做任何游戏逻辑计算
❌ 不做任何操作合法性校验
❌ 不维护本地游戏状态（只维护展示缓存）
```

---

## 2. 技术栈与依赖

### Unity Package Manager依赖

```
com.unity.textmeshpro          TextMeshPro（UI文字）
com.unity.ugui                 uGUI（UI系统）
com.unity.inputsystem          新版输入系统
com.unity.modules.uielements   UI Toolkit runtime（信息密集型面板）
```

### 批准的第三方依赖（Unity Package Manager）

```
VContainer                    生命周期与依赖注入
  包名：jp.hadashikick.vcontainer
  版本：1.17.0
  来源：https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0
  用途：ProjectLifetimeScope / GameLifetimeScope、Store/Service/ViewModel 注入

R3                            响应式状态传播
  包名：com.cysharp.r3
  版本：1.3.0
  来源：https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#1.3.0
  核心程序集：R3 NuGet 1.3.0 的 netstandard2.1 `R3.dll`
  运行依赖：Microsoft.Bcl.TimeProvider 8.0.0、Microsoft.Bcl.AsyncInterfaces 8.0.0、System.Threading.Channels 8.0.0、System.Runtime.CompilerServices.Unsafe 6.0.0、System.ComponentModel.Annotations 5.0.0
  导入位置：client/Assets/Plugins/
  用途：Store / ViewModel 的只读 reactive state

UniTask                       Unity 异步流程
  包名：com.cysharp.unitask
  版本：2.5.10
  来源：https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10
  用途：登录、连接、catalog 加载、请求响应、场景初始化
```

### 第三方插件（手动导入Assets/Plugins）

```
NativeWebSocket                WebSocket客户端
  来源：https://github.com/endel/NativeWebSocket
  版本：最新稳定版
  说明：支持PC/WebGL/移动端

Google.Protobuf                Protobuf C#运行时
  来源：NuGet或直接放DLL
  版本：与服务端protobuf版本一致
```

### 不引入的插件（明确禁止）

```
❌ Zenject（依赖注入；统一使用 VContainer）
❌ UniRx（响应式；统一使用 R3）
❌ Photon/Mirror（网络框架，用NativeWebSocket代替）
❌ DOTween（动画依赖暂不批准；继续使用现有 Unity 动画/Coroutine/UniTask 流程）
❌ 任何付费插件
```

### 生成代码

```
Assets/Scripts/Protocol/          从服务端 buf generate 生成的 C# 代码
                                  不手动修改，与服务端 proto 同步
```

---

## 3. 目录结构

```
panoptes-client/
├── Assets/
│   │
│   ├── Scripts/
│   │   │
│   │   ├── Protocol/                  # Panoptes.Protocol.asmdef（自动生成，禁止手动修改）
│   │   │   ├── Common.cs
│   │   │   ├── Auth.cs
│   │   │   ├── Lobby.cs
│   │   │   ├── GameState.cs
│   │   │   ├── Domestic.cs
│   │   │   ├── Combat.cs
│   │   │   ├── Minister.cs
│   │   │   ├── DataCatalog.cs
│   │   │   └── MapCatalog.cs
│   │   │
│   │   ├── Runtime/
│   │   │   ├── Core/                  # Panoptes.Core.asmdef
│   │   │   │   ├── Foundation/
│   │   │   │   │   ├── Domain/        # DTO（NodeDto/UnitDto/ResourceDto/SettlementDto/...）
│   │   │   │   │   └── Events/        # GameEvents（仅暴露 DTO）
│   │   │   │   ├── Infrastructure/
│   │   │   │   │   ├── Network/       # NetworkManager/MessageDispatcher/NetworkMessageSender
│   │   │   │   │   ├── Mapper/        # NodeMapper/UnitMapper/MinisterMapper/SettlementMapper
│   │   │   │   │   ├── Service/       # AuthService/LobbyService/SessionManager
│   │   │   │   │   └── Debug/         # 调试组件
│   │   │   │   └── Application/
│   │   │   │       ├── Cache/         # GameStateCache/StaticCatalogCache/RoomCache
│   │   │   │       ├── Handler/       # GameMessageHandler/LobbyMessageHandler
│   │   │   │       ├── Services/      # GameIntentService/PlanningIntentService/MinisterCommandService
│   │   │   │       └── App/           # AppManager/SceneLoader/Config
│   │   │   │
│   │   │   └── Presentation/          # Panoptes.Presentation.asmdef
│   │   │       ├── Map/
│   │   │       │   └── InputAdapter/  # Pointer/UI hit-test/raycast 到 NodeView/UnitView
│   │   │       ├── Planning/
│   │   │       │   ├── Feedback/      # 服务端 preview/result 的展示文案
│   │   │       │   └── Input/         # 玩家规划输入模式、pending state、intent port
│   │   │       ├── Animation/
│   │   │       └── UI/
│   │   │           ├── Auth/
│   │   │           ├── Lobby/
│   │   │           ├── HUD/
│   │   │           ├── Domestic/
│   │   │           ├── Combat/
│   │   │           ├── Minister/
│   │   │           ├── Game/
│   │   │           └── Common/
│   │   │
│   │   └── Tests/
│   │       ├── EditMode/              # Panoptes.Tests.EditMode.asmdef
│   │       └── PlayMode/              # Panoptes.Tests.PlayMode.asmdef
│   │
│   ├── Scenes/
│   │   ├── Boot.unity                 # 启动场景，初始化单例
│   │   ├── Login.unity                # 登录/注册
│   │   ├── Lobby.unity                # 大厅/房间
│   │   └── Game.unity                 # 游戏主场景
│   │
│   ├── Prefabs/
│   │   ├── Map/
│   │   │   ├── NodeTile.prefab        # 格子预制体
│   │   │   ├── Unit.prefab            # 单位预制体
│   │   │   └── Road.prefab            # 道路预制体
│   │   └── UI/
│   │       ├── MinisterPanel.prefab
│   │       ├── ActionCard.prefab
│   │       └── ErrorToast.prefab
│   │
│   ├── Art/                           # 免费美术素材
│   │   ├── Sprites/
│   │   │   ├── Terrain/               # 地形贴图
│   │   │   ├── Buildings/             # 建筑图标
│   │   │   ├── Units/                 # 兵种图标
│   │   │   └── UI/                    # UI元素
│   │   └── Fonts/
│   │
│   └── Resources/
│       └── Data/                      # 生成的静态目录 bundle
│
└── Packages/
    └── manifest.json
```

asmdef 边界约束（当前实现）：

```text
Panoptes.Protocol      rootNamespace: Panoptes.Protocol.V1
Panoptes.Core          引用 Panoptes.Protocol
Panoptes.Presentation  仅引用 Panoptes.Core，不引用 Panoptes.Protocol

=> Presentation 层不得直接使用 Panoptes.Protocol.V1 类型
=> 协议类型通过 Core 的 DTO + Mapper 在边界内完成转换
```

### 表现层包结构约定

- `Presentation/Map`：地图渲染、节点/单位/建筑 View、地图 overlay、相机上下文、debug map 数据和地图拾取适配。
- `Presentation/Map/InputAdapter`：只负责把 Unity pointer / physics raycast 转换为 `NodeView`、`UnitView` 或节点上下文，不发送 intent，不管理规划状态。
- `Presentation/Planning/Input`：玩家规划输入控制、输入模式生命周期、pending build/move/deploy 表现状态、intent 发送端口。
- `Presentation/Planning/Feedback`：把服务端 preview/result DTO 转成展示文案；不得在这里推导合法性。
- `MapPlanningInputController` 是地图上的玩家规划输入入口。它可以协调地图 adapter、Planning state 和 overlay，但不应再被命名或理解为通用 Map input。


---

## 4. 架构原则

### 原则一：客户端是哑终端

```
玩家点击"建造农场"
  → 立即发送 MsgTokenBuild 给服务端
  → 按钮进入Loading状态
  → 等待服务端返回 MsgTokenResult
  → 成功：更新缓存，刷新UI
  → 失败：显示ErrorToast，按钮恢复

❌ 不做：检查资源是否足够，判断格子是否合法
```

### 原则二：状态以服务端为准

运行时状态来自`GameStateCache`，静态目录与展示元数据来自`StaticCatalogCache`；两者都不能由 UI 本地推导业务合法性。

静态目录加载链路固定为：

```text
data/ 作者源
  -> make data-gen
  -> client/Assets/Resources/Data/
  -> StaticCatalogCache.LoadLocalCatalog()
  -> UI / MapRenderer / HUD 按 catalog key 查询展示信息
```

运行时状态与静态目录职责分离如下：
- `GameStateCache`：缓存服务端推送的节点、单位、玩家当前状态
- `StaticCatalogCache`：缓存本地静态目录 bundle、地图 runtime bundle、展示元数据
- `MsgStaticCatalogManifest`：只做版本握手与 hash 对比，不承担常规全量静态数据下发

地图加载优先级固定为：

```text
GameStateCache 中已有服务端节点
  -> 直接渲染服务端状态
否则
  -> StaticCatalogCache.TryGetDefaultMap()
  -> 从 Resources/Data/maps/<map_id>.runtime.json 构建静态预览地图
```

因此客户端禁止：
- 直接读取旧 `Resources/Config/*.json`
- 自己拼装地图默认节点
- 根据静态目录推导建造合法性、资源是否足够等业务规则

### 原则三：最终生命周期与依赖注入

新架构模块通过 VContainer 的 `LifetimeScope` 管理生命周期和依赖注入。迁移后的 Store、Service、ViewModel、Binder 不得主动查找 `*.Instance`。

目标结构：

```
ProjectLifetimeScope
  AuthStore
  ConfigStore
  StaticCatalogStore
  Network services
  App navigation services

GameLifetimeScope
  GameStateStore
  PlanningDraftStore
  SelectionStore
  TurnStore
  Game intent services
  Panel ViewModels
```

旧单例可以在未迁移模块中暂时保留，但不得作为新架构模块的兼容入口。迁移一个模块时，该模块的依赖所有权必须同时切到最终 `LifetimeScope`。

旧系统仍可能存在的 legacy 单例：

```
NetworkManager     单例，管理WebSocket连接
GameStateCache     单例，管理游戏状态镜像
AppManager         单例，管理全局状态机
AnimationQueue     单例，管理动画队列
```

这些 legacy 单例只服务未迁移旧模块。新 C0a 功能和已迁移模块必须使用 Store -> ViewModel -> Binder / Service 链路。

### 原则四：消息处理在主线程

WebSocket在后台线程接收消息，必须通过`UnityMainThreadDispatcher`切回主线程后再更新UI和缓存。

---

## 5. 消息协议

### Transport V2 规范

所有WebSocket消息使用 `ClientFrame` / `ServerFrame`，编码为 protojson：

```json
{
  "meta": { "requestId": "req-17" },
  "game": {
    "planning": {
      "buildStructure": {
        "nodeId": "C3",
        "buildingTypeId": "farm"
      }
    }
  }
}
```

### 消息类型枚举（固定，不得新增）

**客户端→服务端：**

```
// Auth
MsgRegister
MsgLogin

// Lobby
MsgCreateRoom
MsgJoinRoom
MsgLeaveRoom
MsgReadyUp

// Planning 阶段
MsgSetPolicy
MsgSetInstitutionLoadout
MsgSetResearchTarget
MsgSetBuildingRecipe
MsgBuildStructure
MsgBuildStructurePreviewRequest
MsgSetBuildingRecipePreviewRequest
MsgTokenReveal
MsgSetWarZone
MsgWarZoneDirective
MsgSetMinisterDirective
MsgIssueUnitOrder
MsgCancelUnitOrder
MsgPlanningPathPreviewRequest
MsgSubmitTurn

// 游戏通用
MsgStaticCatalogSyncRequest
ChatCommand
```

**服务端→客户端：**

```
// Auth
MsgLoginSuccess
MsgAuthError

// Lobby
MsgRoomCreated
MsgRoomState
MsgGameStarting
MsgLobbyError

// 游戏流程
MsgGameInit
MsgGameOver

// Planning / Resolving
MsgPlanningStart
MsgPlanningSnapshot
MsgPlanningPathPreviewResponse
MsgBuildStructurePreviewResponse
MsgSetBuildingRecipePreviewResponse
MsgTokenResult
MsgRevealResult
MsgResearchResult
MsgSetPolicyResult
MsgSetInstitutionLoadoutResult
MsgSetBuildingRecipeResult
MsgBuildStructureResult
MsgIssueUnitOrderResult
MsgTurnReport
MsgGameSync
MsgMinisterReportChunk
MsgMinisterMetrics
MsgGameChatPosted
MsgGameChatSync

// 通用
Problem
```

---

## 6. 场景结构

### Boot.unity

只负责初始化单例，不显示任何UI。
启动后自动跳转到Login.unity。

```
Hierarchy:
  Boot
    AppManager
    NetworkManager
    GameStateCache
    AnimationQueue
    MainThreadDispatcher
```

### Login.unity

```
Hierarchy:
  Canvas
    LoginPanel
      UsernameField (TMP_InputField)
      PasswordField (TMP_InputField)
      LoginButton
      RegisterButton
      ErrorText (TextMeshProUGUI)
```

### Lobby.unity

```
Hierarchy:
  Canvas
    LobbyPanel
      CreateRoomButton
      JoinRoomSection
        RoomCodeField
        JoinButton
    RoomPanel (初始隐藏)
      RoomCodeDisplay
      PlayerList
        PlayerSlot (×2)
      ReadyButton
      LeaveButton
      StartCountdown (初始隐藏)
```

### Game.unity

```
Hierarchy:
  Camera
    Main Camera

  Map
    MapRenderer          ← 动态生成Node格子
    RoadRenderer         ← 动态生成道路

  Units                  ← 动态生成Unit对象

  Canvas (Screen Space - Overlay)
    HUD
      ResourceHUD
      TokenHUD
      TurnHUD
      CastleHPBars
    MinisterArea
      MinisterPanel
      MetricsPanel
    DomesticUI (内政阶段显示)
      BuildMenu
      PolicyPanel
      TokenActionBar
    CombatUI (战斗阶段显示)
      WarZonePanel
      OrderReviewPanel
    PhaseOverlay
      SubmitButton
      CountdownTimer
    Notifications
      ActionCardContainer
      ErrorToastContainer
```

---

## 7. 核心组件

### NetworkManager.cs

```csharp
// 单例，管理WebSocket连接
public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    // 连接状态
    public bool IsConnected { get; private set; }

    // 连接到服务器
    public async Task ConnectAsync(string url);

    // 断开连接
    public void Disconnect();

    // 发送消息（封装为ClientFrame后发送）
    public void Send(IMessage message);

    // 内部：收到消息时分发给MessageDispatcher
    private void OnMessageReceived(byte[] data);
}
```

### MessageDispatcher.cs

```csharp
// 从ServerFrame提取具体payload并路由到对应Handler
public class MessageDispatcher : MonoBehaviour
{
    // 注册Handler（由ProjectLifetimeScope注入后调用）
    public void Register<T>(string messageType, Action<T> handler)
        where T : IMessage<T>, new();

    // 分发（由NetworkManager调用，已在主线程）
    public void Dispatch(ServerFrame frame);
}
```

Handler注册示例：
```csharp
public void UseProjectServices(MessageDispatcher dispatcher, GameStateCache cache)
{
    _dispatcher = dispatcher;
    _cache = cache;
    _dispatcher.Register<MsgGameInit>("MsgGameInit", OnGameInit);
    _dispatcher.Register<MsgPlanningStart>("MsgPlanningStart", OnPlanningStart);
    _dispatcher.Register<MsgPlanningSnapshot>("MsgPlanningSnapshot", OnPlanningSnapshot);
    _dispatcher.Register<MsgGameSync>("MsgGameSync", OnGameSync);
	// ...
}
```

### GameStateCache.cs

```csharp
// 服务端状态的本地镜像
// 只读访问，只由网络消息更新
public class GameStateCache : MonoBehaviour
{
    public static GameStateCache Instance { get; private set; }

    // 基本信息
    public string GameID { get; private set; }
    public string MyPlayerID { get; private set; }
    public int Turn { get; private set; }
    public string Phase { get; private set; }  // "planning|resolving"

    // 节点（key = node_id）
    public IReadOnlyDictionary<string, NodeView> Nodes { get; }

    // 单位（key = unit_id）
    public IReadOnlyDictionary<string, UnitView> Units { get; }

    // 玩家状态
    public PlayerView MyPlayer { get; private set; }

    // 令牌
    public int TokensLeft { get; private set; }

    // 部长列表
    public IReadOnlyList<MinisterView> Ministers { get; }

    // 内部更新方法（由MessageDispatcher调用）
    internal void ApplyGameInit(MsgGameInit msg);
    internal void ApplyPlanningStart(MsgPlanningStart msg);
    internal void ApplyPlanningSnapshot(MsgPlanningSnapshot msg);
    internal void ApplyGameSync(MsgGameSync msg);
    internal void UpdateTokens(int tokensLeft);
    internal void UpdateNodeView(NodeView node);
}
```

---

## 8. UI系统

### MinisterPanel.cs

部长汇报面板，负责流式显示LLM生成的文字。

```csharp
public class MinisterPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text ministerNameText;
    [SerializeField] private TMP_Text reportText;
    [SerializeField] private GameObject panel;

    private StringBuilder _buffer = new();

    // 开始新一轮汇报
    public void StartReport(string ministerRole);

    // 追加流式文字（每个chunk调用一次）
    public void AppendChunk(string chunk);

    // 汇报结束
    public void FinalizeReport();

    // 清空并隐藏
    public void Clear();
}
```

处理`MsgMinisterReportChunk`消息：
```csharp
private void OnMinisterReportChunk(MsgMinisterReportChunk msg)
{
    if (!msg.IsFinal)
        MinisterPanel.Instance.AppendChunk(msg.Chunk);
    else
        MinisterPanel.Instance.FinalizeReport();
}
```

### MetricsPanel.cs

数值轨面板，和叙事轨并排显示。

```csharp
public class MetricsPanel : MonoBehaviour
{
    // 更新数值轨数据
    public void UpdateMetrics(IList<MetricItem> metrics);

    // 每个MetricItem显示：label + value + trend箭头 + confidence标记
    // is_delayed=true时显示"(延迟)"标注
}
```

### BuildMenu.cs

建造菜单，点击格子后弹出可建造的建筑列表。

```csharp
public class BuildMenu : MonoBehaviour
{
    private PlanningIntentService _planningIntentService;

    [Inject]
    private void Construct(PlanningIntentService planningIntentService)
    {
        _planningIntentService = planningIntentService;
    }

    // 在指定屏幕位置显示可建造列表
    // buildingTypes由外部传入，不在本类计算
    public void Show(string nodeId, Vector2 screenPos, IList<string> buildingTypes);

    // 隐藏
    public void Hide();

    // 玩家选择某个建筑时的回调；这里只打包输入，不做合法性校验
    private void OnBuildingSelected(string nodeId, string buildingTypeId, string cityId)
    {
        _planningIntentService.BuildStructure(nodeId, buildingTypeId, cityId);
        Hide();
    }
}
```

### WarZonePanel.cs

战区划定面板。

```csharp
public class WarZonePanel : MonoBehaviour
{
    private GameIntentService _gameIntentService;

    [Inject]
    private void Construct(GameIntentService gameIntentService)
    {
        _gameIntentService = gameIntentService;
    }

    // 进入战区划定模式（格子可框选）
    public void EnterZoneEditMode(string zoneId);

    // 退出划定模式
    public void ExitZoneEditMode();

    // 显示战区指令下达UI
    public void ShowDirectiveUI(string zoneId, string zoneName);

    // 发送战区指令
    private void OnDirectiveSelected(string zoneId, string directive, string targetNode)
    {
        _gameIntentService.SetWarZoneDirective(zoneId, directive, targetNode);
    }
}
```

### OrderReviewPanel.cs

部长战斗指令审阅面板。

```csharp
public class OrderReviewPanel : MonoBehaviour
{
    private PlanningToolService _planningToolService;
    private GameIntentService _gameIntentService;

    [Inject]
    private void Construct(PlanningToolService planningToolService, GameIntentService gameIntentService)
    {
        _planningToolService = planningToolService;
        _gameIntentService = gameIntentService;
    }

    // 显示部长生成的指令列表
    public void ShowOrders(IList<UnitOrder> orders);

    // 否决某条指令（消耗令牌）
    private void OnVetoOrder(string unitId)
    {
        _gameIntentService.VetoCombatOrder(unitId);
    }

    // 修改为精确微操（消耗令牌）
    private void OnMicroOrder(string unitId)
    {
        // 进入地图规划输入流程，实际合法性仍由服务端判定
        _planningToolService.BeginMoveSelection(unitId);
    }
}
```

---

## 9. 网络层

### 连接和认证流程

```csharp
// App启动流程
async void Start()
{
    // 1. 连接WebSocket
    await _networkManager.ConnectAsync(Config.ServerURL);

    // 2. 如果有本地存储的token，尝试自动登录
    // Gamejam阶段跳过，直接显示登录界面
    SceneLoader.Instance.LoadScene("Login");
}

// 登录成功后
private void OnLoginSuccess(MsgLoginSuccess msg)
{
    // 保存token和player_id
    PlayerPrefs.SetString("token", msg.Token);
    PlayerPrefs.SetString("player_id", msg.PlayerId);
    SceneLoader.Instance.LoadScene("Lobby");
}
```

### IClientMessageSender / NetworkMessageSender

```csharp
// 注入式端口，统一封装并发送 ClientFrame
public interface IClientMessageSender
{
    bool Send(IMessage message);
}
```

### 断线重连

```csharp
// NetworkManager内部处理
private async void HandleDisconnect()
{
    // 显示断线提示
    LoadingOverlay.Instance.Show("连接断开，正在重连...");

    int retries = 0;
    while (retries < 5)
    {
        await Task.Delay(2000);
        try
        {
            await ConnectAsync(Config.ServerURL);
            // 重连成功后，服务端检测到相同player_id会重发MsgGameInit
            LoadingOverlay.Instance.Hide();
            return;
        }
        catch { retries++; }
    }

    // 重连失败，返回登录界面
    SceneLoader.Instance.LoadScene("Login");
}
```

---

## 10. 状态缓存

### 初始化（MsgGameInit）

```csharp
internal void ApplyGameInit(MsgGameInit msg)
{
    GameID = msg.GameId;
    MyPlayerID = msg.YourPlayerId;
    Turn = msg.Turn;
    Phase = msg.Phase;

    // 清空并重建节点缓存
    _nodes.Clear();
    foreach (var node in msg.Nodes)
        _nodes[node.Id] = node;

    // 清空并重建单位缓存
    _units.Clear();
    foreach (var unit in msg.Units)
        _units[unit.Id] = unit;

    MyPlayer = msg.MyPlayer;
    _ministers = msg.Ministers.ToList();

    // 通知MapRenderer重建地图
    MapRenderer.Instance.RebuildMap();
}
```

### 同步更新（MsgGameSync）

```csharp
internal void ApplyGameSync(MsgGameSync msg)
{
    Turn = msg.Turn;
    Phase = msg.Phase;
    if (msg.MyPlayer != null)
    {
        MyPlayer = msg.MyPlayer;
    }
    foreach (var node in msg.Nodes)
        _nodes[node.Id] = node;
    foreach (var unit in msg.Units)
        _units[unit.Id] = unit;

    // 结算动画和时间线消费 msg.Events / DomainEventEnvelope，再刷新地图表现。
    MapRenderer.Instance.RebuildMap();
}
```

---

## 11. 动画系统

### AnimationQueue.cs

统一结算后，服务端推送 `MsgGameSync`。客户端按 `DomainEventEnvelope.channel/kind/data` 顺序播放移动、伤害、建筑、科技和胜负表现。

```csharp
public class AnimationQueue : MonoBehaviour
{
    public static AnimationQueue Instance { get; private set; }

    private Queue<DomainEventEnvelope> _queue = new();
    private bool _isPlaying = false;

    // 收到 MsgGameSync 时调用
    public void Enqueue(IList<DomainEventEnvelope> events)
    {
        foreach (var e in events)
            _queue.Enqueue(e);

        if (!_isPlaying)
            StartCoroutine(PlayQueue());
    }

    private IEnumerator PlayQueue()
    {
        _isPlaying = true;
        while (_queue.Count > 0)
        {
            var e = _queue.Dequeue();
            yield return StartCoroutine(PlayEvent(e));
        }
        _isPlaying = false;

        // 动画播放完成后，更新缓存和UI
        _gameStateCache.ApplyCombatSettlement(_pendingSettlement);
    }

    private IEnumerator PlayEvent(CombatEvent e)
    {
        switch (e.DataCase)
        {
            case CombatEvent.DataOneofCase.UnitMove:
                yield return UnitMoveAnim.Play(e.UnitMove);
                break;
            case CombatEvent.DataOneofCase.UnitDied:
                yield return CombatAnim.PlayDeath(e.UnitDied);
                break;
            case CombatEvent.DataOneofCase.CastleDamaged:
                yield return CastleDamageAnim.Play(e.CastleDamaged);
                break;
            // ...
        }
    }
}
```

### UnitMoveAnim.cs

```csharp
public class UnitMoveAnim : MonoBehaviour
{
    // 播放单位移动动画
    public static IEnumerator Play(UnitMoveEvent e)
    {
        var unitView = UnitCache.Instance.GetView(e.UnitId);
        if (unitView == null) yield break;

        var startPos = GridToWorld(e.From);
        var endPos = GridToWorld(e.To);
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            unitView.transform.position =
                Vector3.Lerp(startPos, endPos, elapsed / duration);
            yield return null;
        }

        unitView.transform.position = endPos;
    }
}
```

---

## 12. 开发顺序

严格按照此顺序开发，每步完成后再进入下一步。

```
Step 1：项目基础（Day 1）
  - Unity项目创建，URP配置
  - NativeWebSocket和Protobuf插件导入
    - 运行根目录 `make gen`，同步更新 `Assets/Scripts/Protocol/`
  - Boot场景，单例初始化
  - NetworkManager：连接，发送，接收，主线程回调
  - MessageDispatcher：ServerFrame提取与payload路由骨架
  - IClientMessageSender 与 NetworkMessageSender 注入式发送端口
  - Config.cs：服务器地址配置

Step 2：登录和大厅（Day 2上午）
  - Login场景和LoginPanel
  - 发MsgLogin，处理MsgLoginSuccess/MsgAuthError
  - Lobby场景，LobbyPanel
  - 创建房间，加入房间，ReadyUp流程
  - 处理MsgRoomState，MsgGameStarting
  - 收到GameStarting后跳转Game场景

Step 3：地图渲染（Day 2下午）
  - Game场景基础结构
  - GameStateCache，处理MsgGameInit
  - NodeTile Prefab（简单方块，地形颜色区分）
  - MapRenderer：根据Cache动态生成格子
  - UnitView Prefab（简单图标）
  - 格子点击高亮

Step 4：规划 UI（Day 3）
  - HUD：资源显示，令牌显示，回合/阶段显示
  - 处理MsgPlanningStart：显示阶段、倒计时和当前 planning snapshot
  - BuildMenu：点击格子弹出，选择建筑类型发送
  - PolicyPanel：国策选择发送
  - TechTreePanel：科研目标选择发送
  - UnitInfoPanel：单位移动、攻击、建城等 planning 指令发送
  - 处理MsgTokenResult：成功/失败Toast
  - MsgMinisterReportChunk：流式文字显示
  - MsgMinisterMetrics：数值轨显示
  - MsgPlanningSnapshot：更新草稿显示
  - MsgGameSync：更新缓存，刷新格子
  - SubmitButton和倒计时

Step 5：结算动画（Day 4～5）
  - AnimationQueue
  - UnitMoveAnim（线性插值移动）
  - CombatAnim（简单震动+闪红）
  - CastleDamageAnim（血条减少动画）
  - MsgGameSync：队列播放，完成后更新缓存

Step 6：联调和打磨（Day 6～7）
  - 和服务端全流程联调
  - 断线重连处理
  - MsgGameOver：胜负界面，LLM叙事文字显示
  - 错误处理完善（ErrorToast覆盖所有错误码）
  - CastleHPBars：双方主城血条更新
  - 数值显示对齐（和服务端的数字保持一致）
```

---

## 附录：格子坐标转换

地图使用格子坐标（整数x,y），渲染时转为Unity世界坐标。

```csharp
// Config.cs中定义
public const float TileSize = 1.0f;

// 格子坐标→世界坐标
public static Vector3 GridToWorld(int x, int y)
{
    return new Vector3(x * TileSize, 0, y * TileSize);
}

public static Vector3 GridToWorld(Position pos)
{
    return GridToWorld(pos.X, pos.Y);
}

// 世界坐标→格子坐标（点击时用）
public static (int x, int y) WorldToGrid(Vector3 worldPos)
{
    return (
        Mathf.RoundToInt(worldPos.x / TileSize),
        Mathf.RoundToInt(worldPos.z / TileSize)
    );
}
```

---

## 附录：错误码列表

若有不足，可继续增加。

服务端返回的`error_code`字段枚举，客户端对应显示：

```
insufficient_resources     → "资源不足"
invalid_target             → "目标无效"
not_your_turn              → "还没到你的回合"
no_tokens_left             → "令牌已用完"
building_exists            → "此处已有建筑"
outside_safe_zone          → "超出安全区范围"
unit_not_found             → "找不到该单位"
invalid_directive          → "无效的指令"
phase_mismatch             → "当前阶段不支持此操作"
game_not_found             → "游戏不存在"
room_full                  → "房间已满"
room_not_found             → "房间不存在"
already_in_room            → "你已在房间中"
auth_failed                → "认证失败，请重新登录"
```

---

*文档版本：1.0 | 项目代号：Panoptes | 最后更新：2026-04-04*
*本文档由人工确认，Agent开发以此为准，不得自行修改已定义的接口和结构。*
