using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Core.Infrastructure.Mapper;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Protocol.V1;
using Panoptes.Presentation.UI.Common;
using Panoptes.Presentation.UI.Game;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.Lobby
{
    public sealed class ClientRuntimeIntegrationTests
    {
        private readonly string _appManagerPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Application/App/AppManager.cs");
        private readonly string _compositionBootstrapPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Composition/PanoptesCompositionBootstrap.cs");
        private readonly string _projectCompositionPrefabPath = Path.GetFullPath("Assets/Resources/Prefabs/Composition/PanoptesProjectComposition.prefab");
        private readonly string _lobbyServicePath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Service/LobbyService.cs");
        private readonly string _lobbyScenePath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Lobby/LobbySceneController.cs");
        private readonly string _lobbyPanelControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Lobby/LobbyPanelController.cs");
        private readonly string _roomPanelControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Lobby/RoomPanelController.cs");
        private readonly string _gamePhasesPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Foundation/Domain/GamePhases.cs");
        private readonly string _gameSceneControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs");
        private readonly string _mapRendererPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/MapRenderer.cs");
        private readonly string _mapPlanningInputControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/MapPlanningInputController.cs");
        private readonly string _movePreviewPresenterPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Planning/Feedback/MovePreviewPresenter.cs");
        private readonly string _nodeViewPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/NodeView.cs");
        private readonly string _settlementPlaybackControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/SettlementPlaybackController.cs");
        private readonly string _cityCoreBuildingActionRegistrarPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/CityCoreBuildingActionRegistrar.cs");
        private readonly string _cityCoreProductionPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Domestic/CityCoreProductionPanel.cs");
        private readonly string _resourceHudPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/ResourceHUD.cs");
        private readonly string _techTreePanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Domestic/TechTreePanelController.cs");
        private readonly string _techTreeViewModelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/ViewModels/TechTreeViewModel.cs");
        private readonly string _techTreeUiToolkitBinderPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Binders/UiToolkit/TechTreeUiToolkitBinder.cs");
        private readonly string _managementPanelVisibilityStorePath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/ViewModels/ManagementPanelVisibilityStore.cs");
        private readonly string _recipeSynthesisViewModelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/ViewModels/RecipeSynthesisViewModel.cs");
        private readonly string _recipeSynthesisUiToolkitBinderPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Binders/UiToolkit/RecipeSynthesisUiToolkitBinder.cs");
        private readonly string _recipeSynthesisContextStorePath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/ViewModels/RecipeSynthesisContextStore.cs");
        private readonly string _configCachePath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Application/Cache/ConfigCache.cs");
        private readonly string _staticCatalogCachePath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Application/Cache/StaticCatalogCache.cs");
        private readonly string _cityCoreHpBarPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/CityCoreHPBar.cs");
        private readonly string _cityCoreHpBarOverlayControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/CityCoreHpBarOverlayController.cs");
        private readonly string _buildingViewPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/BuildingView.cs");
        private readonly string _buildCatalogBinderPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Binders/UiToolkit/BuildCatalogUiToolkitBinder.cs");
        private readonly string _buildCatalogContextStorePath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/ViewModels/BuildCatalogContextStore.cs");
        private readonly string _buildCommandPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Domestic/BuildCommandPanel.cs");
        private readonly string _nodeTilePrefabPath = Path.GetFullPath("Assets/Prefabs/Map/NodeTile3D.prefab");
        private readonly string _cityCorePrefabAssetPath = Path.GetFullPath("Assets/Prefabs/Map/CityCore.prefab");
        private readonly string _cityCoreHpBarPrefabPath = Path.GetFullPath("Assets/Prefabs/UI/CityCoreHPBar.prefab");
        private readonly string _cityCoreProductionPanelPrefabPath = Path.GetFullPath("Assets/Prefabs/UI/CityCoreProductionPanel.prefab");
        private readonly string _lobbySceneAssetPath = Path.GetFullPath("Assets/Scenes/Lobby.unity");
        private readonly string _gameSceneAssetPath = Path.GetFullPath("Assets/Scenes/Game.unity");
        private readonly string _integrationCheckerPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Debug/IntegrationChecker.cs");
        private readonly string _strategicPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Turn/StrategicPanel.cs");
        private readonly string _unitInfoPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/UnitInfoPanelController.cs");
        private readonly string _unitInfoDirectOrderPanelBinderPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/UnitInfoDirectOrderPanelBinder.cs");
        private readonly string _unitOrdersPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Turn/UnitOrdersPanel.cs");
        private readonly string _runtimeScriptsRoot = Path.GetFullPath("Assets/Scripts/Runtime");

        [TearDown]
        public void TearDown()
        {
            DestroySingleton("Panoptes.Core.Application.Cache.ClientRuntimeConfigCache, Panoptes.Core");
            DestroySingleton("Panoptes.Core.Application.Cache.ConfigCache, Panoptes.Core");
            DestroySingleton("Panoptes.Core.Application.Cache.GameStateCache, Panoptes.Core");
            DestroySingleton("Panoptes.Core.Application.Cache.GameChatCache, Panoptes.Core");
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
        public void GameChatCache_ShouldApplyPostedAndSync_ForPresentation()
        {
            var cacheObject = new GameObject("GameChatCache");
            var cache = cacheObject.AddComponent<GameChatCache>();

            var addedCount = 0;
            var changedCount = 0;
            GameChatEntryDto addedEntry = null;

            cache.OnEntryAdded += entry =>
            {
                addedCount++;
                addedEntry = entry;
            };
            cache.OnEntriesChanged += () => changedCount++;

            cache.ApplyPosted(new MsgGameChatPosted
            {
                Entry = new ChatEntry
                {
                    Sequence = 1,
                    SenderPlayerId = "player-1",
                    Turn = 2,
                    Phase = "planning",
                    Payload = new ChatPayload
                    {
                        Emote = ChatEmote.Laugh
                    }
                }
            });

            Assert.That(cache.Entries.Count, Is.EqualTo(1));
            Assert.That(addedCount, Is.EqualTo(1));
            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(addedEntry, Is.Not.Null);
            Assert.That(addedEntry.Sequence, Is.EqualTo(1));
            Assert.That(addedEntry.Payload.Kind, Is.EqualTo(GameChatPayloadKind.Emote));
            Assert.That(addedEntry.Payload.Emote, Is.EqualTo(GameChatEmoteKind.Laugh));

            cache.ApplySync(new MsgGameChatSync
            {
                Entries =
                {
                    new ChatEntry
                    {
                        Sequence = 3,
                        SenderPlayerId = "player-2",
                        Turn = 4,
                        Phase = "resolving",
                        Payload = new ChatPayload
                        {
                            Emote = ChatEmote.Warning
                        }
                    }
                }
            });

            Assert.That(cache.Entries.Count, Is.EqualTo(1));
            Assert.That(cache.Entries[0].Sequence, Is.EqualTo(3));
            Assert.That(cache.Entries[0].Payload.Emote, Is.EqualTo(GameChatEmoteKind.Warning));
            Assert.That(changedCount, Is.EqualTo(2));

            cache.Clear();
            Assert.That(cache.Entries, Is.Empty);
            Assert.That(changedCount, Is.EqualTo(3));
        }

        [Test]
        public void GameMessageHandler_ShouldForwardGameChatPostedIntoChatCache()
        {
            var cacheObject = new GameObject("GameChatCache");
            var cache = cacheObject.AddComponent<GameChatCache>();
            SetSingletonInstance(typeof(GameChatCache), cache);

            InvokeStaticMessageHandler("OnGameChatPosted", new MsgGameChatPosted
            {
                Entry = new ChatEntry
                {
                    Sequence = 5,
                    SenderPlayerId = "player-2",
                    Turn = 6,
                    Phase = "settlement",
                    Payload = new ChatPayload
                    {
                        Emote = ChatEmote.Angry
                    }
                }
            });

            Assert.That(cache.Entries.Count, Is.EqualTo(1));
            Assert.That(cache.Entries[0].SenderPlayerId, Is.EqualTo("player-2"));
            Assert.That(cache.Entries[0].Payload.Emote, Is.EqualTo(GameChatEmoteKind.Angry));
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

            var syncDispatches = 0;
            dispatcher.Register<MsgGameSync>("MsgGameSync", _ => syncDispatches++);

            dispatcher.Dispatch(BuildGameFrame(new MsgGameSync { Turn = 1, Phase = "resolving" }, "session-stale"));
            Assert.That(syncDispatches, Is.EqualTo(0),
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

            dispatcher.Dispatch(BuildGameFrame(new MsgGameSync { Turn = 2, Phase = "resolving" }, "session-b"));
            Assert.That(syncDispatches, Is.EqualTo(0),
                "不同 session 的游戏消息必须在分发前被丢弃。");

            dispatcher.Dispatch(BuildGameFrame(new MsgGameSync { Turn = 2, Phase = "resolving" }, "session-a"));
            Assert.That(syncDispatches, Is.EqualTo(1),
                "同一 session 的游戏消息应继续正常分发。");
        }

        [Test]
        public void PlanningDraftCache_ShouldClearPreview_WhenPlanningSnapshotApplied()
        {
            var cache = CreatePlanningDraftCache();
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
                PlannedInstitutionIds = { "academy_charter" }
            });

            Assert.That(cache.CurrentPreview, Is.Null, "snapshot 覆盖后应清掉旧 preview。");
            Assert.That(cache.PlannedInstitutionIds.Count, Is.EqualTo(1));
            Assert.That(cache.PlannedInstitutionIds[0], Is.EqualTo("academy_charter"));
            Assert.That(previewChanged, Is.GreaterThanOrEqualTo(2), "预览建立与清理都应触发 PreviewChanged。");
        }

        [Test]
        public void PlanningDraftCache_ShouldParseAndFilterMinisterDrafts_FromPlanningSnapshot()
        {
            var cache = CreatePlanningDraftCache();

            cache.ApplyPlanningSnapshot(new MsgPlanningSnapshot
            {
                Turn = 3,
                Phase = "planning",
                MinisterDrafts =
                {
                    new MinisterDraftView
                    {
                        MinisterRole = "domestic",
                        Available = true,
                        JsonPayload = "{\"draft_id\":\"draft-research-1\",\"player_id\":\"player-1\",\"minister_role\":\"domestic\",\"kind\":\"research\",\"target_id\":\"agrarian_foundations\",\"target_label\":\"Agrarian Foundations\",\"title\":\"锁定科研目标\",\"summary\":\"建议先研究农业基础。\",\"rationale\":\"它能更快展开后续发展。\",\"risk_note\":\"若你改选其他科技，此卡会变为已偏离。\",\"status\":\"stale\",\"available\":true,\"turn\":3,\"source\":\"rule_only\"}"
                    },
                    new MinisterDraftView
                    {
                        MinisterRole = "domestic",
                        Available = false,
                        JsonPayload = "{\"draft_id\":\"draft-policy-1\",\"player_id\":\"player-1\",\"minister_role\":\"domestic\",\"kind\":\"policy\",\"target_id\":\"expansion\",\"target_label\":\"Expansion\",\"title\":\"调整国家政策\",\"summary\":\"建议转向扩张。\",\"rationale\":\"局势适合加快外扩。\",\"risk_note\":\"如果你拒绝，本回合不会再出现替代建议。\",\"status\":\"rejected\",\"available\":false,\"turn\":3,\"source\":\"rule_only\"}"
                    },
                    new MinisterDraftView
                    {
                        MinisterRole = "military",
                        Available = true,
                        JsonPayload = "{\"draft_id\":\"draft-war-1\",\"player_id\":\"player-1\",\"minister_role\":\"military\",\"kind\":\"operation\",\"target_id\":\"north_front\",\"target_label\":\"North Front\",\"title\":\"北线推进\",\"summary\":\"建议推进北线。\",\"rationale\":\"敌军压力偏低。\",\"risk_note\":\"侧翼暴露。\",\"status\":\"pending\",\"available\":true,\"turn\":3,\"source\":\"rule_only\"}"
                    }
                }
            });

            Assert.That(cache.MinisterDrafts.Count, Is.EqualTo(3), "缓存应保留完整草稿列表以支持 reconnect/snapshot 一致性。");

            var domesticDrafts = cache.GetDomesticMinisterDrafts();
            Assert.That(domesticDrafts.Count, Is.EqualTo(1), "主列表应过滤 rejected 与非 domestic 草稿。");

            var draft = domesticDrafts.Single();
            Assert.That(draft.DraftId, Is.EqualTo("draft-research-1"));
            Assert.That(draft.Kind, Is.EqualTo("research"));
            Assert.That(draft.IsInteractive, Is.True);
            Assert.That(draft.DisplayStatus, Is.EqualTo("已偏离，可重新采纳"));
        }

        [Test]
        public void GameStateCache_ShouldRefreshPlanningStartWithoutSettlementReplay()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<GameStateCache>();
            var planningDraftCache = CreatePlanningDraftCache();
            cache.UseProjectCaches(null, planningDraftCache, null);

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
                        Pos = new Position { Q = 0, R = 0 },
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
                        Pos = new Position { Q = 0, R = 0 }
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
                    new DomainEventEnvelope
                    {
                        Kind = "technology_activated",
                        Channel = "planning",
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
                    MinisterDrafts =
                    {
                        new MinisterDraftView
                        {
                            MinisterRole = "domestic",
                            Available = true,
                            JsonPayload = "{\"draft_id\":\"draft-research-2\",\"player_id\":\"player-1\",\"minister_role\":\"domestic\",\"kind\":\"research\",\"target_id\":\"agrarian_foundations\",\"target_label\":\"Agrarian Foundations\",\"title\":\"锁定科研目标\",\"summary\":\"建议先研究农业基础。\",\"rationale\":\"它能更快展开后续发展。\",\"risk_note\":\"若你改选其他科技，此卡会变为已偏离。\",\"status\":\"pending\",\"available\":true,\"turn\":2,\"source\":\"rule_only\"}"
                        }
                    },
                    PlannedInstitutionIds = { "academy_charter" }
                },
                Nodes =
                {
                    new NodeView
                    {
                        Id = "A1",
                        Pos = new Position { Q = 0, R = 0 },
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
                        Pos = new Position { Q = 1, R = 0 }
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
            Assert.That(planningDraftCache.GetDomesticMinisterDrafts().Count, Is.EqualTo(1));
            Assert.That(planningDraftCache.PlannedInstitutionIds.Single(), Is.EqualTo("academy_charter"));
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
                        CandidateInstitutionIds = { "academy_charter", "war_foundry" },
                        ActiveInstitutionIds = { "academy_charter" }
                    }
                },
                Nodes =
                {
                    new NodeView
                    {
                        Id = "A1",
                        Pos = new Position { Q = 0, R = 0 },
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
                        Pos = new Position { Q = 1, R = 0 },
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
            var savedProgressField = research.GetType().GetField("SavedProgress");
            Assert.That(savedProgressField, Is.Not.Null, "研究 DTO 必须暴露 SavedProgress 集合。");

            var institutions = cache.GetInstitutionState();
            Assert.That(institutions, Is.Not.Null, "缓存应提供制度状态投影。");
            Assert.That(institutions.SlotCount, Is.EqualTo(2));
            Assert.That(institutions.CandidateInstitutionIds, Is.EquivalentTo(new[] { "academy_charter", "war_foundry" }));
            Assert.That(institutions.ActiveInstitutionIds, Is.EquivalentTo(new[] { "academy_charter" }));

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
        public void GameMessageHandler_ShouldPublishStructuredBuildAndRecipeFeedback()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<GameStateCache>();
            SetSingletonInstance(typeof(GameStateCache), cache);

            PlanningCommandResultEvent buildResult = null;
            PlanningCommandResultEvent recipeResult = null;
            var errors = new System.Collections.Generic.List<GameErrorEvent>();
            cache.OnPlanningCommandResult += evt =>
            {
                if (evt.CommandType == "build")
                {
                    buildResult = evt;
                }
                else if (evt.CommandType == "building_recipe")
                {
                    recipeResult = evt;
                }
            };
            cache.OnGameError += evt => errors.Add(evt);

            InvokeStaticMessageHandler("OnBuildStructureResult", new MsgBuildStructureResult
            {
                Success = false,
                NodeId = "A2",
                BuildingTypeId = "farm",
                CityId = "city-1",
                ErrorCode = "resource_type_mismatch",
                FeedbackMessage = "农场只能建在粮食资源点上",
                FeedbackDetails =
                {
                    new FeedbackDetail { Key = "node_id", Value = "A2" },
                    new FeedbackDetail { Key = "building_type_id", Value = "farm" },
                    new FeedbackDetail { Key = "city_id", Value = "city-1" },
                    new FeedbackDetail { Key = "required_resource_type", Value = "food" }
                }
            });

            InvokeStaticMessageHandler("OnSetBuildingRecipeResult", new MsgSetBuildingRecipeResult
            {
                Success = false,
                NodeId = "A2",
                RecipeId = "smelt_iron",
                ErrorCode = "invalid_recipe_selection",
                FeedbackMessage = "该建筑当前不能切换到这个配方",
                FeedbackDetails =
                {
                    new FeedbackDetail { Key = "node_id", Value = "A2" },
                    new FeedbackDetail { Key = "recipe_id", Value = "smelt_iron" }
                }
            });

            Assert.That(buildResult, Is.Not.Null, "建造失败应发布结构化规划结果事件。");
            Assert.That(buildResult.Success, Is.False);
            Assert.That(buildResult.Action, Is.EqualTo("build_structure"));
            Assert.That(buildResult.PrimaryId, Is.EqualTo("A2"));
            Assert.That(buildResult.SecondaryId, Is.EqualTo("farm"));
            Assert.That(buildResult.TertiaryId, Is.EqualTo("city-1"));
            Assert.That(buildResult.Message, Is.EqualTo("农场只能建在粮食资源点上"));
            Assert.That(buildResult.Details, Is.Not.Null);
            Assert.That(buildResult.Details["required_resource_type"], Is.EqualTo("food"));

            Assert.That(recipeResult, Is.Not.Null, "配方失败应发布结构化规划结果事件。");
            Assert.That(recipeResult.Success, Is.False);
            Assert.That(recipeResult.Action, Is.EqualTo("set_building_recipe"));
            Assert.That(recipeResult.PrimaryId, Is.EqualTo("A2"));
            Assert.That(recipeResult.SecondaryId, Is.EqualTo("smelt_iron"));
            Assert.That(recipeResult.Message, Is.EqualTo("该建筑当前不能切换到这个配方"));
            Assert.That(recipeResult.Details, Is.Not.Null);
            Assert.That(recipeResult.Details["recipe_id"], Is.EqualTo("smelt_iron"));

            Assert.That(errors, Has.Count.EqualTo(2), "建造与配方失败都应继续发布 GameError。");
            Assert.That(errors[0].Message, Is.EqualTo("农场只能建在粮食资源点上"));
            Assert.That(errors[0].Details["node_id"], Is.EqualTo("A2"));
            Assert.That(errors[1].Message, Is.EqualTo("该建筑当前不能切换到这个配方"));
            Assert.That(errors[1].Details["recipe_id"], Is.EqualTo("smelt_iron"));
        }

        [Test]
        public void LobbyService_ShouldExposeAddBotAndKickPlayerMessages()
        {
            Assert.That(File.Exists(_lobbyServicePath), Is.True, "LobbyService.cs 不存在。");

            var content = File.ReadAllText(_lobbyServicePath);
            StringAssert.Contains("public void AddBot()", content);
            StringAssert.Contains("Send(new MsgAddBot())", content);
            StringAssert.Contains("public void KickPlayer(string playerId)", content);
            StringAssert.Contains("Send(new MsgKickPlayer", content);
            StringAssert.Contains("public void StartGame()", content);
            StringAssert.Contains("Send(new MsgStartGame())", content);
            Assert.That(content, Does.Not.Contain("MessageSender.Send"),
                "LobbyService 应通过注入的 IClientMessageSender 发送，不应回到静态 MessageSender。");
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
            var compositionBootstrap = File.ReadAllText(_compositionBootstrapPath);
            var projectCompositionPrefab = File.ReadAllText(_projectCompositionPrefabPath);
            StringAssert.Contains("ClientRuntimeConfigCache", projectCompositionPrefab);
            StringAssert.Contains("ConfigCache", projectCompositionPrefab);
            StringAssert.Contains("GameStateCache", projectCompositionPrefab);
            StringAssert.Contains("PlanningDraftCache", projectCompositionPrefab);
            Assert.That(content, Does.Not.Contain("EnsureComponent<CombatDraftCache>(managers);"),
                "Managers 不应再挂载 CombatDraftCache。");
            StringAssert.Contains("LoadingOverlay", projectCompositionPrefab);
            Assert.That(compositionBootstrap, Does.Not.Contain("EnsureComponent<"),
                "项目级组件应来自显式 Project Composition prefab，不应在 bootstrap 中 AddComponent。");
            Assert.That(content, Does.Not.Contain("EnsureOptionalErrorToast(managers);"),
                "ErrorToast 的生命周期应由 Presentation Project scope 管理，Core AppManager 不应反射创建 Presentation UI。");
            Assert.That(content, Does.Not.Contain("EnsureOptionalConfirmDialog(managers);"),
                "ConfirmDialog 的生命周期应由 Presentation Project scope 管理，Core AppManager 不应反射创建 Presentation UI。");

            StringAssert.Contains("EnsureProjectOverlay<ErrorToast>(\"ErrorToast\", \"Prefabs/UI/ErrorToast\")", compositionBootstrap);
            StringAssert.Contains("EnsureProjectOverlay<ConfirmDialog>(\"ConfirmDialog\", \"Prefabs/UI/ConfirmDialog\")", compositionBootstrap);
            StringAssert.Contains("Resources.Load<GameObject>(resourcePath)", compositionBootstrap,
                "通用弹层应优先从 prefab 资源实例化，而不是继续直接挂在 Managers 上。");
            StringAssert.Contains("Object.Instantiate(prefab)", compositionBootstrap,
                "通用弹层应生成为独立根对象，而不是继续复用 Managers 树。");
            StringAssert.Contains("overlayObject.transform.SetParent(null, false);", compositionBootstrap,
                "通用弹层必须与 Managers 脱离父子关系，避开 LoadingOverlay 的 CanvasGroup。");
            StringAssert.Contains("Register<MsgClientRuntimeConfig>(\"MsgClientRuntimeConfig\", OnClientRuntimeConfig)", content);
            StringAssert.Contains("Register<MsgConfigBatchJson>(\"MsgConfigBatchJson\", OnConfigBatchJson)", content);
            StringAssert.Contains("Register<MsgStaticCatalogSectionChunk>(\"MsgStaticCatalogSectionChunk\", OnStaticCatalogSectionChunk)", content,
                "AppManager 必须注册 Catalog V2 section chunk 事件。");
            StringAssert.Contains("Register<MsgStaticCatalogSyncComplete>(\"MsgStaticCatalogSyncComplete\", OnStaticCatalogSyncComplete)", content,
                "AppManager 必须注册 Catalog V2 sync complete 事件。");
            StringAssert.Contains("_messageSender?.Send(request);", content,
                "收到 manifest 后，AppManager 必须通过注入的消息发送器发起 Catalog V2 同步请求。");
            StringAssert.Contains("_configCache?.Clear();", content,
                "进入 Login 或回退会话时必须清理会话级 ConfigCache。");
            Assert.That(content, Does.Not.Contain("StaticCatalogCache.Instance?.Clear();"),
                "AppManager 不应在登录态清空应用级静态目录缓存。");
            Assert.That(content, Does.Not.Contain("ConfigMessageBridge"),
                "正式启动链不应继续依赖 raw ConfigMessageBridge。");
            StringAssert.Contains("_pendingCatalogSync", content,
                "AppManager 必须显式跟踪 Catalog 同步中的 bootstrap 状态。");

            var applyIndex = content.IndexOf("_gameStateCache?.ApplyGameInit(msg);", StringComparison.Ordinal);
            var clearRoomIndex = content.IndexOf("_roomCache?.Clear();", StringComparison.Ordinal);
            var transitionIndex = content.IndexOf("TransitionTo(AppState.Game);", StringComparison.Ordinal);
            Assert.That(applyIndex, Is.GreaterThanOrEqualTo(0), "AppManager 必须先写入 GameStateCache。");
            Assert.That(clearRoomIndex, Is.GreaterThan(applyIndex), "进入 Game 前必须清空大厅房间缓存，避免返回 Lobby 时残留旧房间。");
            Assert.That(transitionIndex, Is.GreaterThan(clearRoomIndex), "AppManager 必须在清理大厅房间缓存之后再切换 Game 场景。");
            Assert.That(transitionIndex, Is.GreaterThan(applyIndex), "AppManager 必须在 ApplyGameInit 之后再切换 Game 场景。");
        }

        [Test]
        public void StaticCatalogAndConfigCache_ShouldHaveSeparatedRuntimeResponsibilities()
        {
            Assert.That(File.Exists(_configCachePath), Is.True, "ConfigCache.cs 不存在。");
            Assert.That(File.Exists(_staticCatalogCachePath), Is.True, "StaticCatalogCache.cs 不存在。");
            Assert.That(File.Exists(_techTreePanelPath), Is.False, "TechTreePanelController.cs 应已删除。");
            Assert.That(File.Exists(_techTreeViewModelPath), Is.True, "TechTreeViewModel.cs 不存在。");
            Assert.That(File.Exists(_techTreeUiToolkitBinderPath), Is.True, "TechTreeUiToolkitBinder.cs 不存在。");
            Assert.That(File.Exists(_managementPanelVisibilityStorePath), Is.True, "ManagementPanelVisibilityStore.cs 不存在。");
            Assert.That(File.Exists(_recipeSynthesisViewModelPath), Is.True, "RecipeSynthesisViewModel.cs 不存在。");
            Assert.That(File.Exists(_recipeSynthesisUiToolkitBinderPath), Is.True, "RecipeSynthesisUiToolkitBinder.cs 不存在。");
            Assert.That(File.Exists(_recipeSynthesisContextStorePath), Is.True, "RecipeSynthesisContextStore.cs 不存在。");
            Assert.That(File.Exists(_buildCommandPanelPath), Is.False, "BuildCommandPanel.cs 应已删除。");
            Assert.That(File.Exists(_buildCatalogBinderPath), Is.True, "BuildCatalogUiToolkitBinder.cs 不存在。");
            Assert.That(File.Exists(_buildCatalogContextStorePath), Is.True, "BuildCatalogContextStore.cs 不存在。");
            Assert.That(File.Exists(_cityCoreProductionPanelPath), Is.False, "CityCoreProductionPanel.cs 应已删除。");

            var configCacheContent = File.ReadAllText(_configCachePath);
            var staticCatalogCacheContent = File.ReadAllText(_staticCatalogCachePath);
            var techTreeContent = File.ReadAllText(_techTreeViewModelPath);
            var techTreeBinderContent = File.ReadAllText(_techTreeUiToolkitBinderPath);
            var recipeViewModelContent = File.ReadAllText(_recipeSynthesisViewModelPath);
            var recipeBinderContent = File.ReadAllText(_recipeSynthesisUiToolkitBinderPath);
            var buildBinderContent = File.ReadAllText(_buildCatalogBinderPath);

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
            StringAssert.Contains("StaticCatalogStore", techTreeContent,
                "科技树应通过静态目录 Store 投影 UI Toolkit 状态。");
            StringAssert.Contains("PlanningDraftStore", techTreeContent,
                "科技树应通过规划草稿 Store 标记计划研究目标。");
            StringAssert.Contains("ManagementPanelVisibilityStore", techTreeBinderContent,
                "科技树 UI Toolkit binder 应订阅最终管理面板显隐状态。");
            Assert.That(recipeViewModelContent, Does.Not.Contain("ConfigCache.Instance"),
                "配方视图模型不应再通过 ConfigCache 读取静态配方实体。");
            StringAssert.Contains("RecipeSynthesisContextStore", recipeViewModelContent,
                "配方合成状态应通过最终上下文 Store 过滤。");
            StringAssert.Contains("PlanningIntentService", recipeBinderContent,
                "配方合成点击应通过最终 PlanningIntentService 提交。");
            Assert.That(buildBinderContent, Does.Not.Contain("serverConfigKey = \"buildconfig\""),
                "建造目录不应再把 buildconfig 作为正式运行时主数据源。");
            StringAssert.Contains("PlanningToolService", buildBinderContent,
                "建造目录点击应进入最终 PlanningToolService。");
        }

        [Test]
        public void PlanningDraftCache_ShouldExposeRecipeSelectionLookup_ForUiConsumers()
        {
            var cache = CreatePlanningDraftCache();
            cache.ApplyPlanningSnapshot(new MsgPlanningSnapshot
            {
                RecipeSelections =
                {
                    new QueuedRecipeSelection
                    {
                        NodeId = "A2",
                        RecipeId = "grain_mill"
                    }
                }
            });

            var lookupMethod = typeof(PlanningDraftCache).GetMethod("TryGetRecipeSelection");
            Assert.That(lookupMethod, Is.Not.Null,
                "PlanningDraftCache 应提供按 nodeId 查询配方草稿的 helper。");

            var args = new object[] { "A2", null };
            var resolved = (bool)lookupMethod!.Invoke(cache, args);
            Assert.That(resolved, Is.True, "现有配方草稿应支持按 nodeId 直接查询。");

            var selection = args[1];
            Assert.That(selection, Is.Not.Null);
            Assert.That(selection!.GetType().GetField("RecipeId")?.GetValue(selection) as string, Is.EqualTo("grain_mill"));
        }

        [Test]
        public void TechnologyAndBuildingUi_ShouldAlignToDtoQueries_AndAvoidLegacyFallbacks()
        {
            Assert.That(File.Exists(_techTreePanelPath), Is.False, "TechTreePanelController.cs 应已删除。");
            Assert.That(File.Exists(_techTreeViewModelPath), Is.True, "TechTreeViewModel.cs 不存在。");
            Assert.That(File.Exists(_techTreeUiToolkitBinderPath), Is.True, "TechTreeUiToolkitBinder.cs 不存在。");
            Assert.That(File.Exists(_managementPanelVisibilityStorePath), Is.True, "ManagementPanelVisibilityStore.cs 不存在。");
            Assert.That(File.Exists(_recipeSynthesisViewModelPath), Is.True, "RecipeSynthesisViewModel.cs 不存在。");
            Assert.That(File.Exists(_recipeSynthesisUiToolkitBinderPath), Is.True, "RecipeSynthesisUiToolkitBinder.cs 不存在。");
            Assert.That(File.Exists(_recipeSynthesisContextStorePath), Is.True, "RecipeSynthesisContextStore.cs 不存在。");
            Assert.That(File.Exists(_cityCoreBuildingActionRegistrarPath), Is.True, "CityCoreBuildingActionRegistrar.cs 不存在。");
            Assert.That(File.Exists(_resourceHudPath), Is.True, "ResourceHUD.cs 不存在。");

            var techTreeContent = File.ReadAllText(_techTreeViewModelPath);
            var techTreeBinderContent = File.ReadAllText(_techTreeUiToolkitBinderPath);
            var managementPanelVisibilityContent = File.ReadAllText(_managementPanelVisibilityStorePath);
            var recipeViewModelContent = File.ReadAllText(_recipeSynthesisViewModelPath);
            var recipeBinderContent = File.ReadAllText(_recipeSynthesisUiToolkitBinderPath);
            var registrarContent = File.ReadAllText(_cityCoreBuildingActionRegistrarPath);
            var resourceHudContent = File.ReadAllText(_resourceHudPath);

            StringAssert.Contains("StaticCatalogStore", techTreeContent,
                "科技树状态构建应消费最终静态目录 Store。");
            StringAssert.Contains("PlanningDraftStore", techTreeContent,
                "科技树状态构建应消费最终规划草稿 Store。");
            StringAssert.Contains("BindVisibility", techTreeBinderContent,
                "科技树 UI Toolkit binder 应通过显隐 Store 控制面板显隐。");
            StringAssert.Contains("Toggle(ManagementPanelId.TechTree)", resourceHudContent,
                "ResourceHUD 全局按钮应切换最终 UI Toolkit 科技树面板。");
            StringAssert.Contains("BehaviorSubject<ManagementPanelVisibilityState>", managementPanelVisibilityContent,
                "管理面板显隐状态应通过可订阅 Store 传播。");

            StringAssert.Contains("PlanningDraftStore", recipeViewModelContent,
                "配方合成 ViewModel 应通过最终规划草稿 Store 标记当前选择。");
            StringAssert.Contains("RecipeSynthesisContextStore", recipeViewModelContent,
                "配方合成 ViewModel 应按最终上下文 Store 过滤当前建筑。");
            StringAssert.Contains("SetBuildingRecipe(nodeId, recipeId)", recipeBinderContent,
                "配方合成 Binder 应通过 PlanningIntentService 提交当前节点配方。");
            StringAssert.Contains("CancelBuildingRecipe(nodeId)", recipeBinderContent,
                "配方合成 Binder 应支持取消当前节点配方。");
            StringAssert.Contains("BuildCatalogContextStore", registrarContent,
                "主城 Build 入口应把主城节点上下文写入最终建造目录上下文 Store。");
            StringAssert.Contains("Show(ManagementPanelId.BuildCatalog)", registrarContent,
                "主城 Build 入口应显示最终 UI Toolkit 建造目录。");
            StringAssert.Contains("RecipeSynthesisContextStore", registrarContent,
                "Synthesis 入口应把当前建筑上下文写入最终配方上下文 Store。");
            StringAssert.Contains("Show(ManagementPanelId.RecipeSynthesis)", registrarContent,
                "Synthesis 入口应显示最终 UI Toolkit 配方面板。");
            Assert.That(registrarContent, Does.Not.Contain("BuildCommandPanel"),
                "主城 Build 入口不应再引用 legacy BuildCommandPanel。");
            Assert.That(registrarContent, Does.Not.Contain("OnProductionActionClicked("),
                "城市核心动作注册器不应再保留 Production 入口。");
            Assert.That(registrarContent, Does.Not.Contain("OnTechTreeActionClicked("),
                "城市核心动作注册器不应再保留城市核心专属 Tech Tree 入口。");
            StringAssert.Contains("OnTechButtonClicked()", resourceHudContent,
                "科技树必须继续由 ResourceHUD 的全局按钮控制。");
        }

        [Test]
        public void LobbyPanelController_ShouldRouteErrorsAndSuccessThroughErrorToast()
        {
            Assert.That(File.Exists(_lobbyPanelControllerPath), Is.True, "LobbyPanelController.cs 不存在。");

            var content = File.ReadAllText(_lobbyPanelControllerPath);
            StringAssert.Contains("using Panoptes.Presentation.UI.Common;", content);
            StringAssert.Contains("ErrorToast errorToast", content,
                "大厅面板应通过 VContainer 注入的 ErrorToast 展示错误/成功提示。");
            Assert.That(content, Does.Not.Contain("ErrorToast.Instance"),
                "大厅面板不应再读取 ErrorToast singleton。");
            StringAssert.Contains("ShowToast(message, false);", content,
                "大厅错误提示应走 ErrorToast。");
            StringAssert.Contains("ShowToast($\"房间已创建，邀请码：{roomCode}\", true);", content,
                "创建房间成功后应给出 toast 反馈。");
        }

        [Test]
        public void LobbyScene_ShouldBindJoinButton_ToLobbyPanelController()
        {
            Assert.That(File.Exists(_lobbySceneAssetPath), Is.True, "Lobby.unity 不存在。");

            var content = File.ReadAllText(_lobbySceneAssetPath);
            var controllerIndex = content.IndexOf("LobbyPanelController", StringComparison.Ordinal);
            Assert.That(controllerIndex, Is.GreaterThanOrEqualTo(0), "LobbyPanelController 序列化块不存在。");

            var snippetLength = Math.Min(400, content.Length - controllerIndex);
            var snippet = content.Substring(controllerIndex, snippetLength);
            StringAssert.Contains("joinButton: {fileID: 999761490}", snippet,
                "Lobby 场景必须把 JoinButton 组件绑定给 LobbyPanelController，否则加入房间按钮不会注册点击事件。");
        }

        [Test]
        public void RoomPanelController_ShouldUseConfirmDialog_ForLeaveKickAndStartGame()
        {
            Assert.That(File.Exists(_roomPanelControllerPath), Is.True, "RoomPanelController.cs 不存在。");

            var content = File.ReadAllText(_roomPanelControllerPath);
            StringAssert.Contains("using Panoptes.Presentation.UI.Common;", content);
            StringAssert.Contains("ConfirmDialog confirmDialog", content,
                "房间敏感操作应通过 VContainer 注入的 ConfirmDialog 二次确认。");
            Assert.That(content, Does.Not.Contain("ConfirmDialog.Instance"),
                "房间面板不应再读取 ConfirmDialog singleton。");
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
        public void GameSceneController_ShouldShowSuccessToast_WhenOwnTechnologyCompletesOnSettlement()
        {
            Assert.That(File.Exists(_gameSceneControllerPath), Is.True, "GameSceneController.cs 不存在。");

            var catalogObject = new GameObject("StaticCatalogCache");
            var cacheObject = new GameObject("GameStateCache");
            var controllerObject = new GameObject("GameSceneController");
            GameObject toastInstance = null;

            try
            {
                var catalog = catalogObject.AddComponent<StaticCatalogCache>();
                SetSingletonInstance(typeof(StaticCatalogCache), catalog);
                Assert.That(catalog.LoadLocalCatalog(), Is.True, "StaticCatalogCache 本地目录加载失败。");
                Assert.That(catalog.TryGetTechnology("agrarian_foundations", out var technology), Is.True, "测试依赖农业基础科技目录项。");
                Assert.That(technology, Is.Not.Null);
                var staticCatalogStore = new StaticCatalogStore();
                new StaticCatalogStoreHydrator(staticCatalogStore).HydrateFromCache(catalog);

                var cache = cacheObject.AddComponent<GameStateCache>();
                SetSingletonInstance(typeof(GameStateCache), cache);
                var gameStateStore = new GameStateStore();
                var settlementStore = new SettlementStore();
                var gameOverStore = new GameOverStore();
                var feedbackStore = new GameplayFeedbackStore();
                var init = new MsgGameInit
                {
                    GameId = "game-1",
                    YourPlayerId = "player-1",
                    Turn = 1,
                    Phase = "planning",
                    MapWidth = 1,
                    MapHeight = 1,
                    MyPlayer = new PlayerView
                    {
                        Id = "player-1",
                        TokensLeft = 3,
                        CapitalCityCoreHp = 100,
                        CapitalCityCoreMaxHp = 100
                    },
                    Nodes =
                    {
                        new NodeView
                        {
                            Id = "A1",
                            Pos = new Position { Q = 0, R = 0 },
                            Terrain = "plain",
                            ControllerPlayerId = "player-1",
                            TerritoryOwnerPlayerId = "player-1",
                            BuildingTypeId = "city_core",
                            BuildingHp = 100
                        }
                    }
                };
                cache.ApplyGameInit(init);
                gameStateStore.Replace(StoreHydrationProtocolMapper.ToGameState(init));

                var toastPrefab = Resources.Load<GameObject>("Prefabs/UI/ErrorToast");
                Assert.That(toastPrefab, Is.Not.Null, "ErrorToast 运行时 prefab 不存在。");

                toastInstance = UnityEngine.Object.Instantiate(toastPrefab);
                toastInstance.hideFlags = HideFlags.HideAndDontSave;

                var toast = toastInstance.GetComponent<ErrorToast>();
                Assert.That(toast, Is.Not.Null, "ErrorToast prefab 缺少脚本组件。");
                InvokeLifecycle(toast, "Awake");
                toast.Show("错误基线", false);

                var background = toast.transform.Find("ToastRoot")?.GetComponent<Image>();
                var message = toast.transform.Find("ToastRoot/Message")?.GetComponent<TMPro.TextMeshProUGUI>();
                Assert.That(background, Is.Not.Null);
                Assert.That(message, Is.Not.Null);
                var errorColor = background.color;

                var controller = controllerObject.AddComponent<GameSceneController>();
                InjectGameSceneController(
                    controller,
                    gameStateStore,
                    settlementStore,
                    gameOverStore,
                    feedbackStore,
                    staticCatalogStore,
                    toast);
                InvokeLifecycle(controller, "Awake");
                InvokeLifecycle(controller, "OnEnable");

                var sync = new MsgGameSync
                {
                    Turn = 1,
                    Phase = "resolving",
                    NextPhase = "planning",
                    MyPlayer = new PlayerView
                    {
                        Id = "player-1",
                        TokensLeft = 3,
                        CapitalCityCoreHp = 100,
                        CapitalCityCoreMaxHp = 100
                    },
                    Nodes =
                    {
                        new NodeView
                        {
                            Id = "A1",
                            Pos = new Position { Q = 0, R = 0 },
                            Terrain = "plain",
                            ControllerPlayerId = "player-1",
                            TerritoryOwnerPlayerId = "player-1",
                            BuildingTypeId = "city_core",
                            BuildingHp = 100
                        }
                    },
                    Events =
                    {
                        new DomainEventEnvelope
                        {
                            Channel = "economy",
                            Kind = "technology_completed",
                            Data =
                            {
                                { "technology_id", "agrarian_foundations" },
                                { "player_id", "player-1" }
                            }
                        },
                        new DomainEventEnvelope
                        {
                            Channel = "economy",
                            Kind = "technology_completed",
                            Data =
                            {
                                { "technology_id", "organized_labor" },
                                { "player_id", "player-2" }
                            }
                        }
                    }
                };
                cache.ApplyGameSync(sync);
                gameStateStore.Replace(StoreHydrationProtocolMapper.MergeGameSync(gameStateStore.Snapshot, sync));
                settlementStore.Replace(SettlementMapper.ToDto(sync));

                Assert.That(message.text, Is.EqualTo("科技研究完成：农业基础"));
                Assert.That(background.color, Is.Not.EqualTo(errorColor));
                Assert.That(background.color.g, Is.GreaterThan(background.color.r),
                    "科技完成提示应使用成功态绿色底。");

                InvokeLifecycle(controller, "OnDisable");
            }
            finally
            {
                if (toastInstance != null)
                {
                    UnityEngine.Object.DestroyImmediate(toastInstance);
                }

                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(cacheObject);
                UnityEngine.Object.DestroyImmediate(catalogObject);
            }
        }

        [Test]
        public void GameSceneController_ShouldSubscribeSettlementStore_ForTechnologyCompletionToast()
        {
            Assert.That(File.Exists(_gameSceneControllerPath), Is.True, "GameSceneController.cs 不存在。");

            var content = File.ReadAllText(_gameSceneControllerPath);
            StringAssert.Contains("SettlementStore", content,
                "GameSceneController 应通过 SettlementStore 展示科技完成提示。");
            StringAssert.Contains("_settlementStore?.State.Subscribe", content,
                "GameSceneController 应订阅结算 Store 状态。");
            StringAssert.DoesNotContain("OnTurnSettled", content,
                "GameSceneController 不应再订阅 legacy GameStateCache 结算事件。");
            StringAssert.Contains("technology_completed", content,
                "GameSceneController 应识别 technology_completed 结算事件。");
            StringAssert.Contains("科技研究完成：", content,
                "GameSceneController 应为科技完成生成明确 toast 文案。");
        }

        [Test]
        public void GamePhases_ShouldExposePlanningResolvingAndTurnReport()
        {
            Assert.That(File.Exists(_gamePhasesPath), Is.True, "GamePhases.cs 不存在。");

            var content = File.ReadAllText(_gamePhasesPath);
            StringAssert.Contains("public const string Planning = \"planning\";", content);
            StringAssert.Contains("public const string Resolving = \"resolving\";", content);
            StringAssert.Contains("public const string TurnReport = \"turn_report\";", content);
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
            StringAssert.DoesNotContain("EnsureComponent<ResourceHUD>", content,
                "ResourceHUD 应由 ResourcePanel prefab + VContainer 场景注册装配，GameSceneController 不应运行时补组件。");
            StringAssert.DoesNotContain("EnsureRuntimeComponent<SettlementPlaybackController>", content,
                "Settlement playback 应由 VContainer + 场景实例装配，GameSceneController 不应动态创建。");
            Assert.That(content, Does.Not.Contain("EnsureComponent<StrategicPanel>"),
                "Game 场景不应再装配 StrategicPanel。");
            Assert.That(content, Does.Not.Contain("EnsureComponent<UnitOrdersPanel>"),
                "Game 场景不应再装配独立 UnitOrdersPanel。");
            Assert.That(content, Does.Not.Contain("MicroPanel"));
            Assert.That(content, Does.Not.Contain("OrderReviewPanel"));
            Assert.That(content, Does.Not.Contain("CombatPlaybackController"));
        }

        [Test]
        public void MapPlanningInputController_ShouldOnlyIssueMoveOrders_FromAuthoritativePreview()
        {
            Assert.That(File.Exists(_mapPlanningInputControllerPath), Is.True, "MapPlanningInputController.cs 不存在。");

            var content = ReadMapPlanningInputControllerSources(_mapPlanningInputControllerPath);
            StringAssert.Contains("TryIssueAuthoritativeMoveOrder(node.NodeId)", content,
                "移动点击应只通过服务端权威 preview 结果发单。");
            StringAssert.Contains("TryGetCurrentMovePreview", content,
                "MapPlanningInputController 应读取当前服务端 preview，而不是继续走本地规则。");
            Assert.That(File.Exists(_movePreviewPresenterPath), Is.True, "MovePreviewPresenter.cs 不存在。");
            StringAssert.Contains("ResolveErrorMessage", File.ReadAllText(_movePreviewPresenterPath),
                "无效 preview 应通过 presenter 给出明确反馈。");
            Assert.That(content, Does.Not.Contain("Backward-compatible quick move"),
                "不应继续保留基于本地高亮的快速移动兼容壳。");
            Assert.That(content, Does.Not.Contain("moveRange = 4"),
                "不应继续使用本地固定移动范围假高亮。");
        }

        [Test]
        public void MapPlanningInputController_ShouldRequireExplicitBuildCityContext()
        {
            Assert.That(File.Exists(_mapPlanningInputControllerPath), Is.True, "MapPlanningInputController.cs 不存在。");

            var content = ReadMapPlanningInputControllerSources(_mapPlanningInputControllerPath);
            var buildPlacementContent = ReadMapPlanningBuildPlacementSources(_mapPlanningInputControllerPath);
            StringAssert.Contains("public void EnterBuildPlacementAny(string buildingType, string cityId)", content,
                "建造入口应显式要求 cityId。");
            StringAssert.Contains("public void EnterBuildPlacementResource(string buildingType, string cityId)", content,
                "资源建筑入口应显式要求 cityId。");
            StringAssert.Contains("public void EnterBuildPlacementCity(string buildingType, string cityId)", content,
                "城内建筑入口应显式要求 cityId。");
            StringAssert.Contains("_activeBuildCityId = string.IsNullOrWhiteSpace(cityId) ? string.Empty : cityId.Trim();", buildPlacementContent,
                "建造模式应保存显式传入的 cityId，而不是临时猜测。");
            StringAssert.Contains("BuildToken(nodeId, buildingType, _activeBuildCityId)", buildPlacementContent,
                "建造消息必须透传显式 cityId。");
            Assert.That(content, Does.Not.Contain("SetBuildCastleContext"),
                "不应再保留隐藏式 SetBuildCastleContext 兼容入口。");
            Assert.That(content, Does.Not.Contain("TryResolveBuildCityId"),
                "不应再在客户端本地猜测 cityId。");
            StringAssert.Contains("缺少建造城市上下文，无法进入建造模式", buildPlacementContent,
                "缺少 cityId 时应在进入建造模式前直接失败。");
        }

        [Test]
        public void MapPlanningInputController_ShouldNotGateCommandsByLocalPlacementOrTargetRules()
        {
            Assert.That(File.Exists(_mapPlanningInputControllerPath), Is.True, "MapPlanningInputController.cs 不存在。");

            var content = ReadMapPlanningInputControllerSources(_mapPlanningInputControllerPath);
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
        public void MapPlanningInputController_ShouldPreferOwnedUnitSelectionBeforeBuildingInfo_OnSharedCityCoreTile()
        {
            Assert.That(File.Exists(_mapPlanningInputControllerPath), Is.True, "MapPlanningInputController.cs 不存在。");

            var content = File.ReadAllText(_mapPlanningInputControllerPath);
            var unitSelectionIndex = content.IndexOf("TrySelectOwnedUnitFromNodeClick()", StringComparison.Ordinal);
            var buildingInfoIndex = content.IndexOf("TryOpenBuildingInfoFromClick()", StringComparison.Ordinal);

            Assert.That(unitSelectionIndex, Is.GreaterThanOrEqualTo(0),
                "同格节点交互应先尝试选中己方可控单位。");
            Assert.That(buildingInfoIndex, Is.GreaterThan(unitSelectionIndex),
                "建筑信息打开逻辑必须排在同格单位优先判定之后。");
        }

        [Test]
        public void MapPlanningInputController_ShouldBeSingleUnityEntryAndMovePlanningHelpersOutOfMapInput()
        {
            Assert.That(File.Exists(_mapPlanningInputControllerPath), Is.True, "MapPlanningInputController.cs 不存在。");

            var mapDirectory = Path.GetDirectoryName(_mapPlanningInputControllerPath);
            Assert.That(mapDirectory, Is.Not.Null);

            var mapPlanningInputControllerFiles = Directory
                .GetFiles(mapDirectory, "MapPlanningInputController*.cs", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .OrderBy(fileName => fileName, StringComparer.Ordinal)
                .ToArray();

            Assert.That(mapPlanningInputControllerFiles, Is.EqualTo(new[] { "MapPlanningInputController.cs" }),
                "MapPlanningInputController 应保持单一 Unity 入口，不应继续通过 partial 文件膨胀。");
            Assert.That(File.Exists(Path.Combine(mapDirectory, "MapInputHandler.cs")), Is.False,
                "旧 MapInputHandler 命名应彻底移除。");

            var oldInputDirectory = Path.Combine(mapDirectory, "Input");
            var oldInputScripts = Directory.Exists(oldInputDirectory)
                ? Directory.GetFiles(oldInputDirectory, "*.cs", SearchOption.AllDirectories)
                : Array.Empty<string>();
            Assert.That(oldInputScripts, Is.Empty,
                "规划输入和反馈 helper 不应继续停留在 Presentation/Map/Input。");

            var planningRoot = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Planning");
            Assert.That(Directory.Exists(planningRoot), Is.True, "Planning 表现层包不存在。");
            Assert.That(File.Exists(Path.Combine(planningRoot, "Feedback/BuildPlacementGhostPresenter.cs")), Is.True);
            Assert.That(File.Exists(Path.Combine(planningRoot, "Feedback/MovePreviewPresenter.cs")), Is.True);
            Assert.That(File.Exists(Path.Combine(planningRoot, "Feedback/MovePreviewGhostPresenter.cs")), Is.True);
            Assert.That(File.Exists(Path.Combine(planningRoot, "Input/State/PendingMoveState.cs")), Is.True);
        }

        [Test]
        public void CityCoreRuntimeActions_ShouldUseCityCoreWithoutCastleAlias()
        {
            Assert.That(File.Exists(_cityCoreBuildingActionRegistrarPath), Is.True, "CityCoreBuildingActionRegistrar.cs 不存在。");

            var registrarContent = File.ReadAllText(_cityCoreBuildingActionRegistrarPath);
            var resolverContent = File.ReadAllText(Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/CityCoreBuildingActionResolver.cs"));

            StringAssert.Contains("\"city_core\"", resolverContent,
                "主城动作注册必须显式接受 city_core。");
            Assert.That(registrarContent, Does.Not.Contain("= \"castle\""),
                "主城动作注册不应再保留 castle 运行时别名。");
            Assert.That(registrarContent, Does.Not.Contain("castle_open_"),
                "主城动作注册不应再保留旧 action id 兼容入口。");
            Assert.That(registrarContent, Does.Not.Contain("building_open_recipe"),
                "主城动作注册不应再保留旧配方 action id 兼容入口。");
            StringAssert.Contains("buildActionId = \"action_3\"", registrarContent,
                "城市核心动作应继续保留 Build。");
            StringAssert.Contains("recipeActionId = \"open_recipe_synthesis\"", registrarContent,
                "城市核心动作应继续保留统一配方入口。");
            Assert.That(registrarContent, Does.Not.Contain("productionActionId"),
                "城市核心动作不应再注册 Production。");
            Assert.That(registrarContent, Does.Not.Contain("techTreeActionId"),
                "城市核心动作不应再注册城市核心专属 Tech Tree。");
        }

        [Test]
        public void CityCoreRuntimeNaming_ShouldBeUnifiedAcrossCriticalPresentationChain()
        {
            Assert.That(File.Exists(_cityCoreHpBarPath), Is.True, "CityCoreHPBar.cs 不存在。");
            Assert.That(File.Exists(_cityCoreHpBarOverlayControllerPath), Is.True, "CityCoreHpBarOverlayController.cs 不存在。");
            Assert.That(File.Exists(_buildingViewPath), Is.True, "BuildingView.cs 不存在。");
            Assert.That(File.Exists(_buildCommandPanelPath), Is.False, "BuildCommandPanel.cs 应已删除。");
            Assert.That(File.Exists(_buildCatalogBinderPath), Is.True, "BuildCatalogUiToolkitBinder.cs 不存在。");
            Assert.That(File.Exists(_buildCatalogContextStorePath), Is.True, "BuildCatalogContextStore.cs 不存在。");
            Assert.That(File.Exists(_cityCorePrefabAssetPath), Is.True, "CityCore.prefab 不存在。");
            Assert.That(File.Exists(_cityCoreHpBarPrefabPath), Is.True, "CityCoreHPBar.prefab 不存在。");
            Assert.That(File.Exists(_cityCoreProductionPanelPrefabPath), Is.False, "CityCoreProductionPanel.prefab 应已删除。");

            var hpBarContent = File.ReadAllText(_cityCoreHpBarPath);
            var overlayContent = File.ReadAllText(_cityCoreHpBarOverlayControllerPath);
            var buildingViewContent = File.ReadAllText(_buildingViewPath);
            var buildBinderContent = File.ReadAllText(_buildCatalogBinderPath);
            var buildContextContent = File.ReadAllText(_buildCatalogContextStorePath);
            var hpBarPrefabContent = File.ReadAllText(_cityCoreHpBarPrefabPath);

            Assert.That(hpBarContent, Does.Not.Contain("Castle"),
                "主城血条脚本不应再保留 Castle 命名。");
            Assert.That(overlayContent, Does.Not.Contain("Castle"),
                "主城覆盖层脚本不应再保留 Castle 命名。");
            Assert.That(buildingViewContent, Does.Not.Contain("Castle"),
                "建筑视图脚本不应再保留 Castle 命名。");
            StringAssert.Contains("PlanningToolService", buildBinderContent,
                "建造目录点击应通过最终规划工具服务。");
            StringAssert.Contains("CityCoreNodeId", buildContextContent,
                "建造目录应保留主城节点上下文。");
            StringAssert.Contains("Panoptes.Presentation::Panoptes.Presentation.UI.HUD.CityCoreHPBar", hpBarPrefabContent,
                "主城血条 prefab 应绑定 CityCoreHPBar 组件。");
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
            StringAssert.Contains("SettlementStore", content,
                "结算回放应订阅 SettlementStore。");
            StringAssert.Contains("MapRenderer _mapRenderer", content,
                "结算回放应使用注入的 MapRenderer。");
            StringAssert.Contains("[Inject]", content,
                "结算回放应由 VContainer 注入依赖。");
            StringAssert.Contains(".State.Subscribe", content,
                "结算回放应订阅 Store 状态流。");
            StringAssert.Contains("state.Sequence", content,
                "结算回放应按 Store sequence 去重。");
            StringAssert.DoesNotContain("GameStateCache", content,
                "结算回放不应直接消费 legacy GameStateCache。");
            StringAssert.DoesNotContain("OnTurnSettled", content,
                "结算回放不应订阅 legacy OnTurnSettled。");
            StringAssert.DoesNotContain("MapRenderer.Instance", content,
                "结算回放应使用注入的 MapRenderer。");
            StringAssert.DoesNotContain("EnsureInstance", content,
                "结算回放不应保留动态创建入口。");
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
        public void UnitInfoPanel_ShouldRenderThroughViewModelBinder_AndDelegateDirectOrderActions()
        {
            Assert.That(File.Exists(_unitInfoPanelPath), Is.True, "UnitInfoPanelController.cs 不存在。");
            Assert.That(File.Exists(_unitInfoDirectOrderPanelBinderPath), Is.True, "UnitInfoDirectOrderPanelBinder.cs 不存在。");
            Assert.That(
                File.Exists(Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/UnitInfoPlanningSummaryPresenter.cs")),
                Is.False,
                "UnitInfo 规划摘要应由 ViewModel/Binder 渲染，不应保留 legacy presenter。");
            Assert.That(
                File.Exists(Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/UnitInfoHpStateResolver.cs")),
                Is.False,
                "UnitInfo HP 应由 ViewModel/Binder 渲染，不应保留 legacy resolver。");
            Assert.That(
                File.Exists(Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/UnitInfoDirectOrderStateResolver.cs")),
                Is.False,
                "UnitInfo direct-order 状态应由 ViewModel 投影，不应保留 legacy resolver。");

            var content = File.ReadAllText(_unitInfoPanelPath);
            var directOrderContent = File.ReadAllText(_unitInfoDirectOrderPanelBinderPath);
            StringAssert.Contains("UnitInfoViewModel", content,
                "UnitInfoPanel 应通过 ViewModel 选择当前单位。");
            StringAssert.Contains("UnitInfoUguiBinder", content,
                "UnitInfoPanel 应通过 Binder 渲染文本、HP、规划摘要和 direct-order 状态。");
            StringAssert.DoesNotContain("PlanningDraftCache", content,
                "UnitInfoPanel 不应再直接消费规划草稿缓存。");
            StringAssert.DoesNotContain("GameStateCache", content,
                "UnitInfoPanel 不应再直接消费游戏状态缓存。");
            StringAssert.DoesNotContain("StaticCatalogCache", content,
                "UnitInfoPanel 不应再直接消费静态目录缓存。");
            StringAssert.Contains("UnitInfoDirectOrderPanelBinder", content,
                "UnitInfoPanel 应委托 direct-order helper 渲染和绑定按钮。");
            StringAssert.Contains("BeginMoveSelection", directOrderContent,
                "direct-order helper 应绑定移动命令入口。");
            StringAssert.Contains("BeginAttackSelection", directOrderContent,
                "direct-order helper 应绑定攻击命令入口。");
            StringAssert.Contains("IssueHoldOrder", directOrderContent,
                "direct-order helper 应绑定待命命令入口。");
            StringAssert.Contains("BeginChargeSelection", directOrderContent,
                "direct-order helper 应绑定冲锋命令入口。");
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
                case MsgGameSync gameSync:
                    gameEvent.GameSync = gameSync;
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

        private static void InjectGameSceneController(
            GameSceneController controller,
            GameStateStore gameStateStore,
            SettlementStore settlementStore,
            GameOverStore gameOverStore,
            GameplayFeedbackStore feedbackStore,
            StaticCatalogStore staticCatalogStore,
            ErrorToast errorToast)
        {
            var method = typeof(GameSceneController).GetMethod("Construct",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (method == null)
            {
                throw new AssertionException("缺少 GameSceneController.Construct 注入方法。");
            }

            method.Invoke(
                controller,
                new object[]
                {
                    gameStateStore,
                    settlementStore,
                    gameOverStore,
                    feedbackStore,
                    staticCatalogStore,
                    errorToast
                });
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

        private static string ReadMapPlanningInputControllerSources(string mapPlanningInputControllerPath)
        {
            Assert.That(File.Exists(mapPlanningInputControllerPath), Is.True, "MapPlanningInputController.cs 不存在。");

            var mapDirectory = Path.GetDirectoryName(mapPlanningInputControllerPath);
            Assert.That(mapDirectory, Is.Not.Null);

            return string.Join("\n", Directory
                .GetFiles(mapDirectory, "MapPlanningInputController*.cs", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        }

        private static string ReadMapPlanningBuildPlacementSources(string mapPlanningInputControllerPath)
        {
            var mapDirectory = Path.GetDirectoryName(mapPlanningInputControllerPath);
            Assert.That(mapDirectory, Is.Not.Null);

            return string.Join("\n", new[]
            {
                mapPlanningInputControllerPath,
                Path.Combine(mapDirectory!, "MapBuildPlacementSession.cs"),
                Path.Combine(mapDirectory!, "../Planning/Input/MapBuildPlacementSession.cs")
            }
                .Select(Path.GetFullPath)
                .Where(File.Exists)
                .Select(File.ReadAllText));
        }

        private static PlanningDraftCache CreatePlanningDraftCache()
        {
            return new GameObject("PlanningDraftCache").AddComponent<PlanningDraftCache>();
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
