using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Stores;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class StaticCatalogStoreHydratorTests
    {
        [TearDown]
        public void TearDown()
        {
            StaticCatalogCache.Instance?.Clear();
        }

        [Test]
        public void HydrateFromCache_ShouldPublishCacheCatalogSnapshot()
        {
            var cache = EnsureCatalogCache();
            cache.ApplySnapshot(new StaticCatalogSnapshot
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
                },
                Units =
                {
                    new UnitCatalogEntry
                    {
                        Id = "scout",
                        Name = "Scout",
                        CanAttackStructures = true
                    }
                },
                Resources =
                {
                    new ResourceDescriptor
                    {
                        Key = "ore",
                        IconKey = "ore_icon",
                        SortOrder = 20,
                        VisibleInHud = true
                    }
                },
                Points =
                {
                    new PointDescriptor
                    {
                        Key = "industry_output",
                        IconKey = "industry_icon",
                        SortOrder = 40,
                        VisibleInHud = true
                    }
                }
            });
            var store = new StaticCatalogStore();
            var hydrator = new StaticCatalogStoreHydrator(store);

            hydrator.HydrateFromCache(cache);

            Assert.That(store.Snapshot.Buildings["farm"].Name, Is.EqualTo("Farm"));
            Assert.That(store.Snapshot.Buildings["farm"].PlacementKind, Is.EqualTo("resource_node"));
            Assert.That(store.Snapshot.Resources["ore"].IconKey, Is.EqualTo("ore_icon"));
            Assert.That(store.Snapshot.Resources["ore"].VisibleInHud, Is.True);
            Assert.That(store.Snapshot.Points["industry_output"].SortOrder, Is.EqualTo(40));
            Assert.That(store.Snapshot.Technologies["irrigation"].ResearchCost, Is.EqualTo(4));
            Assert.That(store.Snapshot.Units["scout"].Flags.CanAttackStructures, Is.True);
        }

        [Test]
        public void HydrateFromSnapshot_ShouldPublishProtocolCatalogSnapshot()
        {
            var store = new StaticCatalogStore();
            var hydrator = new StaticCatalogStoreHydrator(store);

            hydrator.HydrateFromSnapshot(new StaticCatalogSnapshot
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
                },
                Resources =
                {
                    new ResourceDescriptor
                    {
                        Key = "wood",
                        IconKey = "wood_icon",
                        SortOrder = 10,
                        VisibleInHud = true
                    }
                }
            });

            Assert.That(store.Snapshot.Recipes["grain"].BuildingId, Is.EqualTo("mill"));
            Assert.That(store.Snapshot.Recipes["grain"].WorkAmount, Is.EqualTo(2));
            Assert.That(store.Snapshot.Resources["wood"].IconKey, Is.EqualTo("wood_icon"));
        }

        [Test]
        public void HydrateFromCache_ShouldClearStoreWhenCacheMissing()
        {
            var store = new StaticCatalogStore();
            var hydrator = new StaticCatalogStoreHydrator(store);

            hydrator.HydrateFromSnapshot(new StaticCatalogSnapshot
            {
                Buildings =
                {
                    new BuildingCatalogEntry { Id = "farm", Name = "Farm" }
                }
            });
            hydrator.HydrateFromCache(null);

            Assert.That(store.Snapshot.Buildings, Is.Empty);
            Assert.That(store.Snapshot.Resources, Is.Empty);
            Assert.That(store.Snapshot.Points, Is.Empty);
        }

        [Test]
        public void HydrateFromSnapshot_ShouldClearStoreWhenSnapshotMissing()
        {
            var store = new StaticCatalogStore();
            var hydrator = new StaticCatalogStoreHydrator(store);

            hydrator.HydrateFromSnapshot(new StaticCatalogSnapshot
            {
                Buildings =
                {
                    new BuildingCatalogEntry { Id = "farm", Name = "Farm" }
                }
            });
            hydrator.HydrateFromSnapshot(null);

            Assert.That(store.Snapshot.Buildings, Is.Empty);
            Assert.That(store.Snapshot.Resources, Is.Empty);
            Assert.That(store.Snapshot.Points, Is.Empty);
        }

        private static StaticCatalogCache EnsureCatalogCache()
        {
            if (StaticCatalogCache.Instance != null)
            {
                StaticCatalogCache.Instance.Clear();
                return StaticCatalogCache.Instance;
            }

            return new GameObject("StaticCatalogCache").AddComponent<StaticCatalogCache>();
        }
    }
}
