using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Cache
{
    public sealed class GameStateCacheReadQueryTests
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
        public void GameStateCache_ReadQueries_ShouldReturnSnapshotsWithoutMutatingAuthoritativeState()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<GameStateCache>();

            cache.ApplyGameInit(new MsgGameInit
            {
                GameId = "game-1",
                YourPlayerId = "player-1",
                Turn = 1,
                Phase = "planning",
                MyPlayer = new PlayerView
                {
                    Id = "player-1",
                    TokensLeft = 3,
                    Resources = new ResourceBag
                    {
                        Items =
                        {
                            new ResourceValue { Key = ResourceKeys.ResourceOre, Amount = 7 },
                            new ResourceValue { Key = ResourceKeys.ResourceWood, Amount = 5 },
                            new ResourceValue { Key = " custom_resource ", Amount = 2 }
                        }
                    },
                    Points = new PointBag
                    {
                        Items =
                        {
                            new PointValue { Key = "industry_output", Amount = 4 },
                            new PointValue { Key = " logistics_capacity ", Amount = 9 }
                        }
                    },
                    Research = new ResearchStateView
                    {
                        CompletedTechnologyIds = { " organized_labor ", "", "   " },
                        ActiveTechnologyIds = { "academy_charter" },
                        PendingActivationTechnologyIds = { " logistics " }
                    }
                }
            });

            var resources = cache.GetMyResources();
            Assert.That(resources.Ore, Is.EqualTo(7));
            Assert.That(resources.Wood, Is.EqualTo(5));
            Assert.That(resources.IndustryOutput, Is.EqualTo(4));

            var resourceAmounts = cache.GetMyResourceAmounts();
            Assert.That(resourceAmounts[ResourceKeys.ResourceOre], Is.EqualTo(7));
            Assert.That(resourceAmounts["custom_resource"], Is.EqualTo(2));
            resourceAmounts[ResourceKeys.ResourceOre] = 99;
            Assert.That(cache.GetMyResourceAmounts()[ResourceKeys.ResourceOre], Is.EqualTo(7));

            var pointAmounts = cache.GetMyPointAmounts();
            Assert.That(pointAmounts["industry_output"], Is.EqualTo(4));
            Assert.That(pointAmounts["logistics_capacity"], Is.EqualTo(9));

            Assert.That(cache.GetCompletedTechnologyIds(), Is.EquivalentTo(new[] { "organized_labor" }));
            Assert.That(cache.GetActiveTechnologyIds(), Is.EquivalentTo(new[] { "academy_charter" }));
            Assert.That(cache.GetPendingActivationTechnologyIds(), Is.EquivalentTo(new[] { "logistics" }));
        }
    }
}
