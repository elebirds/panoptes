using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class StoreReadModelTests
    {
        [Test]
        public void GameStateStore_ShouldCloneInputAndSnapshotData()
        {
            var nodes = new Dictionary<string, NodeDto>
            {
                ["n1"] = new NodeDto { Id = "n1", Owner = "p1", Q = 1, R = 2 }
            };
            var units = new Dictionary<string, UnitDto>
            {
                ["u1"] = new UnitDto { Id = "u1", Owner = "p1", Hp = 10, MaxHp = 12 }
            };
            var store = new GameStateStore();

            store.Replace(new GameStateStoreState(
                gameId: "g1",
                myPlayerId: "p1",
                turn: 3,
                phase: "planning",
                nodes: nodes,
                units: units,
                myResources: new ResourceDto
                {
                    Food = 5,
                    ResourceAmounts = new Dictionary<string, int> { ["grain"] = 12 },
                    PointAmounts = new Dictionary<string, int> { ["logistics_capacity"] = 4 }
                }));
            nodes["n1"].Owner = "mutated-input";
            units["u1"].Hp = 1;

            var snapshot = store.Snapshot;
            Assert.That(snapshot.GameId, Is.EqualTo("g1"));
            Assert.That(snapshot.Turn, Is.EqualTo(3));
            Assert.That(snapshot.Nodes["n1"].Owner, Is.EqualTo("p1"));
            Assert.That(snapshot.Units["u1"].Hp, Is.EqualTo(10));
            Assert.That(snapshot.MyResources.ResourceAmounts["grain"], Is.EqualTo(12));
            Assert.That(snapshot.MyResources.PointAmounts["logistics_capacity"], Is.EqualTo(4));

            ((Dictionary<string, NodeDto>)snapshot.Nodes)["n1"].Owner = "mutated-snapshot";
            ((Dictionary<string, UnitDto>)snapshot.Units)["u1"].Hp = 2;
            snapshot.MyResources.ResourceAmounts["grain"] = 99;
            snapshot.MyResources.PointAmounts["logistics_capacity"] = 99;

            var nextSnapshot = store.Snapshot;
            Assert.That(nextSnapshot.Nodes["n1"].Owner, Is.EqualTo("p1"));
            Assert.That(nextSnapshot.Units["u1"].Hp, Is.EqualTo(10));
            Assert.That(nextSnapshot.MyResources.ResourceAmounts["grain"], Is.EqualTo(12));
            Assert.That(nextSnapshot.MyResources.PointAmounts["logistics_capacity"], Is.EqualTo(4));
        }

        [Test]
        public void GameStateStore_ShouldReserveAndRefundBuildCostsLocally()
        {
            var store = new GameStateStore();
            store.Replace(new GameStateStoreState(
                turn: 1,
                phase: "planning",
                myResources: new ResourceDto
                {
                    Wood = 5,
                    IndustryOutput = 2,
                    ResourceAmounts = new Dictionary<string, int> { [ResourceKeys.ResourceWood] = 5 },
                    PointAmounts = new Dictionary<string, int> { ["industry_output"] = 2 }
                }));

            var building = new CatalogBuildingDto
            {
                ResourceCosts = new List<CatalogAmountDto> { new() { Key = ResourceKeys.ResourceWood, Amount = 3 } },
                PointCosts = new List<CatalogAmountDto> { new() { Key = "industry_output", Amount = 1 } }
            };

            Assert.That(store.TryReserveBuildCost("N1", building, out var errorCode), Is.True);
            Assert.That(errorCode, Is.Empty);
            Assert.That(store.Snapshot.MyResources.Wood, Is.EqualTo(2));
            Assert.That(store.Snapshot.MyResources.ResourceAmounts[ResourceKeys.ResourceWood], Is.EqualTo(2));
            Assert.That(store.Snapshot.MyResources.IndustryOutput, Is.EqualTo(1));
            Assert.That(store.Snapshot.MyResources.PointAmounts["industry_output"], Is.EqualTo(1));

            Assert.That(store.TryReserveBuildCost("N2", building, out errorCode), Is.False);
            Assert.That(errorCode, Is.EqualTo("insufficient_resources"));

            Assert.That(store.RefundReservedBuildCost("N1"), Is.True);
            Assert.That(store.Snapshot.MyResources.Wood, Is.EqualTo(5));
            Assert.That(store.Snapshot.MyResources.IndustryOutput, Is.EqualTo(2));
        }

        [Test]
        public void PlanningDraftStore_ShouldPublishClonedOrderSnapshots()
        {
            var orders = new List<QueuedUnitOrderDto>
            {
                new QueuedUnitOrderDto
                {
                    UnitId = "u1",
                    Action = "move",
                    PathNodeIds = new List<string> { "n1", "n2" },
                    Params = new Dictionary<string, string> { ["speed"] = "road" }
                }
            };
            var store = new PlanningDraftStore();

            store.ReplaceOrders(orders);
            orders[0].PathNodeIds.Add("n3");
            orders[0].Params["speed"] = "mutated";

            var snapshot = store.Snapshot;
            Assert.That(snapshot.UnitOrders, Has.Count.EqualTo(1));
            Assert.That(snapshot.UnitOrders[0].PathNodeIds, Is.EqualTo(new[] { "n1", "n2" }));
            Assert.That(snapshot.UnitOrders[0].Params["speed"], Is.EqualTo("road"));

            snapshot.UnitOrders[0].PathNodeIds.Add("mutated-snapshot");
            snapshot.UnitOrders[0].Params["speed"] = "mutated-snapshot";

            var nextSnapshot = store.Snapshot;
            Assert.That(nextSnapshot.UnitOrders[0].PathNodeIds, Is.EqualTo(new[] { "n1", "n2" }));
            Assert.That(nextSnapshot.UnitOrders[0].Params["speed"], Is.EqualTo("road"));
        }

        [Test]
        public void PlanningDraftStore_ShouldApplyRecipeSelectionOptimistically()
        {
            var store = new PlanningDraftStore();
            store.Replace(new PlanningDraftState(
                snapshotTurn: 3,
                snapshotPhase: GamePhases.Planning,
                recipeSelections: new[]
                {
                    new QueuedRecipeSelectionDto { NodeId = "old-node", RecipeId = "ore" },
                    new QueuedRecipeSelectionDto { NodeId = "node-a", RecipeId = "grain" }
                }));

            store.ApplyRecipeSelection(" NODE-A ", "  tools ");

            Assert.That(store.Snapshot.SnapshotTurn, Is.EqualTo(3));
            Assert.That(store.Snapshot.SnapshotPhase, Is.EqualTo(GamePhases.Planning));
            Assert.That(store.Snapshot.RecipeSelections, Has.Count.EqualTo(2));
            Assert.That(store.Snapshot.RecipeSelections[0].NodeId, Is.EqualTo("old-node"));
            Assert.That(store.Snapshot.RecipeSelections[0].RecipeId, Is.EqualTo("ore"));
            Assert.That(store.Snapshot.RecipeSelections[1].NodeId, Is.EqualTo("node-a"));
            Assert.That(store.Snapshot.RecipeSelections[1].RecipeId, Is.EqualTo("tools"));

            store.ApplyRecipeSelection("node-a", string.Empty);

            Assert.That(store.Snapshot.RecipeSelections, Has.Count.EqualTo(2));
            Assert.That(store.Snapshot.RecipeSelections[1].NodeId, Is.EqualTo("node-a"));
            Assert.That(store.Snapshot.RecipeSelections[1].RecipeId, Is.Empty);
        }

        [Test]
        public void StaticCatalogStore_ShouldCloneNestedCatalogArrays()
        {
            var building = new CatalogBuildingDto
            {
                Id = "farm",
                Name = "Farm",
                RecipeIds = new List<string> { "grain" },
                Tags = new List<string> { "food" }
            };
            var store = new StaticCatalogStore();

            store.Replace(new StaticCatalogState(
                resources: new Dictionary<string, CatalogHudEntryDto>
                {
                    ["ore"] = new CatalogHudEntryDto
                    {
                        Key = "ore",
                        IconKey = "ore_icon",
                        SortOrder = 10,
                        VisibleInHud = true
                    }
                },
                points: new Dictionary<string, CatalogHudEntryDto>
                {
                    ["industry_output"] = new CatalogHudEntryDto
                    {
                        Key = "industry_output",
                        IconKey = "industry_icon",
                        SortOrder = 20,
                        VisibleInHud = true
                    }
                },
                buildings: new Dictionary<string, CatalogBuildingDto> { ["farm"] = building }));
            building.RecipeIds[0] = "mutated-input";

            var snapshot = store.Snapshot;
            Assert.That(snapshot.Buildings["farm"].RecipeIds, Is.EqualTo(new[] { "grain" }));
            Assert.That(snapshot.Resources["ore"].IconKey, Is.EqualTo("ore_icon"));
            Assert.That(snapshot.Points["industry_output"].VisibleInHud, Is.True);

            snapshot.Buildings["farm"].RecipeIds[0] = "mutated-snapshot";
            ((Dictionary<string, CatalogHudEntryDto>)snapshot.Resources)["ore"].IconKey = "mutated-resource";
            ((Dictionary<string, CatalogHudEntryDto>)snapshot.Points)["industry_output"].VisibleInHud = false;

            var nextSnapshot = store.Snapshot;
            Assert.That(nextSnapshot.Buildings["farm"].RecipeIds, Is.EqualTo(new[] { "grain" }));
            Assert.That(nextSnapshot.Resources["ore"].IconKey, Is.EqualTo("ore_icon"));
            Assert.That(nextSnapshot.Points["industry_output"].VisibleInHud, Is.True);
        }

        [Test]
        public void SelectionStore_ShouldExposeOnlyLatestSelection()
        {
            var store = new SelectionStore();
            var observed = new List<SelectionState>();
            using var subscription = store.State.Subscribe(observed, static (state, target) => target.Add(state));

            store.SelectUnit("u1");
            Assert.That(store.Snapshot.SelectedUnitId, Is.EqualTo("u1"));
            Assert.That(store.Snapshot.SelectedNodeId, Is.Empty);
            Assert.That(observed.Last().SelectedUnitId, Is.EqualTo("u1"));

            store.SelectNode("n1");
            Assert.That(store.Snapshot.SelectedNodeId, Is.EqualTo("n1"));
            Assert.That(store.Snapshot.SelectedUnitId, Is.Empty);
            Assert.That(observed.Last().SelectedNodeId, Is.EqualTo("n1"));

            store.Clear();
            Assert.That(store.Snapshot.SelectedNodeId, Is.Empty);
            Assert.That(store.Snapshot.SelectedUnitId, Is.Empty);
            Assert.That(observed.Last().SelectedNodeId, Is.Empty);
        }

        [Test]
        public void TurnStore_ShouldClonePlanningStartEvents()
        {
            var events = new List<TurnEventDto>
            {
                new TurnEventDto
                {
                    Type = "move",
                    Data = new Dictionary<string, string> { ["unitId"] = "u1" }
                }
            };
            var store = new TurnStore();

            store.Replace(new TurnState(turn: 4, phase: "planning", tokensLeft: 2, planningStartEvents: events));
            events[0].Data["unitId"] = "mutated-input";

            var snapshot = store.Snapshot;
            Assert.That(snapshot.Turn, Is.EqualTo(4));
            Assert.That(snapshot.PlanningStartEvents[0].Data["unitId"], Is.EqualTo("u1"));

            snapshot.PlanningStartEvents[0].Data["unitId"] = "mutated-snapshot";

            var nextSnapshot = store.Snapshot;
            Assert.That(nextSnapshot.PlanningStartEvents[0].Data["unitId"], Is.EqualTo("u1"));
        }

        [Test]
        public void Stores_ShouldNotExposePublicWriteApis()
        {
            var storeTypes = new[]
            {
                typeof(GameStateStore),
                typeof(PlanningDraftStore),
                typeof(PlanningToolStore),
                typeof(SelectionStore),
                typeof(StaticCatalogStore),
                typeof(TurnStore)
            };

            foreach (var storeType in storeTypes)
            {
                var publicDeclaredMethods = storeType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(method => method.Name)
                    .ToList();

                Assert.That(publicDeclaredMethods, Is.Empty, $"{storeType.Name} must keep write APIs internal/application-facing.");
            }
        }
    }
}
