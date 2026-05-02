using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class StoreHydrationHelperTests
    {
        [Test]
        public void Hydrate_ShouldPublishRepresentativeStoreStates()
        {
            var gameStateStore = new GameStateStore();
            var planningDraftStore = new PlanningDraftStore();
            var staticCatalogStore = new StaticCatalogStore();
            var turnStore = new TurnStore();
            var helper = new StoreHydrationHelper(
                gameStateStore,
                planningDraftStore,
                staticCatalogStore,
                turnStore);
            var observedGameStates = new List<GameStateStoreState>();
            var observedPlanningDrafts = new List<PlanningDraftState>();
            var observedCatalogs = new List<StaticCatalogState>();
            var observedTurns = new List<TurnState>();

            using var gameSubscription = gameStateStore.State.Subscribe(
                observedGameStates,
                static (state, target) => target.Add(state));
            using var draftSubscription = planningDraftStore.State.Subscribe(
                observedPlanningDrafts,
                static (state, target) => target.Add(state));
            using var catalogSubscription = staticCatalogStore.State.Subscribe(
                observedCatalogs,
                static (state, target) => target.Add(state));
            using var turnSubscription = turnStore.State.Subscribe(
                observedTurns,
                static (state, target) => target.Add(state));

            helper.Hydrate(new StoreHydrationSnapshot(
                gameState: new GameStateStoreState(
                    gameId: "game-1",
                    activeGameSessionId: "session-1",
                    myPlayerId: "player-1",
                    turn: 6,
                    phase: "planning",
                    mapWidth: 8,
                    mapHeight: 7,
                    tokensLeft: 3,
                    nodes: new Dictionary<string, NodeDto>
                    {
                        ["n1"] = new NodeDto { Id = "n1", Owner = "player-1", Q = 1, R = -1 }
                    },
                    units: new Dictionary<string, UnitDto>
                    {
                        ["u1"] = new UnitDto { Id = "u1", Type = "scout", Owner = "player-1", Hp = 5, MaxHp = 6 }
                    },
                    myResources: new ResourceDto { Food = 9, Wood = 4 }),
                planningDraft: new PlanningDraftState(
                    snapshotTurn: 6,
                    snapshotPhase: "planning",
                    unitOrders: new[]
                    {
                        new QueuedUnitOrderDto
                        {
                            UnitId = "u1",
                            Action = "move",
                            PathNodeIds = new List<string> { "n1", "n2" }
                        }
                    },
                    buildOrders: new[]
                    {
                        new QueuedBuildOrderDto { NodeId = "n1", BuildingTypeId = "farm" }
                    },
                    plannedResearchTargetTechnologyId: "irrigation"),
                staticCatalog: new StaticCatalogState(
                    buildings: new Dictionary<string, CatalogBuildingDto>
                    {
                        ["farm"] = new CatalogBuildingDto { Id = "farm", Name = "Farm", PlacementKind = "resource_node" }
                    },
                    units: new Dictionary<string, CatalogUnitDto>
                    {
                        ["scout"] = new CatalogUnitDto { Id = "scout", Name = "Scout" }
                    }),
                turn: new TurnState(
                    turn: 6,
                    phase: "planning",
                    tokensLeft: 3,
                    planningStartEvents: new[]
                    {
                        new TurnEventDto { Type = "unit_moved", UnitId = "u1", NodeId = "n2" }
                    })));

            Assert.That(observedGameStates.Last().GameId, Is.EqualTo("game-1"));
            Assert.That(observedGameStates.Last().Nodes["n1"].Owner, Is.EqualTo("player-1"));
            Assert.That(observedGameStates.Last().Units["u1"].Type, Is.EqualTo("scout"));
            Assert.That(observedGameStates.Last().MyResources.Food, Is.EqualTo(9));

            Assert.That(observedPlanningDrafts.Last().SnapshotTurn, Is.EqualTo(6));
            Assert.That(observedPlanningDrafts.Last().UnitOrders[0].PathNodeIds, Is.EqualTo(new[] { "n1", "n2" }));
            Assert.That(observedPlanningDrafts.Last().BuildOrders[0].BuildingTypeId, Is.EqualTo("farm"));
            Assert.That(observedPlanningDrafts.Last().PlannedResearchTargetTechnologyId, Is.EqualTo("irrigation"));

            Assert.That(observedCatalogs.Last().Buildings["farm"].Name, Is.EqualTo("Farm"));
            Assert.That(observedCatalogs.Last().Units["scout"].Name, Is.EqualTo("Scout"));

            Assert.That(observedTurns.Last().Turn, Is.EqualTo(6));
            Assert.That(observedTurns.Last().Phase, Is.EqualTo("planning"));
            Assert.That(observedTurns.Last().PlanningStartEvents[0].Type, Is.EqualTo("unit_moved"));
        }

        [Test]
        public void HydrateSpecificStore_ShouldLeaveOtherStoresUntouched()
        {
            var gameStateStore = new GameStateStore();
            var planningDraftStore = new PlanningDraftStore();
            var staticCatalogStore = new StaticCatalogStore();
            var turnStore = new TurnStore();
            var helper = new StoreHydrationHelper(
                gameStateStore,
                planningDraftStore,
                staticCatalogStore,
                turnStore);

            helper.HydrateTurn(new TurnState(turn: 2, phase: "resolving", tokensLeft: 0));

            Assert.That(turnStore.Snapshot.Turn, Is.EqualTo(2));
            Assert.That(turnStore.Snapshot.Phase, Is.EqualTo("resolving"));
            Assert.That(gameStateStore.Snapshot.Turn, Is.Zero);
            Assert.That(planningDraftStore.Snapshot.SnapshotTurn, Is.Zero);
            Assert.That(staticCatalogStore.Snapshot.Buildings, Is.Empty);
        }
    }
}
