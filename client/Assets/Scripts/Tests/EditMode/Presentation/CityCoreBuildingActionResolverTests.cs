using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.HUD;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class CityCoreBuildingActionResolverTests
    {
        private GameObject _unitObject;

        [TearDown]
        public void TearDown()
        {
            if (_unitObject != null)
            {
                Object.DestroyImmediate(_unitObject);
                _unitObject = null;
            }
        }

        [Test]
        public void IsOwnedRecipeBuildingProxy_ShouldUseCityOwnerFallbackWhenBuildingOwnerMissing()
        {
            var gameStateStore = new GameStateStore();
            var staticCatalogStore = new StaticCatalogStore();
            var resolver = new CityCoreBuildingActionResolver(gameStateStore, staticCatalogStore);
            var unit = CreateUnitView("workshop_node");

            gameStateStore.Replace(new GameStateStoreState(
                myPlayerId: "player_a",
                nodes: new Dictionary<string, NodeDto>
                {
                    ["capital"] = new NodeDto
                    {
                        Id = "capital",
                        Owner = "player_a",
                        BuildingType = "city_core",
                        CityId = "capital",
                        IsCityCore = true
                    },
                    ["workshop_node"] = new NodeDto
                    {
                        Id = "workshop_node",
                        BuildingType = "workshop",
                        CityId = "capital"
                    }
                }));
            staticCatalogStore.Replace(new StaticCatalogState(buildings: new Dictionary<string, CatalogBuildingDto>
            {
                ["workshop"] = new CatalogBuildingDto
                {
                    Id = "workshop",
                    RecipeIds = new List<string> { "tools" }
                }
            }));

            Assert.That(resolver.IsOwnedRecipeBuildingProxy(unit), Is.True);
            Assert.That(resolver.TryResolveRecipeBuilding(unit, out var context), Is.True);
            Assert.That(context.OwnerId, Is.EqualTo("player_a"));
        }

        [Test]
        public void IsOwnedRecipeBuildingProxy_ShouldRejectOtherCityOwnerFallback()
        {
            var gameStateStore = new GameStateStore();
            var staticCatalogStore = new StaticCatalogStore();
            var resolver = new CityCoreBuildingActionResolver(gameStateStore, staticCatalogStore);
            var unit = CreateUnitView("workshop_node");

            gameStateStore.Replace(new GameStateStoreState(
                myPlayerId: "player_a",
                nodes: new Dictionary<string, NodeDto>
                {
                    ["capital"] = new NodeDto
                    {
                        Id = "capital",
                        Owner = "player_b",
                        BuildingType = "city_core",
                        CityId = "capital",
                        IsCityCore = true
                    },
                    ["workshop_node"] = new NodeDto
                    {
                        Id = "workshop_node",
                        BuildingType = "workshop",
                        CityId = "capital"
                    }
                }));
            staticCatalogStore.Replace(new StaticCatalogState(buildings: new Dictionary<string, CatalogBuildingDto>
            {
                ["workshop"] = new CatalogBuildingDto
                {
                    Id = "workshop",
                    DefaultRecipeId = "tools"
                }
            }));

            Assert.That(resolver.IsOwnedRecipeBuildingProxy(unit), Is.False);
        }

        private UnitView CreateUnitView(string nodeId)
        {
            _unitObject = new GameObject("UnitViewTest");
            var unit = _unitObject.AddComponent<UnitView>();
            unit.Bind(new UnitDto { Id = nodeId }, Vector3.zero);
            return unit;
        }
    }
}
