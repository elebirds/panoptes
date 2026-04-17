using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Events;
using Panoptes.Core.Infrastructure.Network;
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
        private readonly string _nodeViewPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/NodeView.cs");
        private readonly string _settlementPlaybackControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/SettlementPlaybackController.cs");
        private readonly string _cityCoreBuildingActionRegistrarPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/CityCoreBuildingActionRegistrar.cs");
        private readonly string _cityCoreProductionPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Domestic/CityCoreProductionPanel.cs");
        private readonly string _techTreePanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Domestic/TechTreePanelController.cs");
        private readonly string _recipeSynthesisPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Turn/RecipeSynthesisPanel.cs");
        private readonly string _configCachePath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Application/Cache/ConfigCache.cs");
        private readonly string _staticCatalogCachePath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Application/Cache/StaticCatalogCache.cs");
        private readonly string _configBridgePath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Network/ConfigMessageBridge.cs");
        private readonly string _cityCoreHpBarPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/CityCoreHPBar.cs");
        private readonly string _cityCoreHpBarOverlayControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/CityCoreHpBarOverlayController.cs");
        private readonly string _buildingViewPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/BuildingView.cs");
        private readonly string _buildCommandPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Domestic/BuildCommandPanel.cs");
        private readonly string _nodeTilePrefabPath = Path.GetFullPath("Assets/Prefabs/Map/NodeTile3D.prefab");
        private readonly string _cityCorePrefabAssetPath = Path.GetFullPath("Assets/Prefabs/Map/CityCore.prefab");
        private readonly string _cityCoreHpBarPrefabPath = Path.GetFullPath("Assets/Prefabs/UI/CityCoreHPBar.prefab");
        private readonly string _cityCoreProductionPanelPrefabPath = Path.GetFullPath("Assets/Prefabs/UI/CityCoreProductionPanel.prefab");
        private readonly string _gameSceneAssetPath = Path.GetFullPath("Assets/Scenes/Game.unity");
        private readonly string _integrationCheckerPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Debug/IntegrationChecker.cs");
        private readonly string _strategicPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Turn/StrategicPanel.cs");
        private readonly string _unitInfoPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/UnitInfoPanelController.cs");
        private readonly string _unitOrdersPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Turn/UnitOrdersPanel.cs");
        private readonly string _runtimeScriptsRoot = Path.GetFullPath("Assets/Scripts/Runtime");

        [TearDown]
        public void TearDown()
        {
            DestroySingleton("Panoptes.Core.Application.Cache.ClientRuntimeConfigCache, Panoptes.Core");
            DestroySingleton("Panoptes.Core.Application.Cache.ConfigCache, Panoptes.Core");
            DestroySingleton("Panoptes.Core.Application.Cache.GameStateCache, Panoptes.Core");
            DestroySingleton("Panoptes.Core.Application.Cache.StaticCatalogCache, Panoptes.Core");
            DestroySingleton("Panoptes.Core.Application.Cache.PlanningDraftCache, Panoptes.Core");
            DestroySingleton("Panoptes.Core.Infrastructure.Network.MessageDispatcher, Panoptes.Core");
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
        public void ConfigCache_ShouldApplyTypedBatch_AndClearSessionScopedEntries()
        {
            var cacheType = Type.GetType("Panoptes.Core.Application.Cache.ConfigCache, Panoptes.Core")
                            ?? throw new AssertionException("ConfigCache 类型不存在。");
            var cacheObject = new GameObject("ConfigCache");
            var cache = cacheObject.AddComponent(cacheType);

            var batchType = Type.GetType("Panoptes.Protocol.V1.MsgConfigBatchJson, Panoptes.Protocol")
                           ?? throw new AssertionException("MsgConfigBatchJson 类型不存在。");
            var entryType = Type.GetType("Panoptes.Protocol.V1.ConfigJsonEntry, Panoptes.Protocol")
                           ?? throw new AssertionException("ConfigJsonEntry 类型不存在。");

            var batch = Activator.CreateInstance(batchType)
                        ?? throw new AssertionException("无法创建 MsgConfigBatchJson。");
            var entry = Activator.CreateInstance(entryType)
                        ?? throw new AssertionException("无法创建 ConfigJsonEntry。");

            entryType.GetProperty("Key")?.SetValue(entry, "mapconfig");
            entryType.GetProperty("Json")?.SetValue(entry, "{\"id\":\"default\",\"nodes\":[]}");

            var configs = batchType.GetProperty("Configs")?.GetValue(batch);
            configs?.GetType().GetMethod("Add", new[] { entryType })?.Invoke(configs, new[] { entry });

            var applyMethod = cacheType.GetMethod("ApplyBatch");
            Assert.That(applyMethod, Is.Not.Null, "ConfigCache 必须提供 typed ApplyBatch 入口。");
            applyMethod?.Invoke(cache, new[] { batch });

            var tryGetJson = cacheType.GetMethod("TryGetJson");
            var args = new object[] { "mapconfig", null };
            var found = (bool)(tryGetJson?.Invoke(cache, args) ?? false);
            Assert.That(found, Is.True, "typed config batch 应写入 ConfigCache。");
            Assert.That(args[1] as string, Is.EqualTo("{\"id\":\"default\",\"nodes\":[]}"));

            cacheType.GetMethod("Clear")?.Invoke(cache, Array.Empty<object>());

            args = new object[] { "mapconfig", null };
            found = (bool)(tryGetJson?.Invoke(cache, args) ?? false);
            Assert.That(found, Is.False, "Clear 后不应保留旧会话的 config 覆盖。");
        }

        [Test]
        public void MessageDispatcher_ShouldDropCrossSessionGameEvents()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<GameStateCache>();
            SetSingletonInstance(typeof(GameStateCache), cache);

            var dispatcherObject = new GameObject("MessageDispatcher");
            var dispatcher = dispatcherObject.AddComponent<MessageDispatcher>();
            SetSingletonInstance(typeof(MessageDispatcher), dispatcher);

            var settlementDispatches = 0;
            dispatcher.Register<MsgTurnSettlement>("MsgTurnSettlement", _ => settlementDispatches++);

            dispatcher.Dispatch(BuildGameFrame(new MsgTurnSettlement { Turn = 1, Phase = "resolving" }, "session-stale"));
            Assert.That(settlementDispatches, Is.EqualTo(0),
                "未建立激活会话前，不应处理非 MsgGameInit 的游戏消息。");

            dispatcher.Dispatch(BuildGameFrame(new MsgGameInit
            {
                GameId = "game-1",
                YourPlayerId = "player-1",
                Turn = 1,
                Phase = "planning",
                MyPlayer = new PlayerView
                {
                    Id = "player-1",
                    TokensLeft = 3
                }
            }, "session-a"));

            var activeSession = GetStringPropertyIfPresent(cache, "ActiveGameSessionID");
            Assert.That(activeSession, Is.EqualTo("session-a"),
                "MsgGameInit 建立当前会话后，应记录激活中的 game session id。");

            dispatcher.Dispatch(BuildGameFrame(new MsgTurnSettlement { Turn = 2, Phase = "resolving" }, "session-b"));
            Assert.That(settlementDispatches, Is.EqualTo(0),
                "不同 session 的游戏消息必须在分发前被丢弃。");

            dispatcher.Dispatch(BuildGameFrame(new MsgTurnSettlement { Turn = 2, Phase = "resolving" }, "session-a"));
            Assert.That(settlementDispatches, Is.EqualTo(1),
                "同一 session 的游戏消息应继续正常分发。");
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
                PlanningStartEvents =
                {
                    new TurnEvent
                    {
                        Type = "technology_activated",
                        Data = { { "technology_id", "agrarian_foundations" }, { "player_id", "player-1" } }
                    }
                },
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
            Assert.That(cache.LastPlanningStartEvents.Count, Is.EqualTo(1));
            Assert.That(cache.LastPlanningStartEvents[0].Type, Is.EqualTo("technology_activated"));
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
        public void GameStateCache_ShouldProjectAuthoritativeResearchInstitutionCityAndBuildingQueries()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<GameStateCache>();

            cache.ApplyGameInit(new MsgGameInit
            {
                GameId = "game-1",
                YourPlayerId = "player-1",
                Turn = 3,
                Phase = "planning",
                MyPlayer = new PlayerView
                {
                    Id = "player-1",
                    TokensLeft = 2,
                    ActiveNationalPolicyId = "war_preparedness",
                    CapitalCityCoreHp = 80,
                    CapitalCityCoreMaxHp = 100,
                    Research = new ResearchStateView
                    {
                        CurrentTargetTechnologyId = "agrarian_foundations",
                        CurrentProgress = 3,
                        RequiredProgress = 5,
                        CompletedTechnologyIds = { "organized_labor" },
                        ActiveTechnologyIds = { "organized_labor" },
                        PendingActivationTechnologyIds = { "logistics" },
                        SavedProgress =
                        {
                            new ResearchProgressEntry
                            {
                                TechnologyId = "agrarian_foundations",
                                CurrentProgress = 3,
                                RequiredProgress = 5
                            }
                        }
                    },
                    Institutions = new InstitutionStateView
                    {
                        SlotCount = 2,
                        CandidatePolicyIds = { "academy_charter", "war_foundry" },
                        ActivePolicyIds = { "academy_charter" }
                    }
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
                        BuildingHp = 80,
                        BuildingStatus = "active",
                        CityId = "city-1",
                        ServiceCityId = "city-1",
                        IsCityCore = true
                    },
                    new NodeView
                    {
                        Id = "A2",
                        Pos = new Position { X = 1, Y = 0 },
                        Terrain = "plain",
                        ControllerPlayerId = "player-1",
                        TerritoryOwnerPlayerId = "player-1",
                        BuildingTypeId = "farm",
                        BuildingHp = 60,
                        BuildingStatus = "takeover",
                        CityId = string.Empty,
                        ServiceCityId = "city-1",
                        TakeoverProgress = 1,
                        TakeoverRequired = 2
                    }
                }
            });

            var research = cache.GetCurrentResearchState();
            Assert.That(research, Is.Not.Null, "缓存应提供当前研究状态投影。");
            Assert.That(research.TechnologyId, Is.EqualTo("agrarian_foundations"));
            Assert.That(research.CurrentProgress, Is.EqualTo(3));
            Assert.That(research.RequiredProgress, Is.EqualTo(5));
            Assert.That(research.CompletedTechnologyIds, Is.EquivalentTo(new[] { "organized_labor" }));
            Assert.That(research.ActiveTechnologyIds, Is.EquivalentTo(new[] { "organized_labor" }));
            Assert.That(research.PendingActivationTechnologyIds, Is.EquivalentTo(new[] { "logistics" }));

            var institutions = cache.GetInstitutionState();
            Assert.That(institutions, Is.Not.Null, "缓存应提供制度状态投影。");
            Assert.That(institutions.SlotCount, Is.EqualTo(2));
            Assert.That(institutions.CandidatePolicyIds, Is.EquivalentTo(new[] { "academy_charter", "war_foundry" }));
            Assert.That(institutions.ActivePolicyIds, Is.EquivalentTo(new[] { "academy_charter" }));

            var cities = cache.GetCities();
            Assert.That(cities, Has.Count.EqualTo(1), "缓存应按权威节点重建城市索引。");
            Assert.That(cache.TryGetCity("city-1", out var city), Is.True);
            Assert.That(city, Is.Not.Null);
            Assert.That(city.OwnerId, Is.EqualTo("player-1"));
            Assert.That(city.CoreNodeId, Is.EqualTo("A1"));
            Assert.That(city.BuildingNodeIds, Is.EquivalentTo(new[] { "A1", "A2" }));

            Assert.That(cache.TryGetBuilding("A2", out var building), Is.True);
            Assert.That(building, Is.Not.Null);
            Assert.That(building.NodeId, Is.EqualTo("A2"));
            Assert.That(building.CityId, Is.Empty);
            Assert.That(building.ServiceCityId, Is.EqualTo("city-1"));
            Assert.That(building.Status, Is.EqualTo("takeover"));
            Assert.That(building.TakeoverProgress, Is.EqualTo(1));
            Assert.That(building.TakeoverRequired, Is.EqualTo(2));
            Assert.That(building.IsCityCore, Is.False);
        }

        [Test]
        public void GameMessageHandler_ShouldPublishPlanningCommandResults_WithoutMutatingAuthoritativeState()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<GameStateCache>();
            SetSingletonInstance(typeof(GameStateCache), cache);

            cache.ApplyGameInit(new MsgGameInit
            {
                GameId = "game-1",
                YourPlayerId = "player-1",
                Turn = 4,
                Phase = "planning",
                MyPlayer = new PlayerView
                {
                    Id = "player-1",
                    TokensLeft = 2,
                    ActiveNationalPolicyId = "recovery",
                    Research = new ResearchStateView
                    {
                        CurrentTargetTechnologyId = "organized_labor",
                        CurrentProgress = 1,
                        RequiredProgress = 4
                    }
                }
            });

            PlanningCommandResultEvent researchResult = null;
            PlanningCommandResultEvent policyResult = null;
            GameErrorEvent policyError = null;
            cache.OnPlanningCommandResult += evt =>
            {
                if (evt.CommandType == "research")
                {
                    researchResult = evt;
                }
                else if (evt.CommandType == "policy")
                {
                    policyResult = evt;
                }
            };
            cache.OnGameError += evt => policyError = evt;

            InvokeStaticMessageHandler("OnResearchResult", new MsgResearchResult
            {
                Success = true,
                TechnologyId = "agrarian_foundations"
            });
            InvokeStaticMessageHandler("OnSetPolicyResult", new MsgSetPolicyResult
            {
                Success = false,
                NationalPolicyId = "war_preparedness",
                ErrorCode = "invalid_request"
            });

            Assert.That(researchResult, Is.Not.Null, "研究 typed result 应发布统一规划命令结果事件。");
            Assert.That(researchResult.Success, Is.True);
            Assert.That(researchResult.CommandType, Is.EqualTo("research"));
            Assert.That(researchResult.PrimaryId, Is.EqualTo("agrarian_foundations"));

            Assert.That(policyResult, Is.Not.Null, "国策失败也应发布统一规划命令结果事件。");
            Assert.That(policyResult.Success, Is.False);
            Assert.That(policyResult.CommandType, Is.EqualTo("policy"));
            Assert.That(policyResult.PrimaryId, Is.EqualTo("war_preparedness"));
            Assert.That(policyResult.ErrorCode, Is.EqualTo("invalid_request"));

            Assert.That(policyError, Is.Not.Null, "规划命令失败应继续走 GameError 事件。");
            Assert.That(policyError.Code, Is.EqualTo("invalid_request"));

            Assert.That(cache.GetCurrentResearchState().TechnologyId, Is.EqualTo("organized_labor"),
                "typed result 不应覆盖权威 active cache 中的研究目标。");
            Assert.That(cache.MyPlayer.ActiveNationalPolicyId, Is.EqualTo("recovery"),
                "typed result 不应覆盖权威 active cache 中的国策状态。");
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
            StringAssert.Contains("EnsureComponent<ConfigCache>(managers);", content);
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
            StringAssert.Contains("Register<MsgConfigBatchJson>(\"MsgConfigBatchJson\", OnConfigBatchJson)", content);
            StringAssert.Contains("Register<MsgStaticCatalogSectionChunk>(\"MsgStaticCatalogSectionChunk\", OnStaticCatalogSectionChunk)", content,
                "AppManager 必须注册 Catalog V2 section chunk 事件。");
            StringAssert.Contains("Register<MsgStaticCatalogSyncComplete>(\"MsgStaticCatalogSyncComplete\", OnStaticCatalogSyncComplete)", content,
                "AppManager 必须注册 Catalog V2 sync complete 事件。");
            StringAssert.Contains("MessageSender.Send(new MsgStaticCatalogSyncRequest", content,
                "收到 manifest 后，AppManager 必须主动发起 Catalog V2 同步请求。");
            StringAssert.Contains("ConfigCache.Instance?.Clear();", content,
                "进入 Login 或回退会话时必须清理会话级 ConfigCache。");
            Assert.That(content, Does.Not.Contain("StaticCatalogCache.Instance?.Clear();"),
                "AppManager 不应在登录态清空应用级静态目录缓存。");
            Assert.That(content, Does.Not.Contain("ConfigMessageBridge"),
                "正式启动链不应继续依赖 raw ConfigMessageBridge。");
            StringAssert.Contains("_pendingCatalogSync", content,
                "AppManager 必须显式跟踪 Catalog 同步中的 bootstrap 状态。");

            var applyIndex = content.IndexOf("GameStateCache.Instance?.ApplyGameInit(msg);", StringComparison.Ordinal);
            var transitionIndex = content.IndexOf("TransitionTo(AppState.Game);", StringComparison.Ordinal);
            Assert.That(applyIndex, Is.GreaterThanOrEqualTo(0), "AppManager 必须先写入 GameStateCache。");
            Assert.That(transitionIndex, Is.GreaterThan(applyIndex), "AppManager 必须在 ApplyGameInit 之后再切换 Game 场景。");
        }

        [Test]
        public void StaticCatalogAndConfigCache_ShouldHaveSeparatedRuntimeResponsibilities()
        {
            Assert.That(File.Exists(_configCachePath), Is.True, "ConfigCache.cs 不存在。");
            Assert.That(File.Exists(_staticCatalogCachePath), Is.True, "StaticCatalogCache.cs 不存在。");
            Assert.That(File.Exists(_techTreePanelPath), Is.True, "TechTreePanelController.cs 不存在。");
            Assert.That(File.Exists(_recipeSynthesisPanelPath), Is.True, "RecipeSynthesisPanel.cs 不存在。");
            Assert.That(File.Exists(_buildCommandPanelPath), Is.True, "BuildCommandPanel.cs 不存在。");
            Assert.That(File.Exists(_cityCoreProductionPanelPath), Is.True, "CityCoreProductionPanel.cs 不存在。");

            var configCacheContent = File.ReadAllText(_configCachePath);
            var staticCatalogCacheContent = File.ReadAllText(_staticCatalogCachePath);
            var techTreeContent = File.ReadAllText(_techTreePanelPath);
            var recipeContent = File.ReadAllText(_recipeSynthesisPanelPath);
            var buildContent = File.ReadAllText(_buildCommandPanelPath);
            var cityCoreContent = File.ReadAllText(_cityCoreProductionPanelPath);

            StringAssert.Contains("public void Clear()", configCacheContent,
                "ConfigCache 必须暴露会话级清理入口。");
            StringAssert.Contains("public void ApplyBatch(MsgConfigBatchJson msg)", configCacheContent,
                "ConfigCache 必须直接消费 typed config batch。");
            StringAssert.Contains("public CatalogSyncDecision CompareManifest(StaticCatalogManifest manifest)", staticCatalogCacheContent,
                "StaticCatalogCache 必须提供 manifest 比对入口。");
            StringAssert.Contains("public void BeginSectionSync(", staticCatalogCacheContent,
                "StaticCatalogCache 必须提供 section 同步起始入口。");
            StringAssert.Contains("public void ApplySectionChunk(MsgStaticCatalogSectionChunk chunk)", staticCatalogCacheContent,
                "StaticCatalogCache 必须支持逐块接收 section payload。");
            StringAssert.Contains("public bool FinalizeSectionSync(", staticCatalogCacheContent,
                "StaticCatalogCache 必须在 sync complete 后原子提交 section 变更。");
            StringAssert.Contains("ui_tech_tree_layout", staticCatalogCacheContent,
                "科技树布局元数据必须进入静态目录缓存。");

            Assert.That(techTreeContent, Does.Not.Contain("Missing technology tree config from server snapshot"),
                "科技树面板不应再等待服务端 snapshot 作为主路径。");
            Assert.That(techTreeContent, Does.Not.Contain("ConfigCache"),
                "科技树面板不应再通过 ConfigCache 读取静态科技实体。");
            StringAssert.Contains("TechNodeTitle", techTreeContent,
                "科技树在模板缺失时也必须生成可见标题文本，避免界面空白。");
            StringAssert.Contains("TechNodeDescription", techTreeContent,
                "科技树在模板缺失时也必须生成可见描述文本，避免界面空白。");
            Assert.That(recipeContent, Does.Not.Contain("ConfigCache"),
                "配方面板不应再通过 ConfigCache 读取静态配方实体。");
            Assert.That(buildContent, Does.Not.Contain("serverConfigKey = \"buildconfig\""),
                "建造面板不应再把 buildconfig 作为正式运行时主数据源。");
            Assert.That(cityCoreContent, Does.Not.Contain("buildConfigKey = \"buildconfig\""),
                "CityCoreProductionPanel 不应继续读取 buildconfig。");
            Assert.That(cityCoreContent, Does.Not.Contain("armyConfigKey = \"armyconfig\""),
                "CityCoreProductionPanel 不应继续读取 armyconfig。");
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
        public void MapInputHandler_ShouldRequireExplicitBuildCityContext()
        {
            Assert.That(File.Exists(_mapInputHandlerPath), Is.True, "MapInputHandler.cs 不存在。");

            var content = File.ReadAllText(_mapInputHandlerPath);
            StringAssert.Contains("public void EnterBuildPlacementAny(string buildingType, string cityId)", content,
                "建造入口应显式要求 cityId。");
            StringAssert.Contains("public void EnterBuildPlacementResource(string buildingType, string cityId)", content,
                "资源建筑入口应显式要求 cityId。");
            StringAssert.Contains("public void EnterBuildPlacementCity(string buildingType, string cityId)", content,
                "城内建筑入口应显式要求 cityId。");
            StringAssert.Contains("_activeBuildCityId = string.IsNullOrWhiteSpace(cityId) ? string.Empty : cityId.Trim();", content,
                "建造模式应保存显式传入的 cityId，而不是临时猜测。");
            StringAssert.Contains("GameIntents.BuildToken(nodeId, buildingType, _activeBuildCityId);", content,
                "建造消息必须透传显式 cityId。");
            Assert.That(content, Does.Not.Contain("SetBuildCastleContext"),
                "不应再保留隐藏式 SetBuildCastleContext 兼容入口。");
            Assert.That(content, Does.Not.Contain("TryResolveBuildCityId"),
                "不应再在客户端本地猜测 cityId。");
            StringAssert.Contains("缺少建造城市上下文，无法进入建造模式", content,
                "缺少 cityId 时应在进入建造模式前直接失败。");
        }

        [Test]
        public void MapInputHandler_ShouldNotGateCommandsByLocalPlacementOrTargetRules()
        {
            Assert.That(File.Exists(_mapInputHandlerPath), Is.True, "MapInputHandler.cs 不存在。");

            var content = File.ReadAllText(_mapInputHandlerPath);
            Assert.That(content, Does.Not.Contain("territoryOnlyBuildingTypes"),
                "Chunk 8A 后不应再靠本地 territoryOnlyBuildingTypes 过滤发送建造。");
            Assert.That(content, Does.Not.Contain("globalPlacementBuildingTypes"),
                "Chunk 8A 后不应再靠本地 globalPlacementBuildingTypes 过滤发送建造。");
            Assert.That(content, Does.Not.Contain("TryValidateTerritoryExpandRequest("),
                "建城发送前不应继续做本地合法性校验。");
            Assert.That(content, Does.Not.Contain("TryGetAttackableStructureNode("),
                "结构攻击发送前不应继续做本地目标合法性推断。");
            Assert.That(content, Does.Not.Contain("CanPlaceBuildingAt("),
                "建造点击不应继续以本地规则否决发送。");
        }

        [Test]
        public void MapInputHandler_ShouldPreferOwnedUnitSelectionBeforeBuildingInfo_OnSharedCityCoreTile()
        {
            Assert.That(File.Exists(_mapInputHandlerPath), Is.True, "MapInputHandler.cs 不存在。");

            var content = File.ReadAllText(_mapInputHandlerPath);
            var unitSelectionIndex = content.IndexOf("TrySelectOwnedUnitFromNodeClick()", StringComparison.Ordinal);
            var buildingInfoIndex = content.IndexOf("TryOpenBuildingInfoFromClick()", StringComparison.Ordinal);

            Assert.That(unitSelectionIndex, Is.GreaterThanOrEqualTo(0),
                "同格节点交互应先尝试选中己方可控单位。");
            Assert.That(buildingInfoIndex, Is.GreaterThan(unitSelectionIndex),
                "建筑信息打开逻辑必须排在同格单位优先判定之后。");
        }

        [Test]
        public void CityCoreRuntimeActions_ShouldUseCityCoreWithoutCastleAlias()
        {
            Assert.That(File.Exists(_cityCoreBuildingActionRegistrarPath), Is.True, "CityCoreBuildingActionRegistrar.cs 不存在。");
            Assert.That(File.Exists(_cityCoreProductionPanelPath), Is.True, "CityCoreProductionPanel.cs 不存在。");

            var registrarContent = File.ReadAllText(_cityCoreBuildingActionRegistrarPath);
            var productionPanelContent = File.ReadAllText(_cityCoreProductionPanelPath);

            StringAssert.Contains("\"city_core\"", registrarContent,
                "主城动作注册必须显式接受 city_core。");
            Assert.That(registrarContent, Does.Not.Contain("= \"castle\""),
                "主城动作注册不应再保留 castle 运行时别名。");
            Assert.That(registrarContent, Does.Not.Contain("castle_open_"),
                "主城动作注册不应再保留旧 action id 兼容入口。");
            Assert.That(registrarContent, Does.Not.Contain("building_open_recipe"),
                "主城动作注册不应再保留旧配方 action id 兼容入口。");
            StringAssert.Contains("\"city_core\"", productionPanelContent,
                "主城生产面板必须显式接受 city_core。");
            Assert.That(productionPanelContent, Does.Not.Contain("legacyCastleBuildingType"),
                "主城生产面板不应再保留 legacy castle 兼容字段。");
        }

        [Test]
        public void CityCoreRuntimeNaming_ShouldBeUnifiedAcrossCriticalPresentationChain()
        {
            Assert.That(File.Exists(_cityCoreHpBarPath), Is.True, "CityCoreHPBar.cs 不存在。");
            Assert.That(File.Exists(_cityCoreHpBarOverlayControllerPath), Is.True, "CityCoreHpBarOverlayController.cs 不存在。");
            Assert.That(File.Exists(_buildingViewPath), Is.True, "BuildingView.cs 不存在。");
            Assert.That(File.Exists(_buildCommandPanelPath), Is.True, "BuildCommandPanel.cs 不存在。");
            Assert.That(File.Exists(_cityCorePrefabAssetPath), Is.True, "CityCore.prefab 不存在。");
            Assert.That(File.Exists(_cityCoreHpBarPrefabPath), Is.True, "CityCoreHPBar.prefab 不存在。");
            Assert.That(File.Exists(_cityCoreProductionPanelPrefabPath), Is.True, "CityCoreProductionPanel.prefab 不存在。");

            var hpBarContent = File.ReadAllText(_cityCoreHpBarPath);
            var overlayContent = File.ReadAllText(_cityCoreHpBarOverlayControllerPath);
            var buildingViewContent = File.ReadAllText(_buildingViewPath);
            var buildCommandPanelContent = File.ReadAllText(_buildCommandPanelPath);
            var hpBarPrefabContent = File.ReadAllText(_cityCoreHpBarPrefabPath);
            var productionPanelPrefabContent = File.ReadAllText(_cityCoreProductionPanelPrefabPath);

            Assert.That(hpBarContent, Does.Not.Contain("Castle"),
                "主城血条脚本不应再保留 Castle 命名。");
            Assert.That(overlayContent, Does.Not.Contain("Castle"),
                "主城覆盖层脚本不应再保留 Castle 命名。");
            Assert.That(buildingViewContent, Does.Not.Contain("Castle"),
                "建筑视图脚本不应再保留 Castle 命名。");
            Assert.That(buildCommandPanelContent, Does.Not.Contain("SetCastleContext"),
                "建造面板不应再保留 SetCastleContext 命名。");
            Assert.That(buildCommandPanelContent, Does.Not.Contain("ClearCastleContext"),
                "建造面板不应再保留 ClearCastleContext 命名。");
            StringAssert.Contains("SetCityCoreContext", buildCommandPanelContent,
                "建造面板应改用 CityCore 命名的上下文入口。");
            StringAssert.Contains("ClearCityCoreContext", buildCommandPanelContent,
                "建造面板应改用 CityCore 命名的上下文清理入口。");
            StringAssert.Contains("Panoptes.Presentation::Panoptes.Presentation.UI.HUD.CityCoreHPBar", hpBarPrefabContent,
                "主城血条 prefab 应绑定 CityCoreHPBar 组件。");
            StringAssert.Contains("Panoptes.Presentation::Panoptes.Presentation.UI.Domestic.CityCoreProductionPanel", productionPanelPrefabContent,
                "主城生产面板 prefab 应绑定 CityCoreProductionPanel 组件。");
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
        public void GameScene_ShouldNotKeepCastlePlacementToken_AfterCityCoreUnification()
        {
            Assert.That(File.Exists(_gameSceneAssetPath), Is.True, "Game.unity 不存在。");

            var content = File.ReadAllText(_gameSceneAssetPath);
            Assert.That(content, Does.Not.Contain("- castle"),
                "Game 场景运行时关键配置不应再依赖 castle 常量。");
            StringAssert.Contains("- city_core", content,
                "Game 场景运行时关键配置应改为 city_core。");
        }

        [Test]
        public void NodeTilePrefab_ShouldMapDedicatedCityCorePrefab()
        {
            Assert.That(File.Exists(_nodeTilePrefabPath), Is.True, "NodeTile3D.prefab 不存在。");

            var content = File.ReadAllText(_nodeTilePrefabPath);
            StringAssert.Contains("buildingType: city_core", content,
                "NodeTile3D prefab 应为 city_core 提供专门映射。");
            Assert.That(content, Does.Not.Contain("buildingType: castle"),
                "NodeTile3D prefab 不应再保留 castle 重复映射。");
        }

        [Test]
        public void IntegrationChecker_ShouldOnlyRequireOwnedCityCore_AfterGameInit()
        {
            Assert.That(File.Exists(_integrationCheckerPath), Is.True, "IntegrationChecker.cs 不存在。");

            var content = File.ReadAllText(_integrationCheckerPath);
            StringAssert.Contains("MsgGameInit 后缺少己方 city_core", content,
                "客户端启动自检必须覆盖己方主城缺失场景。");
            Assert.That(content, Does.Not.Contain("MsgGameInit 后缺少己方扩张单位"),
                "客户端启动自检不应再把开局扩张单位当成必须项。");
        }

        [Test]
        public void NodeView_ShouldNotAliasCityCoreToCastle()
        {
            Assert.That(File.Exists(_nodeViewPath), Is.True, "NodeView.cs 不存在。");

            var content = File.ReadAllText(_nodeViewPath);
            Assert.That(content, Does.Not.Contain("case \"city_core\":\n                    return \"castle\""),
                "NodeView 不应再把 city_core 归一化成 castle。");
        }

        [Test]
        public void SettlementPlaybackController_ShouldHandleUnifiedChunk8ASettlementEvents()
        {
            Assert.That(File.Exists(_settlementPlaybackControllerPath), Is.True, "SettlementPlaybackController.cs 不存在。");

            var content = File.ReadAllText(_settlementPlaybackControllerPath);
            StringAssert.Contains("case \"city_founded\":", content,
                "结算回放应消费建城事件。");
            StringAssert.Contains("case \"building_status_changed\":", content,
                "结算回放应消费建筑状态变化事件。");
            StringAssert.Contains("case \"facility_takeover_progressed\":", content,
                "结算回放应消费设施接管推进事件。");
            StringAssert.Contains("case \"facility_takeover_completed\":", content,
                "结算回放应消费设施接管完成事件。");
            StringAssert.Contains("case \"technology_completed\":", content,
                "结算回放应消费科技完成事件。");
            StringAssert.Contains("case \"technology_activated\":", content,
                "结算回放应消费科技激活事件。");
            StringAssert.Contains("case \"city_core_destroyed\":", content,
                "结算回放应消费主城摧毁事件。");
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

        private static string GetStringPropertyIfPresent(Component instance, string propertyName)
        {
            var property = instance.GetType().GetProperty(propertyName);
            return property != null ? property.GetValue(instance) as string : string.Empty;
        }

        private static ServerFrame BuildGameFrame(object message, string gameSessionId)
        {
            var frame = new ServerFrame
            {
                Meta = new EventMeta()
            };

            var sessionProperty = typeof(EventMeta).GetProperty("GameSessionId");
            sessionProperty?.SetValue(frame.Meta, gameSessionId);

            var gameEvent = new GameEvent();
            switch (message)
            {
                case MsgGameInit gameInit:
                    gameEvent.GameInit = gameInit;
                    break;
                case MsgTurnSettlement settlement:
                    gameEvent.TurnSettlement = settlement;
                    break;
                default:
                    throw new AssertionException($"不支持的测试消息类型: {message?.GetType().Name ?? "null"}");
            }

            frame.Game = gameEvent;
            return frame;
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

        private static void InvokeStaticMessageHandler(string methodName, object message)
        {
            var method = typeof(Panoptes.Core.Application.Handler.GameMessageHandler).GetMethod(methodName,
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (method == null)
            {
                throw new AssertionException($"缺少静态消息处理方法 {methodName}");
            }

            method.Invoke(null, new[] { message });
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
