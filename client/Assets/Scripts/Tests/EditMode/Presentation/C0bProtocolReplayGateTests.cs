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
    public sealed class C0bProtocolReplayGateTests
    {
        private GameObject _dispatcherObject;
        private StoreMessageHydrator _hydrator;

        [TearDown]
        public void TearDown()
        {
            _hydrator?.Dispose();
            _hydrator = null;

            if (_dispatcherObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_dispatcherObject);
                _dispatcherObject = null;
            }

            ResetMessageDispatcherSingleton();
            typeof(GameStateCache)
                .GetProperty(nameof(GameStateCache.Instance), BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(true)
                ?.Invoke(null, new object[] { null });
        }

        [Test]
        public void ServerFrames_ShouldHydrateStoresAndManagementViewModelsOffline()
        {
            ResetMessageDispatcherSingleton();
            var dispatcher = CreateDispatcher();
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
            dispatcher.Register<MsgStaticCatalogSnapshot>(
                "MsgStaticCatalogSnapshot",
                msg => catalogHydrator.HydrateFromSnapshot(msg?.Snapshot));

            using var techTree = new TechTreeViewModel(catalogStore, draftStore);
            using var buildCatalog = new BuildCatalogViewModel(catalogStore, draftStore, gameStore);
            using var overview = new NationalOverviewViewModel(
                gameStore,
                turnStore,
                draftStore,
                catalogStore,
                settlementStore);

            var frames = LoadFixtureFrames();
            Assert.That(frames, Has.Count.EqualTo(7), "C0b fixture should cover catalog, init, planning, command, sync, and next planning start.");

            dispatcher.Dispatch(frames[0]);
            Assert.That(catalogStore.Snapshot.Technologies["agri_unlock_farm"].Name, Is.EqualTo("Agrarian Foundations"));
            Assert.That(catalogStore.Snapshot.Buildings["farm"].PlacementKind, Is.EqualTo("city_territory"));

            dispatcher.Dispatch(frames[1]);
            dispatcher.Dispatch(frames[2]);
            Assert.That(gameStore.Snapshot.ActiveGameSessionId, Is.EqualTo("research_unlock_build"));
            Assert.That(gameStore.Snapshot.Nodes, Contains.Key("A1"));
            Assert.That(turnStore.Snapshot.IsInteractive, Is.True);

            dispatcher.Dispatch(frames[3]);
            Assert.That(draftStore.Snapshot.PlannedResearchTargetTechnologyId, Is.EqualTo("agri_unlock_farm"));
            Assert.That(FindTechRow(techTree.Current, "agri_unlock_farm").Status, Is.EqualTo("Planned research"));
            Assert.That(overview.Current.PlannedResearchText, Is.EqualTo("Agrarian Foundations"));

            dispatcher.Dispatch(frames[4]);
            dispatcher.Dispatch(frames[5]);
            dispatcher.Dispatch(frames[6]);

            Assert.That(gameStore.Snapshot.Turn, Is.EqualTo(2));
            Assert.That(turnStore.Snapshot.Turn, Is.EqualTo(2));
            Assert.That(turnStore.Snapshot.PlanningStartEvents.Any(evt => evt.Type == "technology_activated"), Is.True);
            Assert.That(draftStore.Snapshot.BuildOrders[0].BuildingTypeId, Is.EqualTo("farm"));
            Assert.That(FindBuildItem(buildCatalog.Current, "farm").IsPending, Is.True);
            Assert.That(overview.Current.Events[0].Title, Is.EqualTo("technology activated"));
        }

        private MessageDispatcher CreateDispatcher()
        {
            _dispatcherObject = new GameObject("C0bMessageDispatcher");
            return _dispatcherObject.AddComponent<MessageDispatcher>();
        }

        private static IReadOnlyList<ServerFrame> LoadFixtureFrames()
        {
            var path = ResolveFixturePath();
            Assert.That(File.Exists(path), Is.True, $"Missing C0b fixture at {path}");

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
            var localPath = Path.GetFullPath("Assets/Scripts/Tests/EditMode/Fixtures/C0b/server_frames.jsonl");
            if (File.Exists(localPath))
            {
                return localPath;
            }

            return Path.GetFullPath("client/Assets/Scripts/Tests/EditMode/Fixtures/C0b/server_frames.jsonl");
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
    }
}
