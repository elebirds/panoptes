using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Mapper;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Cache
{
    public sealed class GameStateFogTests
    {
        [TearDown]
        public void TearDown()
        {
            if (GameStateCache.Instance != null)
            {
                Object.DestroyImmediate(GameStateCache.Instance.gameObject);
            }
        }

        [Test]
        public void NodeMapper_ShouldMapObservationKnowledgeState()
        {
            var dto = NodeMapper.ToDto(new NodeView
            {
                Id = "N1",
                Pos = new Position { X = 1, Y = 2 },
                Terrain = "plain",
                IsCurrentlyVisible = false,
                IsMemory = true,
                LastObservedTurn = 7
            });

            Assert.That(dto, Is.Not.Null);
            Assert.That(dto.IsVisible, Is.False);
            Assert.That(dto.IsMemory, Is.True);
            Assert.That(dto.LastObservedTurn, Is.EqualTo(7));
        }

        [Test]
        public void GameStateCache_ShouldPreserveMemoryNodesAndRemoveHiddenUnits()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<GameStateCache>();

            cache.ApplyGameInit(new MsgGameInit
            {
                GameId = "game-1",
                YourPlayerId = "player-1",
                Turn = 1,
                Phase = "planning",
                MyPlayer = new PlayerView { Id = "player-1", TokensLeft = 3 },
                Nodes =
                {
                    new NodeView
                    {
                        Id = "N1",
                        Pos = new Position { X = 0, Y = 0 },
                        Terrain = "plain",
                        IsCurrentlyVisible = true,
                        LastObservedTurn = 1
                    },
                    new NodeView
                    {
                        Id = "N2",
                        Pos = new Position { X = 1, Y = 0 },
                        Terrain = "plain",
                        BuildingTypeId = "farm",
                        IsCurrentlyVisible = true,
                        LastObservedTurn = 1
                    }
                },
                Units =
                {
                    new UnitView
                    {
                        Id = "enemy-1",
                        Faction = "player-2",
                        UnitType = "infantry",
                        Pos = new Position { X = 1, Y = 0 },
                        Hp = 30,
                        MaxHp = 30
                    }
                }
            });

            cache.ApplyPlanningStart(new MsgPlanningStart
            {
                Turn = 2,
                Phase = "planning",
                Tokens = 3,
                MyPlayer = new PlayerView { Id = "player-1", TokensLeft = 3 },
                Nodes =
                {
                    new NodeView
                    {
                        Id = "N1",
                        Pos = new Position { X = 0, Y = 0 },
                        Terrain = "plain",
                        IsCurrentlyVisible = true,
                        LastObservedTurn = 2
                    },
                    new NodeView
                    {
                        Id = "N2",
                        Pos = new Position { X = 1, Y = 0 },
                        Terrain = "plain",
                        BuildingTypeId = "farm",
                        IsCurrentlyVisible = false,
                        IsMemory = true,
                        LastObservedTurn = 1
                    }
                },
                Snapshot = new MsgPlanningSnapshot { Turn = 2, Phase = "planning" }
            });

            Assert.That(cache.Units.ContainsKey("enemy-1"), Is.False);
            Assert.That(cache.Nodes.ContainsKey("N2"), Is.True);
            Assert.That(cache.Nodes["N2"].IsVisible, Is.False);
            Assert.That(cache.Nodes["N2"].IsMemory, Is.True);
            Assert.That(cache.Nodes["N2"].LastObservedTurn, Is.EqualTo(1));
        }
    }
}
