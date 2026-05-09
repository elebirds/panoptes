using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.ViewModels;
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

        [Test]
        public void RegisteredRecipeSynthesisActions_ShouldSwitchBuildAndRecipeContextsThroughVisibilityStore()
        {
            var gameStateStore = new GameStateStore();
            var staticCatalogStore = new StaticCatalogStore();
            using var visibilityStore = new ManagementPanelVisibilityStore();
            using var buildContextStore = new BuildCatalogContextStore();
            using var recipeContextStore = new RecipeSynthesisContextStore();

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

            _unitObject = new GameObject("CityCoreActionRegistrarFlowTest");
            var registry = _unitObject.AddComponent<UnitInfoActionRegistry>();
            var registrar = _unitObject.AddComponent<CityCoreBuildingActionRegistrar>();
            var unit = _unitObject.AddComponent<UnitView>();
            InjectRegistrar(
                registrar,
                gameStateStore,
                staticCatalogStore,
                visibilityStore,
                buildContextStore,
                recipeContextStore);
            registrar.EnsureRegistered();

            unit.Bind(new UnitDto { Id = "capital" }, Vector3.zero);
            Assert.That(registry.TryResolve("action_3", unit, out var buildHandler, out _, out var buildVisible), Is.True);
            Assert.That(buildVisible, Is.True);
            buildHandler(unit);

            Assert.That(visibilityStore.IsVisible(ManagementPanelId.BuildCatalog), Is.True);
            Assert.That(buildContextStore.Current.CityCoreNodeId, Is.EqualTo("capital"));
            Assert.That(recipeContextStore.Current.HasContext, Is.False);

            unit.Bind(new UnitDto { Id = "workshop_node" }, Vector3.zero);
            Assert.That(registry.TryResolve("open_recipe_synthesis", unit, out var recipeHandler, out _, out var recipeVisible), Is.True);
            Assert.That(recipeVisible, Is.True);
            recipeHandler(unit);

            Assert.That(visibilityStore.IsVisible(ManagementPanelId.RecipeSynthesis), Is.True);
            Assert.That(buildContextStore.Current.CityCoreNodeId, Is.EqualTo(string.Empty));
            Assert.That(recipeContextStore.Current.NodeId, Is.EqualTo("workshop_node"));
            Assert.That(recipeContextStore.Current.BuildingTypeId, Is.EqualTo("workshop"));
            Assert.That(recipeContextStore.Current.OwnerId, Is.EqualTo("player_a"));

            unit.Bind(new UnitDto { Id = "capital" }, Vector3.zero);
            buildHandler(unit);

            Assert.That(visibilityStore.IsVisible(ManagementPanelId.BuildCatalog), Is.True);
            Assert.That(buildContextStore.Current.CityCoreNodeId, Is.EqualTo("capital"));
            Assert.That(recipeContextStore.Current.HasContext, Is.False);
        }

        private UnitView CreateUnitView(string nodeId)
        {
            _unitObject = new GameObject("UnitViewTest");
            var unit = _unitObject.AddComponent<UnitView>();
            unit.Bind(new UnitDto { Id = nodeId }, Vector3.zero);
            return unit;
        }

        private static void InjectRegistrar(
            CityCoreBuildingActionRegistrar registrar,
            GameStateStore gameStateStore,
            StaticCatalogStore staticCatalogStore,
            ManagementPanelVisibilityStore visibilityStore,
            BuildCatalogContextStore buildContextStore,
            RecipeSynthesisContextStore recipeContextStore)
        {
            var method = typeof(CityCoreBuildingActionRegistrar).GetMethod(
                "Construct",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(
                registrar,
                new object[]
                {
                    gameStateStore,
                    staticCatalogStore,
                    visibilityStore,
                    buildContextStore,
                    recipeContextStore,
                    null,
                    null,
                    null
                });
        }
    }
}
