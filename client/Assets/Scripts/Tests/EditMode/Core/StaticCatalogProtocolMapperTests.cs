using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Protocol.V1;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class StaticCatalogProtocolMapperTests
    {
        [Test]
        public void ToState_ShouldMapRepresentativeCatalogEntries()
        {
            var state = StaticCatalogProtocolMapper.ToState(new StaticCatalogSnapshot
            {
                Buildings =
                {
                    new BuildingCatalogEntry
                    {
                        Id = "farm",
                        Name = "Farm",
                        Description = "Produces food",
                        PlacementKind = "resource_node",
                        RequiredResourceType = "food",
                        Tags = { "economy" }
                    }
                },
                Recipes =
                {
                    new RecipeCatalogEntry
                    {
                        Id = "grain",
                        Name = "Mill Grain",
                        BuildingId = "mill",
                        WorkAmount = 3,
                        BaseProgress = 1,
                        Tags = { "food" }
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
                        ResearchCost = 5,
                        Tags = { "growth" }
                    }
                },
                Policies =
                {
                    new PolicyCatalogEntry
                    {
                        Id = "mobilize",
                        Name = "Mobilize",
                        Layer = "national",
                        ActivationTiming = "next_turn"
                    }
                },
                Units =
                {
                    new UnitCatalogEntry
                    {
                        Id = "scout",
                        Name = "Scout",
                        PrefabKey = "unit_scout",
                        CanAttackStructures = true,
                        Tags = { "fast" }
                    }
                },
                Resources =
                {
                    new ResourceDescriptor
                    {
                        Key = "food",
                        DisplayName = "Food",
                        Description = "Basic food stock",
                        IconKey = "food_icon",
                        SortOrder = 10,
                        VisibleInHud = true
                    }
                },
                Points =
                {
                    new PointDescriptor
                    {
                        Key = "industry_output",
                        DisplayName = "Industry",
                        Description = "Build progress",
                        IconKey = "industry_icon",
                        SortOrder = 30,
                        VisibleInHud = false
                    }
                }
            });

            Assert.That(state.Resources["food"].IconKey, Is.EqualTo("food_icon"));
            Assert.That(state.Resources["food"].Name, Is.EqualTo("Food"));
            Assert.That(state.Resources["food"].Description, Is.EqualTo("Basic food stock"));
            Assert.That(state.Resources["food"].VisibleInHud, Is.True);
            Assert.That(state.Points["industry_output"].Name, Is.EqualTo("Industry"));
            Assert.That(state.Points["industry_output"].SortOrder, Is.EqualTo(30));
            Assert.That(state.Points["industry_output"].VisibleInHud, Is.False);
            Assert.That(state.Buildings["farm"].RequiredResourceType, Is.EqualTo("food"));
            Assert.That(state.Buildings["farm"].Tags, Is.EqualTo(new[] { "economy" }));
            Assert.That(state.Recipes["grain"].WorkAmount, Is.EqualTo(3));
            Assert.That(state.Technologies["irrigation"].ResearchCost, Is.EqualTo(5));
            Assert.That(state.Policies["mobilize"].ActivationTiming, Is.EqualTo("next_turn"));
            Assert.That(state.Units["scout"].Flags.CanAttackStructures, Is.True);
            Assert.That(state.Units["scout"].Tags, Is.EqualTo(new[] { "fast" }));
        }
    }
}
