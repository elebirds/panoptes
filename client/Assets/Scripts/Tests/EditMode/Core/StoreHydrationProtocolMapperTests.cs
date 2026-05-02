using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class StoreHydrationProtocolMapperTests
    {
        [Test]
        public void MergeGameSync_ShouldPreserveExistingIdentityAndFallbackValues()
        {
            var current = new GameStateStoreState(
                gameId: "game-1",
                activeGameSessionId: "session-1",
                myPlayerId: "player-1",
                turn: 4,
                phase: "planning",
                mapWidth: 8,
                mapHeight: 7,
                tokensLeft: 3,
                nodes: new Dictionary<string, NodeDto>
                {
                    ["n1"] = new NodeDto { Id = "n1", BuildingMaxHp = 12 }
                },
                units: new Dictionary<string, UnitDto>
                {
                    ["u1"] = new UnitDto { Id = "u1", MaxHp = 6 }
                },
                myResources: new ResourceDto { Food = 2 });

            var mapped = StoreHydrationProtocolMapper.MergeGameSync(current, new MsgGameSync
            {
                Turn = 5,
                Phase = "resolving",
                MyPlayer = new PlayerView
                {
                    Id = "player-1",
                    TokensLeft = 1,
                    Resources = new ResourceBag
                    {
                        Items = { new ResourceValue { Key = ResourceKeys.ResourceFood, Amount = 9 } }
                    }
                },
                Nodes =
                {
                    new NodeView
                    {
                        Id = "n1",
                        Pos = new Position { Q = 2, R = -2 },
                        BuildingTypeId = "farm",
                        BuildingHp = 5
                    }
                },
                Units =
                {
                    new UnitView
                    {
                        Id = "u1",
                        UnitType = "scout",
                        Faction = "player-1",
                        Pos = new Position { Q = 2, R = -2 },
                        Hp = 4
                    }
                }
            });

            Assert.That(mapped.GameId, Is.EqualTo("game-1"));
            Assert.That(mapped.ActiveGameSessionId, Is.EqualTo("session-1"));
            Assert.That(mapped.MapWidth, Is.EqualTo(8));
            Assert.That(mapped.Turn, Is.EqualTo(5));
            Assert.That(mapped.Phase, Is.EqualTo("resolving"));
            Assert.That(mapped.TokensLeft, Is.EqualTo(1));
            Assert.That(mapped.MyResources.Food, Is.EqualTo(9));
            Assert.That(mapped.Nodes["n1"].BuildingMaxHp, Is.EqualTo(12));
            Assert.That(mapped.Units["u1"].MaxHp, Is.EqualTo(6));
        }

        [Test]
        public void ToPlanningDraft_ShouldMapSnapshotWithoutProtocolTypes()
        {
            var mapped = StoreHydrationProtocolMapper.ToPlanningDraft(new MsgPlanningSnapshot
            {
                Turn = 6,
                Phase = "planning",
                PlannedResearchTargetTechnologyId = "irrigation",
                PlannedNationalPolicyId = "mobilize",
                UnitOrders =
                {
                    new QueuedUnitOrder
                    {
                        UnitId = "u1",
                        Action = "move",
                        TargetNodeId = "n2",
                        PathNodeIds = { "n1", "n2" },
                        TurnStops = { new MarchTurnStop { TurnIndex = 1, NodeId = "n2" } }
                    }
                },
                BuildOrders =
                {
                    new QueuedBuildOrder { NodeId = "n3", BuildingTypeId = "farm", CityId = "city-1" }
                },
                RecipeSelections =
                {
                    new QueuedRecipeSelection { NodeId = "n4", RecipeId = "grain" }
                },
                PlannedInstitutionPolicyIds = { "labor" }
            });

            Assert.That(mapped.SnapshotTurn, Is.EqualTo(6));
            Assert.That(mapped.UnitOrders[0].PathNodeIds, Is.EqualTo(new[] { "n1", "n2" }));
            Assert.That(mapped.UnitOrders[0].TurnStops[0].NodeId, Is.EqualTo("n2"));
            Assert.That(mapped.BuildOrders[0].BuildingTypeId, Is.EqualTo("farm"));
            Assert.That(mapped.RecipeSelections[0].RecipeId, Is.EqualTo("grain"));
            Assert.That(mapped.PlannedResearchTargetTechnologyId, Is.EqualTo("irrigation"));
            Assert.That(mapped.PlannedNationalPolicyId, Is.EqualTo("mobilize"));
            Assert.That(mapped.PlannedInstitutionPolicyIds, Is.EqualTo(new[] { "labor" }));
        }
    }
}
