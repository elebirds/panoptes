using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class StoreHydrationCacheBridgeTests
    {
        private StoreHydrationCacheBridge _bridge;

        [TearDown]
        public void TearDown()
        {
            _bridge?.Dispose();
            _bridge = null;
            GameStateCache.Instance?.Clear();
            PlanningDraftCache.Instance?.ClearAll();
            StaticCatalogCache.Instance?.Clear();
        }

        [Test]
        public void Attach_ShouldHydrateStoresFromExistingCacheSnapshots()
        {
            var gameCache = EnsureGameStateCache();
            var draftCache = PlanningDraftCache.EnsureInstance();
            var catalogCache = StaticCatalogCache.EnsureInstance();
            var gameStore = new GameStateStore();
            var draftStore = new PlanningDraftStore();
            var catalogStore = new StaticCatalogStore();
            var turnStore = new TurnStore();
            var helper = new StoreHydrationHelper(gameStore, draftStore, catalogStore, turnStore);

            gameCache.ApplyGameInit(new MsgGameInit
            {
                GameId = "game-1",
                YourPlayerId = "player-1",
                Turn = 3,
                Phase = "planning",
                MapWidth = 4,
                MapHeight = 5,
                MyPlayer = new PlayerView
                {
                    Id = "player-1",
                    TokensLeft = 2,
                    Resources = new ResourceBag
                    {
                        Items =
                        {
                            new ResourceValue { Key = ResourceKeys.ResourceFood, Amount = 8 },
                            new ResourceValue { Key = ResourceKeys.ResourceWood, Amount = 4 }
                        }
                    }
                },
                Nodes =
                {
                    new NodeView
                    {
                        Id = "n1",
                        Pos = new Position { Q = 1, R = -1 },
                        ControllerPlayerId = "player-1",
                        Terrain = "plain"
                    }
                },
                Units =
                {
                    new UnitView
                    {
                        Id = "u1",
                        UnitType = "scout",
                        Faction = "player-1",
                        Pos = new Position { Q = 1, R = -1 },
                        Hp = 5
                    }
                }
            });
            draftCache.ApplyPlanningSnapshot(new MsgPlanningSnapshot
            {
                Turn = 3,
                Phase = "planning",
                PlannedResearchTargetTechnologyId = "irrigation",
                BuildOrders =
                {
                    new QueuedBuildOrder { NodeId = "n1", BuildingTypeId = "farm", CityId = "city-1" }
                }
            });
            catalogCache.ApplySnapshot(new StaticCatalogSnapshot
            {
                Buildings =
                {
                    new BuildingCatalogEntry
                    {
                        Id = "farm",
                        Name = "Farm",
                        Description = "Food",
                        PlacementKind = "resource_node"
                    }
                },
                Technologies =
                {
                    new TechnologyCatalogEntry
                    {
                        Id = "irrigation",
                        Name = "Irrigation",
                        Branch = "economy",
                        Tier = 1,
                        ResearchCost = 4
                    }
                }
            });

            _bridge = new StoreHydrationCacheBridge(helper);
            _bridge.Attach(gameCache, draftCache, catalogCache);

            Assert.That(gameStore.Snapshot.GameId, Is.EqualTo("game-1"));
            Assert.That(gameStore.Snapshot.Nodes["n1"].Owner, Is.EqualTo("player-1"));
            Assert.That(gameStore.Snapshot.Units["u1"].Type, Is.EqualTo("scout"));
            Assert.That(gameStore.Snapshot.MyResources.Food, Is.EqualTo(8));
            Assert.That(turnStore.Snapshot.Turn, Is.EqualTo(3));
            Assert.That(turnStore.Snapshot.TokensLeft, Is.EqualTo(2));
            Assert.That(draftStore.Snapshot.BuildOrders[0].BuildingTypeId, Is.EqualTo("farm"));
            Assert.That(draftStore.Snapshot.PlannedResearchTargetTechnologyId, Is.EqualTo("irrigation"));
            Assert.That(catalogStore.Snapshot.Buildings["farm"].Name, Is.EqualTo("Farm"));
            Assert.That(catalogStore.Snapshot.Technologies["irrigation"].ResearchCost, Is.EqualTo(4));
        }

        [Test]
        public void AttachedBridge_ShouldPublishSubsequentCacheChanges()
        {
            var gameCache = EnsureGameStateCache();
            var draftCache = PlanningDraftCache.EnsureInstance();
            var catalogCache = StaticCatalogCache.EnsureInstance();
            var gameStore = new GameStateStore();
            var draftStore = new PlanningDraftStore();
            var catalogStore = new StaticCatalogStore();
            var turnStore = new TurnStore();
            var helper = new StoreHydrationHelper(gameStore, draftStore, catalogStore, turnStore);

            _bridge = new StoreHydrationCacheBridge(helper);
            _bridge.Attach(gameCache, draftCache, catalogCache);

            gameCache.UpdateTokens(6);
            draftCache.ApplyPlanningSnapshot(new MsgPlanningSnapshot
            {
                Turn = 5,
                Phase = "planning",
                RecipeSelections =
                {
                    new QueuedRecipeSelection { NodeId = "n2", RecipeId = "grain" }
                }
            });
            catalogCache.ApplySnapshot(new StaticCatalogSnapshot
            {
                Recipes =
                {
                    new RecipeCatalogEntry
                    {
                        Id = "grain",
                        Name = "Mill Grain",
                        BuildingId = "mill",
                        WorkAmount = 2,
                        BaseProgress = 1
                    }
                }
            });

            Assert.That(gameStore.Snapshot.TokensLeft, Is.EqualTo(6));
            Assert.That(turnStore.Snapshot.TokensLeft, Is.EqualTo(6));
            Assert.That(draftStore.Snapshot.RecipeSelections[0].RecipeId, Is.EqualTo("grain"));
            Assert.That(catalogStore.Snapshot.Recipes["grain"].BuildingId, Is.EqualTo("mill"));
        }

        private static GameStateCache EnsureGameStateCache()
        {
            if (GameStateCache.Instance != null)
            {
                GameStateCache.Instance.Clear();
                return GameStateCache.Instance;
            }

            return new UnityEngine.GameObject("GameStateCache").AddComponent<GameStateCache>();
        }
    }
}
