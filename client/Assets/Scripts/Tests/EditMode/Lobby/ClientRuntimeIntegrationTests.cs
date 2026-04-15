using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Events;
using Panoptes.Protocol.V1;
using Panoptes.Presentation.UI.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Panoptes.Tests.EditMode.Lobby
{
    public sealed class ClientRuntimeIntegrationTests
    {
        private readonly string _appManagerPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Application/App/AppManager.cs");
        private readonly string _lobbyServicePath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Service/LobbyService.cs");
        private readonly string _lobbyScenePath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Lobby/LobbySceneController.cs");
        private readonly string _lobbyPanelControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Lobby/LobbyPanelController.cs");
        private readonly string _roomPanelControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Lobby/RoomPanelController.cs");
        private readonly string _gamePhasesPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Foundation/Domain/GamePhases.cs");
        private readonly string _gameSceneControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs");
        private readonly string _mapRendererPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/MapRenderer.cs");
        private readonly string _mapInputHandlerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/MapInputHandler.cs");
        private readonly string _gameSceneAssetPath = Path.GetFullPath("Assets/Scenes/Game.unity");
        private readonly string _strategicPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Turn/StrategicPanel.cs");
        private readonly string _unitInfoPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/UnitInfoPanelController.cs");
        private readonly string _unitOrdersPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Turn/UnitOrdersPanel.cs");
        private readonly string _runtimeScriptsRoot = Path.GetFullPath("Assets/Scripts/Runtime");

        [TearDown]
        public void TearDown()
        {
            DestroySingleton("Panoptes.Core.Application.Cache.ClientRuntimeConfigCache, Panoptes.Core");
            DestroySingleton("Panoptes.Core.Application.Cache.GameStateCache, Panoptes.Core");
            DestroySingleton("Panoptes.Core.Application.Cache.PlanningDraftCache, Panoptes.Core");
        }

        [Test]
        public void ClientRuntimeConfigCache_ShouldDefaultToFalse_AndRaiseChangeEvent()
        {
            var cacheType = Type.GetType("Panoptes.Core.Application.Cache.ClientRuntimeConfigCache, Panoptes.Core")
                            ?? throw new AssertionException("ClientRuntimeConfigCache 类型不存在。");
            var cacheObject = new GameObject("ClientRuntimeConfigCache");
            var cache = cacheObject.AddComponent(cacheType);

            var changedCount = 0;
            var changedEvent = cacheType.GetEvent("OnConfigChanged");
            var handler = new Action(() => changedCount++);
            changedEvent?.AddEventHandler(cache, handler);

            Assert.That(GetProperty<bool>(cache, cacheType, "DevMode"), Is.False);

            var msgType = Type.GetType("Panoptes.Protocol.V1.MsgClientRuntimeConfig, Panoptes.Protocol")
                          ?? throw new AssertionException("MsgClientRuntimeConfig 类型不存在。");
            var msg = Activator.CreateInstance(msgType)
                      ?? throw new AssertionException("无法创建 MsgClientRuntimeConfig。");
            msgType.GetProperty("DevMode")?.SetValue(msg, true);
            cacheType.GetMethod("Apply")?.Invoke(cache, new[] { msg });

            Assert.That(GetProperty<bool>(cache, cacheType, "DevMode"), Is.True);
            Assert.That(changedCount, Is.EqualTo(1));

            cacheType.GetMethod("Clear")?.Invoke(cache, Array.Empty<object>());

            Assert.That(GetProperty<bool>(cache, cacheType, "DevMode"), Is.False);
            Assert.That(changedCount, Is.EqualTo(2));
        }

        [Test]
        public void GameStateCache_ShouldRaiseStateChanged_WhenGameInitAppliedAndCleared()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<Panoptes.Core.Application.Cache.GameStateCache>();

            var changedCount = 0;
            cache.OnStateChanged += () => changedCount++;

            var gameInitType = Type.GetType("Panoptes.Protocol.V1.MsgGameInit, Panoptes.Protocol")
                               ?? throw new AssertionException("MsgGameInit 类型不存在。");
            var playerViewType = Type.GetType("Panoptes.Protocol.V1.PlayerView, Panoptes.Protocol")
                                ?? throw new AssertionException("PlayerView 类型不存在。");
            var gameInit = Activator.CreateInstance(gameInitType)
                           ?? throw new AssertionException("无法创建 MsgGameInit。");
            var playerView = Activator.CreateInstance(playerViewType)
                            ?? throw new AssertionException("无法创建 PlayerView。");

            gameInitType.GetProperty("GameId")?.SetValue(gameInit, "game-1");
            gameInitType.GetProperty("YourPlayerId")?.SetValue(gameInit, "player-1");
            gameInitType.GetProperty("Turn")?.SetValue(gameInit, 1);
            gameInitType.GetProperty("Phase")?.SetValue(gameInit, "planning");
            playerViewType.GetProperty("TokensLeft")?.SetValue(playerView, 3);
            gameInitType.GetProperty("MyPlayer")?.SetValue(gameInit, playerView);

            var applyMethod = typeof(Panoptes.Core.Application.Cache.GameStateCache).GetMethod("ApplyGameInit");
            applyMethod?.Invoke(cache, new[] { gameInit });

            Assert.That(cache.GameID, Is.EqualTo("game-1"));
            Assert.That(cache.MyPlayerID, Is.EqualTo("player-1"));
            Assert.That(changedCount, Is.EqualTo(1));

            cache.Clear();

            Assert.That(cache.GameID, Is.Empty);
            Assert.That(cache.MyPlayerID, Is.Empty);
            Assert.That(changedCount, Is.EqualTo(2));
        }

        [Test]
        public void PlanningDraftCache_ShouldClearPreview_WhenPlanningSnapshotApplied()
        {
            var cache = PlanningDraftCache.EnsureInstance();
            var previewChanged = 0;
            cache.PreviewChanged += () => previewChanged++;

            cache.TrackPreviewRequest("req-1", "unit-1", "move", "node-b");
            cache.ApplyPreviewResponse(new MsgPlanningPathPreviewResponse
            {
                RequestId = "req-1",
                UnitId = "unit-1",
                Action = "move",
                TargetNodeId = "node-b",
                Valid = true
            });

            Assert.That(cache.CurrentPreview, Is.Not.Null, "预览响应后应存在当前 preview。");

            cache.ApplyPlanningSnapshot(new MsgPlanningSnapshot
            {
                Turn = 2,
                Phase = "planning",
                PlannedInstitutionPolicyIds = { "academy_charter" }
            });

            Assert.That(cache.CurrentPreview, Is.Null, "snapshot 覆盖后应清掉旧 preview。");
            Assert.That(cache.PlannedInstitutionPolicyIds.Count, Is.EqualTo(1));
            Assert.That(cache.PlannedInstitutionPolicyIds[0], Is.EqualTo("academy_charter"));
            Assert.That(previewChanged, Is.GreaterThanOrEqualTo(2), "预览建立与清理都应触发 PreviewChanged。");
        }

        [Test]
        public void GameStateCache_ShouldRefreshPlanningStartWithoutSettlementReplay()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<GameStateCache>();

            var nodeEvents = 0;
            var unitEvents = 0;
            var settledEvents = 0;
            NodeChangedEvent lastNodeEvent = null;
            UnitsChangedEvent lastUnitEvent = null;

            cache.OnNodeChanged += evt =>
            {
                nodeEvents++;
                lastNodeEvent = evt;
            };
            cache.OnUnitsChanged += evt =>
            {
                unitEvents++;
                lastUnitEvent = evt;
            };
            cache.OnTurnSettled += _ => settledEvents++;

            cache.ApplyGameInit(new MsgGameInit
            {
                GameId = "game-1",
                YourPlayerId = "player-1",
                Turn = 1,
                Phase = "planning",
                MyPlayer = new PlayerView
                {
                    Id = "player-1",
                    TokensLeft = 1,
                    CapitalCityCoreHp = 100,
                    CapitalCityCoreMaxHp = 100
                },
                Nodes =
                {
                    new NodeView
                    {
                        Id = "A1",
                        Pos = new Position { X = 0, Y = 0 },
                        Terrain = "plain",
                        ControllerPlayerId = "player-1",
                        TerritoryOwnerPlayerId = "player-1",
                        BuildingTypeId = "city_core",
                        BuildingHp = 100
                    }
                },
                Units =
                {
                    new UnitView
                    {
                        Id = "unit-1",
                        Faction = "player-1",
                        UnitType = "settler",
                        Hp = 10,
                        MaxHp = 10,
                        Pos = new Position { X = 0, Y = 0 }
                    }
                }
            });

            nodeEvents = 0;
            unitEvents = 0;
            lastNodeEvent = null;
            lastUnitEvent = null;

            var planningStart = new MsgPlanningStart
            {
                Turn = 2,
                Phase = "planning",
                Timeout = 30,
                Tokens = 3,
                MyPlayer = new PlayerView
                {
                    Id = "player-1",
                    TokensLeft = 3,
                    CapitalCityCoreHp = 90,
                    CapitalCityCoreMaxHp = 100
                },
                Snapshot = new MsgPlanningSnapshot
                {
                    Turn = 2,
                    Phase = "planning",
                    PlannedInstitutionPolicyIds = { "academy_charter" }
                },
                Nodes =
                {
                    new NodeView
                    {
                        Id = "A1",
                        Pos = new Position { X = 0, Y = 0 },
                        Terrain = "plain",
                        ControllerPlayerId = "player-1",
                        TerritoryOwnerPlayerId = "player-1",
                        BuildingTypeId = "city_core",
                        BuildingHp = 90
                    }
                },
                Units =
                {
                    new UnitView
                    {
                        Id = "unit-1",
                        Faction = "player-1",
                        UnitType = "settler",
                        Hp = 10,
                        MaxHp = 10,
                        Pos = new Position { X = 1, Y = 0 }
                    }
                }
            };

            cache.ApplyPlanningStart(planningStart);

            Assert.That(settledEvents, Is.EqualTo(0), "planning_start 不应触发 settlement 回放。");
            Assert.That(cache.TokensLeft, Is.EqualTo(3));
            Assert.That(cache.MyPlayer, Is.Not.Null);
            Assert.That(cache.MyPlayer.TokensLeft, Is.EqualTo(3));
            Assert.That(PlanningDraftCache.EnsureInstance().PlannedInstitutionPolicyIds.Single(), Is.EqualTo("academy_charter"));
            Assert.That(nodeEvents, Is.EqualTo(1));
            Assert.That(lastNodeEvent, Is.Not.Null);
            Assert.That(lastNodeEvent.ChangeType, Is.EqualTo("planning_start"));
            Assert.That(unitEvents, Is.EqualTo(1));
            Assert.That(lastUnitEvent, Is.Not.Null);
            Assert.That(lastUnitEvent.ChangeType, Is.EqualTo("planning_start"));
            Assert.That(lastUnitEvent.Moved.Count, Is.EqualTo(1));

            cache.ApplyPlanningStart(planningStart);

            Assert.That(nodeEvents, Is.EqualTo(1), "相同 planning_start 不应重复发 node diff。");
            Assert.That(unitEvents, Is.EqualTo(1), "相同 planning_start 不应重复发 unit diff。");
        }

        [Test]
        public void LobbyService_ShouldExposeAddBotAndKickPlayerMessages()
        {
            Assert.That(File.Exists(_lobbyServicePath), Is.True, "LobbyService.cs 不存在。");

            var content = File.ReadAllText(_lobbyServicePath);
            StringAssert.Contains("public void AddBot()", content);
            StringAssert.Contains("MessageSender.Send(new MsgAddBot())", content);
            StringAssert.Contains("public void KickPlayer(string playerId)", content);
            StringAssert.Contains("MessageSender.Send(new MsgKickPlayer", content);
            StringAssert.Contains("public void StartGame()", content);
            StringAssert.Contains("MessageSender.Send(new MsgStartGame())", content);
        }

        [Test]
        public void LobbySceneController_ShouldRegisterPlayerKickedHandler()
        {
            Assert.That(File.Exists(_lobbyScenePath), Is.True, "LobbySceneController.cs 不存在。");

            var content = File.ReadAllText(_lobbyScenePath);
            StringAssert.Contains("_cache.OnPlayerKicked += OnPlayerKicked;", content);
        }

        [Test]
        public void AppManager_ShouldRegisterRuntimeConfig_AndApplyGameInitBeforeTransition()
        {
            Assert.That(File.Exists(_appManagerPath), Is.True, "AppManager.cs 不存在。");

            var content = File.ReadAllText(_appManagerPath);
            StringAssert.Contains("EnsureComponent<ClientRuntimeConfigCache>(managers);", content);
            StringAssert.Contains("EnsureComponent<GameStateCache>(managers);", content);
            StringAssert.Contains("EnsureComponent<PlanningDraftCache>(managers);", content);
            Assert.That(content, Does.Not.Contain("EnsureComponent<CombatDraftCache>(managers);"),
                "Managers 不应再挂载 CombatDraftCache。");
            StringAssert.Contains("EnsureOptionalLoadingOverlay(managers);", content);
            StringAssert.Contains("EnsureOptionalErrorToast(managers);", content);
            StringAssert.Contains("EnsureOptionalConfirmDialog(managers);", content);
            StringAssert.Contains("Resources.Load<GameObject>(resourcePath)", content,
                "通用弹层应优先从 prefab 资源实例化，而不是继续直接挂在 Managers 上。");
            StringAssert.Contains("Instantiate(prefab)", content,
                "通用弹层应生成为独立根对象，而不是继续复用 Managers 树。");
            StringAssert.Contains("overlayObject.transform.SetParent(null, false);", content,
                "通用弹层必须与 Managers 脱离父子关系，避开 LoadingOverlay 的 CanvasGroup。");
            StringAssert.Contains("Register<MsgClientRuntimeConfig>(\"MsgClientRuntimeConfig\", OnClientRuntimeConfig)", content);

            var applyIndex = content.IndexOf("GameStateCache.Instance?.ApplyGameInit(msg);", StringComparison.Ordinal);
            var transitionIndex = content.IndexOf("TransitionTo(AppState.Game);", StringComparison.Ordinal);
            Assert.That(applyIndex, Is.GreaterThanOrEqualTo(0), "AppManager 必须先写入 GameStateCache。");
            Assert.That(transitionIndex, Is.GreaterThan(applyIndex), "AppManager 必须在 ApplyGameInit 之后再切换 Game 场景。");
        }

        [Test]
        public void LobbyPanelController_ShouldRouteErrorsAndSuccessThroughErrorToast()
        {
            Assert.That(File.Exists(_lobbyPanelControllerPath), Is.True, "LobbyPanelController.cs 不存在。");

            var content = File.ReadAllText(_lobbyPanelControllerPath);
            StringAssert.Contains("using Panoptes.Presentation.UI.Common;", content);
            StringAssert.Contains("ErrorToast.Instance", content,
                "大厅面板应优先通过 ErrorToast 展示错误/成功提示。");
            StringAssert.Contains("ShowToast(message, false);", content,
                "大厅错误提示应走 ErrorToast。");
            StringAssert.Contains("ShowToast($\"房间已创建，邀请码：{roomCode}\", true);", content,
                "创建房间成功后应给出 toast 反馈。");
        }

        [Test]
        public void RoomPanelController_ShouldUseConfirmDialog_ForLeaveKickAndStartGame()
        {
            Assert.That(File.Exists(_roomPanelControllerPath), Is.True, "RoomPanelController.cs 不存在。");

            var content = File.ReadAllText(_roomPanelControllerPath);
            StringAssert.Contains("using Panoptes.Presentation.UI.Common;", content);
            StringAssert.Contains("ConfirmDialog.Instance", content,
                "房间敏感操作应优先通过 ConfirmDialog 二次确认。");
            StringAssert.Contains("ShowConfirmation(", content);
            StringAssert.Contains("\"开始游戏\"", content,
                "开始游戏前应弹确认框。");
            StringAssert.Contains("\"离开房间\"", content,
                "离开房间前应弹确认框。");
            StringAssert.Contains("\"移出玩家\"", content,
                "踢人前应弹确认框。");
            StringAssert.Contains("ShowToast(\"确认面板未就绪，请稍后重试\", false);", content,
                "ConfirmDialog 缺失时应保守降级并提示用户。");
            Assert.That(content, Does.Not.Contain("onConfirm?.Invoke();"),
                "ConfirmDialog 缺失时不应直接执行敏感操作。");
        }

        [Test]
        public void GameSceneController_ShouldRenderWaitingStateWithoutWarning_WhenCacheIsEmpty()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<Panoptes.Core.Application.Cache.GameStateCache>();
            SetSingletonInstance(typeof(Panoptes.Core.Application.Cache.GameStateCache), cache);

            var controllerObject = new GameObject("GameSceneController");
            var controller = controllerObject.AddComponent<GameSceneController>();

            InvokeLifecycle(controller, "Awake");

            LogAssert.NoUnexpectedReceived();
            controller.RefreshFromCache();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GamePhases_ShouldOnlyExposePlanningAndResolving()
        {
            Assert.That(File.Exists(_gamePhasesPath), Is.True, "GamePhases.cs 不存在。");

            var content = File.ReadAllText(_gamePhasesPath);
            StringAssert.Contains("public const string Planning = \"planning\";", content);
            StringAssert.Contains("public const string Resolving = \"resolving\";", content);
            Assert.That(content, Does.Not.Contain("DomesticPlanning"));
            Assert.That(content, Does.Not.Contain("CombatPlanning"));
            Assert.That(content, Does.Not.Contain("DomesticResolving"));
            Assert.That(content, Does.Not.Contain("CombatResolving"));
        }

        [Test]
        public void GameSceneController_ShouldBootstrapUnifiedPlanningAndSettlementPresentation()
        {
            Assert.That(File.Exists(_gameSceneControllerPath), Is.True, "GameSceneController.cs 不存在。");

            var content = File.ReadAllText(_gameSceneControllerPath);
            StringAssert.Contains("EnsureComponent<SettlementTimeline>(canvas.transform, \"SettlementTimeline\");", content);
            StringAssert.Contains("EnsureComponent<TurnReportPanel>(canvas.transform, \"TurnReportPanel\");", content);
            StringAssert.Contains("EnsureRuntimeComponent<SettlementPlaybackController>(\"SettlementPlaybackController\");", content);
            Assert.That(content, Does.Not.Contain("EnsureComponent<StrategicPanel>"),
                "Game 场景不应再装配 StrategicPanel。");
            Assert.That(content, Does.Not.Contain("EnsureComponent<UnitOrdersPanel>"),
                "Game 场景不应再装配独立 UnitOrdersPanel。");
            Assert.That(content, Does.Not.Contain("MicroPanel"));
            Assert.That(content, Does.Not.Contain("OrderReviewPanel"));
            Assert.That(content, Does.Not.Contain("CombatPlaybackController"));
        }

        [Test]
        public void MapInputHandler_ShouldOnlyIssueMoveOrders_FromAuthoritativePreview()
        {
            Assert.That(File.Exists(_mapInputHandlerPath), Is.True, "MapInputHandler.cs 不存在。");

            var content = File.ReadAllText(_mapInputHandlerPath);
            StringAssert.Contains("TryIssueAuthoritativeMoveOrder(node.NodeId)", content,
                "移动点击应只通过服务端权威 preview 结果发单。");
            StringAssert.Contains("TryGetCurrentMovePreview", content,
                "MapInputHandler 应读取当前服务端 preview，而不是继续走本地规则。");
            StringAssert.Contains("ResolveMovePreviewErrorMessage", content,
                "无效 preview 应给出明确反馈。");
            Assert.That(content, Does.Not.Contain("Backward-compatible quick move"),
                "不应继续保留基于本地高亮的快速移动兼容壳。");
            Assert.That(content, Does.Not.Contain("moveRange = 4"),
                "不应继续使用本地固定移动范围假高亮。");
        }

        [Test]
        public void MapRenderer_GameRuntime_ShouldNotFallbackToLocalOrConfiguredMaps()
        {
            Assert.That(File.Exists(_mapRendererPath), Is.True, "MapRenderer.cs 不存在。");

            var content = File.ReadAllText(_mapRendererPath);
            StringAssert.Contains("BuildBackendGameMap();", content,
                "Game 运行态应通过单独的后端权威建图入口渲染地图。");
            Assert.That(content, Does.Not.Contain("HasSufficientTerritoryAndCastles(backendNodes)"),
                "Game 运行态不应再根据 territory/city_core 完整度决定是否回退本地地图。");
            Assert.That(content, Does.Not.Contain("if (TryLoadConfiguredMap())"),
                "Game 运行态不应再尝试从配置或本地 fallback 地图建图。");
        }

        [Test]
        public void GameScene_MapRendererConfig_ShouldDisableRuntimeFallback()
        {
            Assert.That(File.Exists(_gameSceneAssetPath), Is.True, "Game.unity 不存在。");

            var content = File.ReadAllText(_gameSceneAssetPath);
            Assert.That(content, Does.Not.Contain("preferLocalMapWhenBackendHasNoTerritory: 1"),
                "Game 场景不应再启用 territory 不完整时的本地 fallback。");
            Assert.That(content, Does.Not.Contain("preferServerPushedMapConfig: 1"),
                "Game 场景运行时不应再从服务端配置或本地资源选择另一张地图。");
        }

        [Test]
        public void StrategicPanel_RuntimeScript_ShouldBeRemoved_AfterCleanup()
        {
            Assert.That(File.Exists(_strategicPanelPath), Is.True, "StrategicPanel.cs 占位文件不存在。");

            var content = File.ReadAllText(_strategicPanelPath);
            Assert.That(content, Does.Not.Contain("class StrategicPanel"),
                "StrategicPanel 后续要整体重构，当前不应继续保留运行时类型。");
        }

        [Test]
        public void UnitInfoPanel_ShouldOwnPerUnitPlanningSummary_AndDirectOrderActions()
        {
            Assert.That(File.Exists(_unitInfoPanelPath), Is.True, "UnitInfoPanelController.cs 不存在。");

            var content = File.ReadAllText(_unitInfoPanelPath);
            StringAssert.Contains("PlanningDraftCache", content,
                "UnitInfoPanel 应直接消费规划草稿缓存。");
            StringAssert.Contains("GetOrdersInDisplayOrder", content,
                "UnitInfoPanel 应展示当前规划中的单位命令摘要。");
            StringAssert.Contains("BeginMoveSelection", content,
                "UnitInfoPanel 应直接承载移动命令入口。");
            StringAssert.Contains("BeginAttackSelection", content,
                "UnitInfoPanel 应直接承载攻击命令入口。");
            StringAssert.Contains("IssueHoldOrder", content,
                "UnitInfoPanel 应直接承载待命命令入口。");
            StringAssert.Contains("BeginChargeSelection", content,
                "UnitInfoPanel 应直接承载冲锋命令入口。");
        }

        [Test]
        public void UnitOrdersPanel_RuntimeScript_ShouldBeRemoved_AfterMerge()
        {
            Assert.That(File.Exists(_unitOrdersPanelPath), Is.True, "UnitOrdersPanel.cs 占位文件不存在。");

            var content = File.ReadAllText(_unitOrdersPanelPath);
            Assert.That(content, Does.Not.Contain("class UnitOrdersPanel"),
                "UnitOrdersPanel 已并入 UnitInfoPanelController，不应继续保留独立运行时类型。");
        }

        [Test]
        public void RuntimeScripts_ShouldNotUseDeprecatedUnityApis_ThatWereJustRemoved()
        {
            Assert.That(Directory.Exists(_runtimeScriptsRoot), Is.True, "Runtime 脚本目录不存在。");

            var scriptPaths = Directory.GetFiles(_runtimeScriptsRoot, "*.cs", SearchOption.AllDirectories);
            Assert.That(scriptPaths, Is.Not.Empty, "未找到 Runtime 脚本。");

            foreach (var path in scriptPaths)
            {
                var content = File.ReadAllText(path);
                Assert.That(content, Does.Not.Contain("enableWordWrapping"),
                    $"已废弃的 TMP enableWordWrapping 不应再出现在 {path}。");
                Assert.That(content, Does.Not.Contain("GetInstanceID("),
                    $"已废弃的 GetInstanceID 不应再出现在 {path}。");
                Assert.That(content, Does.Not.Contain("FindObjectsSortMode"),
                    $"已废弃的 FindObjectsSortMode 重载不应再出现在 {path}。");
            }
        }

        private static void DestroySingleton(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type == null)
            {
                return;
            }

            var existing = UnityEngine.Object.FindAnyObjectByType(type) as Component;
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            SetSingletonInstance(type, null);
        }

        private static T GetProperty<T>(Component instance, Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName)
                           ?? throw new AssertionException($"缺少属性 {propertyName}");
            return (T)property.GetValue(instance);
        }

        private static void InvokeLifecycle(object instance, string methodName)
        {
            var method = instance.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (method == null)
            {
                throw new AssertionException($"缺少生命周期方法 {methodName}");
            }

            method.Invoke(instance, null);
        }

        private static void SetSingletonInstance(Type type, object value)
        {
            var field = type.GetField("<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            field.SetValue(null, value);
        }
    }
}
