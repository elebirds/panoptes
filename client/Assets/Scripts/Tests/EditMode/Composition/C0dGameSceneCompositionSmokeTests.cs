using System;
using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Presentation.Animation;
using Panoptes.Presentation.Binders.UiToolkit;
using Panoptes.Presentation.Composition;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.Game;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.UI.Turn;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Panoptes.Tests.EditMode.Composition
{
    public sealed class C0dGameSceneCompositionSmokeTests
    {
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private SceneSetup[] _previousSceneSetup;

        private static IEnumerable<ResourcePrefabCase> RegisteredResourcePrefabCases
        {
            get
            {
                yield return new ResourcePrefabCase("Prefabs/Runtime/AnimationQueue", typeof(AnimationQueue));
                yield return new ResourcePrefabCase("Prefabs/UI/DamageNumberPopupController", typeof(DamageNumberPopupController));
                yield return new ResourcePrefabCase("Prefabs/UI/ManagementHost", typeof(ManagementHostUiToolkitBinder));
                yield return new ResourcePrefabCase("Prefabs/UI/TokenHUD", typeof(TokenHUD));
                yield return new ResourcePrefabCase("Prefabs/UI/CityCoreHpBarOverlay", typeof(CityCoreHpBarOverlayController));
                yield return new ResourcePrefabCase("Prefabs/UI/BuildingConstructionOverlay", typeof(BuildingConstructionOverlayController));
                yield return new ResourcePrefabCase("Prefabs/UI/GameChatPanel", typeof(GameChatPanelController));
                yield return new ResourcePrefabCase("Prefabs/UI/SettlementTimeline", typeof(SettlementTimeline));
                yield return new ResourcePrefabCase("Prefabs/UI/TurnReportPanel", typeof(TurnReportPanel));
                yield return new ResourcePrefabCase("Prefabs/UI/GameOverOverlay", typeof(GameOverOverlay));
                yield return new ResourcePrefabCase("Prefabs/UI/BuildCatalog", typeof(BuildCatalogUiToolkitBinder));
                yield return new ResourcePrefabCase("Prefabs/UI/TechTree", typeof(TechTreeUiToolkitBinder));
                yield return new ResourcePrefabCase("Prefabs/UI/RecipeSynthesis", typeof(RecipeSynthesisUiToolkitBinder));
                yield return new ResourcePrefabCase("Prefabs/UI/PolicyFocus", typeof(PolicyFocusUiToolkitBinder));
                yield return new ResourcePrefabCase("Prefabs/UI/MinisterReport", typeof(MinisterReportUiToolkitBinder));
                yield return new ResourcePrefabCase("Prefabs/UI/NationalLedger", typeof(NationalLedgerUiToolkitBinder));
            }
        }

        [SetUp]
        public void SetUp()
        {
            _previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        }

        [TearDown]
        public void TearDown()
        {
            if (_previousSceneSetup != null && _previousSceneSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(_previousSceneSetup);
            }

            _previousSceneSetup = null;
        }

        [Test]
        public void GameScene_ShouldOpenWithSingleFinalCompositionRoot()
        {
            var scene = OpenGameScene();
            var scopes = FindSceneComponents<GameLifetimeScope>(scene);

            Assert.That(scopes, Has.Length.EqualTo(1), "Game scene must own exactly one scene lifetime scope.");
            Assert.That(scopes[0].gameObject.name, Is.EqualTo("Game Composition"));
            Assert.That(scopes[0].enabled, Is.True);
            Assert.That(scopes[0], Is.TypeOf<GameLifetimeScope>());
            Assert.That(FindSceneComponents<ProjectLifetimeScope>(scene), Is.Empty,
                "ProjectLifetimeScope is created by bootstrap and must not be serialized into Game.unity.");
        }

        [Test]
        public void GameScene_ShouldContainCompositionRegisteredSceneComponents()
        {
            var scene = OpenGameScene();

            AssertSceneComponent<GameSceneController>(scene);
            AssertSceneComponent<MapRenderer>(scene);
            AssertSceneComponent<MapPlanningInputController>(scene);
            AssertSceneComponent<SettlementPlaybackController>(scene);
            AssertSceneComponent<CinemachineMapCameraController>(scene);
            AssertSceneComponent<ResourceHUD>(scene);
            AssertSceneComponent<TurnHUD>(scene);
            AssertSceneComponent<UnitInfoPanelController>(scene);
            AssertSceneComponent<CityCoreBuildingActionRegistrar>(scene);
            AssertSceneComponent<SettlerUnitActionRegistrar>(scene);
        }

        [TestCaseSource(nameof(RegisteredResourcePrefabCases))]
        public void RegisteredResourcePrefabs_ShouldLoadExpectedComponent(ResourcePrefabCase testCase)
        {
            var prefab = Resources.Load<GameObject>(testCase.ResourcePath);
            Assert.That(prefab, Is.Not.Null, "Resources/" + testCase.ResourcePath + ".prefab must exist.");
            Assert.That(
                prefab.GetComponent(testCase.ComponentType),
                Is.Not.Null,
                "Resources/" + testCase.ResourcePath + ".prefab must carry " + testCase.ComponentType.Name + ".");
        }

        [Test]
        public void MinisterReportPrefab_ShouldCarryExplicitUiDocument()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/UI/MinisterReport");
            Assert.That(prefab, Is.Not.Null, "Resources/Prefabs/UI/MinisterReport.prefab must exist.");
            Assert.That(
                prefab.GetComponent<UIDocument>(),
                Is.Not.Null,
                "MinisterReport must serialize UIDocument instead of relying on runtime component injection.");
        }

        private static Scene OpenGameScene()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True, GameScenePath + " must be a valid scene.");
            Assert.That(scene.isLoaded, Is.True, GameScenePath + " must load in EditMode.");
            return scene;
        }

        private static void AssertSceneComponent<TComponent>(Scene scene)
            where TComponent : Component
        {
            var components = FindSceneComponents<TComponent>(scene);
            Assert.That(
                components,
                Has.Length.EqualTo(1),
                GameScenePath + " must contain exactly one " + typeof(TComponent).Name + " for GameLifetimeScope composition.");
            Assert.That(components[0].gameObject.scene, Is.EqualTo(scene));
        }

        private static TComponent[] FindSceneComponents<TComponent>(Scene scene)
            where TComponent : Component
        {
            var results = new List<TComponent>();
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                results.AddRange(roots[i].GetComponentsInChildren<TComponent>(true));
            }

            return results.ToArray();
        }

        public readonly struct ResourcePrefabCase
        {
            public ResourcePrefabCase(string resourcePath, Type componentType)
            {
                ResourcePath = resourcePath;
                ComponentType = componentType;
            }

            public string ResourcePath { get; }

            public Type ComponentType { get; }

            public override string ToString()
            {
                return ResourcePath + " -> " + ComponentType.Name;
            }
        }
    }
}
