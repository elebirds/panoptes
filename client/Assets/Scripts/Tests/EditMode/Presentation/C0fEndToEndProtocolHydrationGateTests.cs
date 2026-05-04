using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Presentation.ViewModels;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class C0fEndToEndProtocolHydrationGateTests
    {
        private GameObject _dispatcherObject;
        private GameObject _catalogCacheObject;
        private StoreMessageHydrator _hydrator;

        [TearDown]
        public void TearDown()
        {
            _hydrator?.Dispose();
            _hydrator = null;

            if (_catalogCacheObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_catalogCacheObject);
                _catalogCacheObject = null;
            }

            if (_dispatcherObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_dispatcherObject);
                _dispatcherObject = null;
            }

            ResetSingleton<StaticCatalogCache>(nameof(StaticCatalogCache.Instance));
            ResetSingleton<GameStateCache>(nameof(GameStateCache.Instance));
            ResetMessageDispatcherSingleton();
        }

        [Test]
        public void BackendWebSocketFrames_ShouldHydrateClientStoresAndManagementViewModels()
        {
            ResetSingleton<StaticCatalogCache>(nameof(StaticCatalogCache.Instance));
            ResetSingleton<GameStateCache>(nameof(GameStateCache.Instance));
            ResetMessageDispatcherSingleton();

            var dispatcher = CreateDispatcher();
            var staticCatalogCache = CreateStaticCatalogCache();
            var gameStore = new GameStateStore();
            var draftStore = new PlanningDraftStore();
            var catalogStore = new StaticCatalogStore();
            var turnStore = new TurnStore();
            var settlementStore = new SettlementStore();
            var catalogHydrator = new StaticCatalogStoreHydrator(catalogStore);

            _hydrator = new StoreMessageHydrator(
                dispatcher,
                new StoreHydrationHelper(gameStore, draftStore, catalogStore, turnStore),
                gameStore,
                draftStore,
                turnStore,
                new GameChatStore(),
                new GameOverStore(),
                settlementStore,
                new GameplayFeedbackStore());
            _hydrator.Attach();

            using var techTree = new TechTreeViewModel(catalogStore, draftStore);
            using var buildCatalog = new BuildCatalogViewModel(catalogStore, draftStore);
            using var overview = new NationalOverviewViewModel(
                gameStore,
                turnStore,
                draftStore,
                catalogStore,
                settlementStore);

            var messageCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var sawPlannedResearchSnapshot = false;
            var sawSuccessfulResearchResult = false;
            var sawCatalogSyncComplete = false;

            dispatcher.OnDispatching += entry =>
            {
                messageCounts.TryGetValue(entry.MessageType, out var count);
                messageCounts[entry.MessageType] = count + 1;
            };
            RegisterStaticCatalogHandlers(dispatcher, staticCatalogCache, catalogHydrator, () =>
            {
                sawCatalogSyncComplete = true;
            });
            RegisterProtocolOnlyNoopHandlers(dispatcher);
            dispatcher.Register<MsgPlanningSnapshot>("MsgPlanningSnapshot", msg =>
            {
                if (!string.Equals(msg?.PlannedResearchTargetTechnologyId, "agri_unlock_farm", StringComparison.Ordinal))
                {
                    return;
                }

                sawPlannedResearchSnapshot = true;
                Assert.That(draftStore.Snapshot.PlannedResearchTargetTechnologyId, Is.EqualTo("agri_unlock_farm"));
                Assert.That(FindTechRow(techTree.Current, "agri_unlock_farm").Status, Is.EqualTo("Planned research"));
            });
            dispatcher.Register<MsgResearchResult>("MsgResearchResult", msg =>
            {
                sawSuccessfulResearchResult = msg != null &&
                                              msg.Success &&
                                              string.Equals(msg.TechnologyId, "agri_unlock_farm", StringComparison.Ordinal);
            });

            var frames = LoadFixtureFrames();
            Assert.That(frames, Has.Count.GreaterThanOrEqualTo(20), "C0f fixture should include auth, lobby, catalog sync, game bootstrap, planning command, and turn resolution frames.");

            foreach (var frame in frames)
            {
                dispatcher.Dispatch(frame);
            }

            AssertMessageSeen(messageCounts, "MsgClientRuntimeConfig");
            AssertMessageSeen(messageCounts, "MsgRoomCreated");
            AssertMessageSeen(messageCounts, "MsgRoomState", 3);
            AssertMessageSeen(messageCounts, "MsgGameStarting");
            AssertMessageSeen(messageCounts, "MsgStaticCatalogManifest");
            AssertMessageSeen(messageCounts, "MsgStaticCatalogSectionChunk", 1);
            AssertMessageSeen(messageCounts, "MsgStaticCatalogSyncComplete");
            AssertMessageSeen(messageCounts, "MsgGameInit");
            AssertMessageSeen(messageCounts, "MsgPlanningStart", 2);
            AssertMessageSeen(messageCounts, "MsgPlanningSnapshot");
            AssertMessageSeen(messageCounts, "MsgResearchResult");
            AssertMessageSeen(messageCounts, "MsgGameSync");

            Assert.That(sawCatalogSyncComplete, Is.True);
            Assert.That(sawPlannedResearchSnapshot, Is.True);
            Assert.That(sawSuccessfulResearchResult, Is.True);
            Assert.That(catalogStore.Snapshot.Technologies, Contains.Key("agri_unlock_farm"));
            Assert.That(catalogStore.Snapshot.Buildings["farm"].DefaultRecipeId, Is.EqualTo("farm_food"));
            Assert.That(catalogStore.Snapshot.Recipes, Contains.Key("farm_food"));

            Assert.That(gameStore.Snapshot.ActiveGameSessionId, Is.EqualTo("research_unlock_build"));
            Assert.That(gameStore.Snapshot.Turn, Is.EqualTo(2));
            Assert.That(gameStore.Snapshot.Nodes, Contains.Key("B2"));
            Assert.That(turnStore.Snapshot.Turn, Is.EqualTo(2));
            Assert.That(turnStore.Snapshot.PlanningStartEvents.Any(evt => evt.Type == "technology_activated"), Is.True);

            Assert.That(draftStore.Snapshot.BuildOrders, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(draftStore.Snapshot.BuildOrders[0].BuildingTypeId, Is.EqualTo("farm"));
            Assert.That(FindBuildItem(buildCatalog.Current, "farm").IsPending, Is.True);
            Assert.That(overview.Current.Events.Any(evt => evt.Title == "technology activated"), Is.True);
        }

        private MessageDispatcher CreateDispatcher()
        {
            _dispatcherObject = new GameObject("C0fMessageDispatcher");
            return _dispatcherObject.AddComponent<MessageDispatcher>();
        }

        private StaticCatalogCache CreateStaticCatalogCache()
        {
            _catalogCacheObject = new GameObject("C0fStaticCatalogCache");
            return _catalogCacheObject.AddComponent<StaticCatalogCache>();
        }

        private static void RegisterStaticCatalogHandlers(
            MessageDispatcher dispatcher,
            StaticCatalogCache cache,
            StaticCatalogStoreHydrator hydrator,
            Action onSyncComplete)
        {
            dispatcher.Register<MsgStaticCatalogManifest>("MsgStaticCatalogManifest", msg =>
            {
                var decision = cache.CompareManifest(msg?.Manifest);
                hydrator.HydrateFromCache(cache);
                cache.BeginSectionSync(msg?.Manifest, decision.RequestedSections);
            });
            dispatcher.Register<MsgStaticCatalogSectionChunk>("MsgStaticCatalogSectionChunk", cache.ApplySectionChunk);
            dispatcher.Register<MsgStaticCatalogSyncComplete>("MsgStaticCatalogSyncComplete", msg =>
            {
                Assert.That(cache.FinalizeSectionSync(msg), Is.True);
                hydrator.HydrateFromCache(cache);
                onSyncComplete?.Invoke();
            });
            dispatcher.Register<MsgStaticCatalogSnapshot>("MsgStaticCatalogSnapshot", msg =>
            {
                cache.ApplySnapshot(msg?.Snapshot);
                hydrator.HydrateFromCache(cache);
            });
        }

        private static void RegisterProtocolOnlyNoopHandlers(MessageDispatcher dispatcher)
        {
            dispatcher.Register<MsgClientRuntimeConfig>("MsgClientRuntimeConfig", _ => { });
            dispatcher.Register<MsgRoomCreated>("MsgRoomCreated", _ => { });
            dispatcher.Register<MsgRoomState>("MsgRoomState", _ => { });
            dispatcher.Register<MsgGameStarting>("MsgGameStarting", _ => { });
            dispatcher.Register<MsgConfigBatchJson>("MsgConfigBatchJson", _ => { });
            dispatcher.Register<Problem>("Problem", msg => Assert.Fail($"Unexpected problem frame: {msg?.Code}"));
        }

        private static IReadOnlyList<ServerFrame> LoadFixtureFrames()
        {
            var path = ResolveFixturePath();
            Assert.That(File.Exists(path), Is.True, $"Missing C0f fixture at {path}");

            var frames = new List<ServerFrame>();
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                frames.Add(JsonParser.Default.Parse<ServerFrame>(line));
            }

            return frames;
        }

        private static string ResolveFixturePath()
        {
            var localPath = Path.GetFullPath("Assets/Scripts/Tests/EditMode/Fixtures/C0f/server_frames.jsonl");
            if (File.Exists(localPath))
            {
                return localPath;
            }

            return Path.GetFullPath("client/Assets/Scripts/Tests/EditMode/Fixtures/C0f/server_frames.jsonl");
        }

        private static void AssertMessageSeen(Dictionary<string, int> messageCounts, string messageType, int minimum = 1)
        {
            messageCounts.TryGetValue(messageType, out var count);
            Assert.That(count, Is.GreaterThanOrEqualTo(minimum), $"{messageType} should appear at least {minimum} time(s).");
        }

        private static ManagementPanelRowState FindTechRow(ManagementPanelState state, string rowId)
        {
            return state.Groups
                .SelectMany(group => group.Rows)
                .First(row => string.Equals(row.Id, rowId, StringComparison.Ordinal));
        }

        private static BuildCatalogItemState FindBuildItem(BuildCatalogState state, string buildingId)
        {
            return state.Groups
                .SelectMany(group => group.Items)
                .First(item => string.Equals(item.BuildingId, buildingId, StringComparison.Ordinal));
        }

        private static void ResetMessageDispatcherSingleton()
        {
            typeof(MessageDispatcher)
                .GetProperty(nameof(MessageDispatcher.Instance), BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(true)
                ?.Invoke(null, new object[] { null });
        }

        private static void ResetSingleton<T>(string propertyName)
        {
            typeof(T)
                .GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(true)
                ?.Invoke(null, new object[] { null });
        }
    }
}
