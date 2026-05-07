using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.ViewModels;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class BuildCatalogViewModelTests
    {
        [Test]
        public void Current_ShouldGroupBuildingsByPlacementKind()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            using var viewModel = new BuildCatalogViewModel(catalogStore, draftStore);

            catalogStore.Replace(new StaticCatalogState(buildings: new Dictionary<string, CatalogBuildingDto>
            {
                ["farm"] = new CatalogBuildingDto { Id = "farm", Name = "Farm", PlacementKind = "resource_node", SortOrder = 2 },
                ["workshop"] = new CatalogBuildingDto { Id = "workshop", Name = "Workshop", PlacementKind = "city_territory", SortOrder = 1 },
                ["road"] = new CatalogBuildingDto { Id = "road", Name = "Road", PlacementKind = "any", SortOrder = 3 },
                ["city_core"] = new CatalogBuildingDto { Id = "city_core", Name = "City Core", PlacementKind = "city_foundation_center", SortOrder = 0 }
            }));

            var state = viewModel.Current;

            Assert.That(state.Groups, Has.Count.EqualTo(3));
            Assert.That(state.Groups[0].Id, Is.EqualTo("tile"));
            Assert.That(state.Groups[0].Items[0].BuildingId, Is.EqualTo("farm"));
            Assert.That(state.Groups[1].Id, Is.EqualTo("city"));
            Assert.That(state.Groups[1].Items[0].BuildingId, Is.EqualTo("workshop"));
            Assert.That(state.Groups[2].Id, Is.EqualTo("other"));
            Assert.That(state.Groups[2].Items[0].BuildingId, Is.EqualTo("road"));
        }

        [Test]
        public void Current_ShouldMarkPendingBuildOrders()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            using var viewModel = new BuildCatalogViewModel(catalogStore, draftStore);

            catalogStore.Replace(new StaticCatalogState(buildings: new Dictionary<string, CatalogBuildingDto>
            {
                ["farm"] = new CatalogBuildingDto { Id = "farm", Name = "Farm", PlacementKind = "resource_node" }
            }));
            draftStore.Replace(new PlanningDraftState(buildOrders: new[]
            {
                new QueuedBuildOrderDto { BuildingTypeId = " farm ", NodeId = "n1", CityId = "c1" }
            }));

            Assert.That(viewModel.Current.Groups[0].Items[0].IsPending, Is.True);
        }

        [Test]
        public void Current_ShouldProjectBuildingCosts()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            using var viewModel = new BuildCatalogViewModel(catalogStore, draftStore);

            catalogStore.Replace(new StaticCatalogState(
                resources: new Dictionary<string, CatalogHudEntryDto>
                {
                    ["wood"] = new CatalogHudEntryDto { Key = "wood", Name = "Wood", IconKey = "resource_wood" }
                },
                points: new Dictionary<string, CatalogHudEntryDto>
                {
                    ["industry_output"] = new CatalogHudEntryDto { Key = "industry_output", Name = "Industry", IconKey = "point_industry_output" }
                },
                buildings: new Dictionary<string, CatalogBuildingDto>
                {
                    ["workshop"] = new CatalogBuildingDto
                    {
                        Id = "workshop",
                        Name = "Workshop",
                        PlacementKind = "city_territory",
                        ResourceCosts = new List<CatalogAmountDto> { new() { Key = "wood", Amount = 2 } },
                        PointCosts = new List<CatalogAmountDto> { new() { Key = "industry_output", Amount = 1 } }
                    }
                }));

            var costs = viewModel.Current.Groups[0].Items[0].Costs;

            Assert.That(costs, Has.Count.EqualTo(2));
            Assert.That(costs[0].Id, Is.EqualTo("wood"));
            Assert.That(costs[0].IconKey, Is.EqualTo("resource_wood"));
            Assert.That(costs[1].Id, Is.EqualTo("industry_output"));
            Assert.That(costs[1].Amount, Is.EqualTo(1));
        }
    }
}
