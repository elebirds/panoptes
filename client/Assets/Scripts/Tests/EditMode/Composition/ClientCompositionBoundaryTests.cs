using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Panoptes.Tests.EditMode.Composition
{
    public sealed class ClientCompositionBoundaryTests
    {
        [Test]
        public void CompositionRuntime_ShouldNotUseLegacySingletonLookup()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/Composition"),
                ResolveAssetPath("Scripts/Runtime/Core/Infrastructure/Network/NetworkMessageSender.cs")
            };

            var offenders = FindTokenOffenders(roots, "*.cs", ".Instance");

            Assert.That(offenders, Is.Empty, "Final composition code must receive dependencies from VContainer, not legacy singleton lookup.");
        }

        [Test]
        public void CompositionScopes_ShouldRegisterFinalStoresWithoutLegacyCacheFacades()
        {
            var root = ResolveAssetPath("Scripts/Runtime/Presentation/Composition");
            var offenders = FindTokenOffenders(
                new[] { root },
                "*.cs",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache");

            Assert.That(offenders, Is.Empty, "GameLifetimeScope must wait for final Stores instead of registering legacy cache facades.");

            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            Assert.That(installer, Does.Contain("StaticCatalogStore"));
            Assert.That(installer, Does.Contain("GameStateStore"));
            Assert.That(installer, Does.Contain("PlanningDraftStore"));
            Assert.That(installer, Does.Contain("SelectionStore"));
            Assert.That(installer, Does.Contain("PlanningToolStore"));
            Assert.That(installer, Does.Contain("TurnStore"));
            Assert.That(installer, Does.Contain("SelectionService"));
            Assert.That(installer, Does.Contain("PlanningToolService"));
            Assert.That(installer, Does.Contain("GameIntentService"));
            Assert.That(installer, Does.Contain("PlanningIntentService"));
            Assert.That(installer, Does.Contain("MinisterCommandService"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<TurnHUD>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<GameChatPanelController>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<MinisterPanel>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<SettlementTimeline>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<TurnReportPanel>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<GameOverOverlay>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<UnitInfoPanelController>"));
            Assert.That(installer, Does.Contain("RegisterOptionalSceneComponent<RecipeSynthesisPanel>"));
            Assert.That(installer, Does.Contain("RegisterOptionalSceneComponent<TechTreePanelController>"));
            Assert.That(installer, Does.Contain("UnitInfoViewModel"));
            Assert.That(installer, Does.Contain("PlanningToolViewModel"));
            Assert.That(installer, Does.Contain("TurnSummaryViewModel"));
            Assert.That(installer, Does.Contain("TurnSummaryUiToolkitBinder"));
        }

        [Test]
        public void CompositionRuntime_ShouldNotReintroduceManualSceneInjectionShells()
        {
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/SceneCommandServiceInjector.cs")),
                Is.False,
                "Scene command injection must be owned by GameLifetimeScope registrations, not a manual scope lookup shell.");

            var root = ResolveAssetPath("Scripts/Runtime/Presentation/Composition");
            var offenders = FindTokenOffenders(
                new[] { root },
                "*.cs",
                "SceneCommandServiceInjector",
                "LifetimeScope.Find<GameLifetimeScope>()",
                "InjectGameObject");

            Assert.That(offenders, Is.Empty, "Presentation composition must not perform manual scene injection.");
        }

        [Test]
        public void PresentationCommandCallers_ShouldUseServicesInsteadOfStaticGameIntents()
        {
            var root = ResolveAssetPath("Scripts/Runtime/Presentation");
            var offenders = FindTokenOffenders(
                new[] { root },
                "*.cs",
                "GameIntents.");

            Assert.That(offenders, Is.Empty, "Migrated Presentation command callers must use injected command services.");
        }

        [Test]
        public void GameSceneController_ShouldOnlyEnsureHelpersNotInjectThemManually()
        {
            var controller = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs"));

            Assert.That(controller, Does.Not.Contain("IObjectResolver"));
            Assert.That(controller, Does.Not.Contain("InjectIfPossible"));
            Assert.That(controller, Does.Not.Contain("InjectDynamicPresentationHelpers"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<GameChatPanelController>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<MinisterPanel>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<TurnHUD>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<SettlementTimeline>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<TurnReportPanel>"));
            Assert.That(controller, Does.Not.Contain("EnsurePrefabComponent<GameOverOverlay>"));
            Assert.That(controller, Does.Contain("EnsureComponent<ResourceHUD>"));
        }

        [Test]
        public void CommandServices_ShouldNotDependOnUnityUiOrStaticNetworkSender()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Core/Application/Services/GameIntentService.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Application/Services/PlanningIntentService.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Application/Services/MinisterCommandService.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "UnityEngine.UI",
                "TMPro",
                "NetworkManager",
                "MessageSender.");

            Assert.That(offenders, Is.Empty, "Command services may build messages, but must not know about UI controls or static network senders.");
        }

        [Test]
        public void UnitInfoMigratedSlice_ShouldNotDependOnProtocolOrLegacyCaches()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoPanelController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/UnitInfoViewModel.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Binders/Ugui/UnitInfoUguiBinder.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoHpState.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoDirectOrderState.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "NetworkManager.Instance",
                ".Instance");

            Assert.That(offenders, Is.Empty, "Migrated UnitInfo ViewModel/Binder must consume final stores and services only.");
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoReactiveBridge.cs")),
                Is.False,
                "UnitInfo must not reintroduce a compatibility bridge between controller, ViewModel, and Binder.");
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoPlanningSummaryPresenter.cs")),
                Is.False,
                "UnitInfo planning summary must be projected by UnitInfoViewModel and rendered by UnitInfoUguiBinder.");
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoHpStateResolver.cs")),
                Is.False,
                "UnitInfo HP state must come from UnitInfoViewModel, not a cache fallback resolver.");
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoDirectOrderStateResolver.cs")),
                Is.False,
                "UnitInfo direct-order state must come from UnitInfoViewModel, not a cache fallback resolver.");
        }

        [Test]
        public void TurnSummaryUiToolkitSlice_ShouldUseStableNamesAndFinalStores()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/TurnSummaryViewModel.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Binders/UiToolkit/TurnSummaryUiToolkitBinder.cs")
            };
            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "NetworkManager.Instance",
                ".Instance");

            Assert.That(offenders, Is.Empty, "Migrated TurnSummary ViewModel/Binder must consume final stores only.");

            var uxml = File.ReadAllText(ResolveAssetPath("UI/Toolkit/Turn/TurnSummary.uxml"));
            Assert.That(uxml, Does.Contain("turn-summary-root"));
            Assert.That(uxml, Does.Contain("turn-summary-title"));
            Assert.That(uxml, Does.Contain("turn-summary-turn-value"));
            Assert.That(uxml, Does.Contain("turn-summary-phase-value"));
            Assert.That(uxml, Does.Contain("turn-summary-events"));
        }

        [Test]
        public void PlanningToolMigratedSlice_ShouldUseFinalStores()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Core/Application/Stores/PlanningToolStore.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Application/Services/PlanningToolService.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapPlanningInputStateAdapter.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/PlanningToolViewModel.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/PlanningToolViewState.cs")
            };
            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "NetworkManager.Instance",
                ".Instance",
                "UnityEngine.UI",
                "TMPro");

            Assert.That(offenders, Is.Empty, "PlanningTool state and ViewModel must consume final stores/services only.");

            var viewModel = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/PlanningToolViewModel.cs"));
            Assert.That(viewModel, Does.Not.Contain("PlanningToolService"));
            Assert.That(viewModel, Does.Not.Contain("SelectionService"));
            Assert.That(viewModel, Does.Not.Contain("public void Begin"));
            Assert.That(viewModel, Does.Not.Contain("public void Enter"));
            Assert.That(viewModel, Does.Not.Contain("public void Select"));
            Assert.That(viewModel, Does.Not.Contain("public void Clear"));
        }

        [Test]
        public void PlanningInputSlice_ShouldNotExposeLegacyCaches()
        {
            var root = ResolveAssetPath("Scripts/Runtime/Presentation/Planning/Input");
            Assert.That(
                File.Exists(Path.Combine(root, "PlanningInputContext.cs")),
                Is.False,
                "Planning input modes must not retain an empty compatibility context shell.");

            var offenders = FindTokenOffenders(
                new[] { root },
                "*.cs",
                "Panoptes.Core.Application.Cache",
                "GameStateCache",
                "PlanningDraftCache");

            Assert.That(offenders, Is.Empty, "Planning input context and modes must not expose legacy cache channels.");
        }

        [Test]
        public void MapInputAdapter_ShouldPublishToolAndSelectionState()
        {
            var controller = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapPlanningInputController.cs"));

            Assert.That(controller, Does.Contain("PlanningToolService"));
            Assert.That(controller, Does.Contain("PlanningToolViewModel"));
            Assert.That(controller, Does.Contain("SelectionService"));
            Assert.That(controller, Does.Contain("MapPlanningInputStateAdapter"));
            Assert.That(controller, Does.Contain("_inputState.PublishSelectedUnit(unit.UnitId)"));
            Assert.That(controller, Does.Not.Contain("private PlanningToolService"));
            Assert.That(controller, Does.Not.Contain("private SelectionService"));
            Assert.That(controller, Does.Not.Contain("private PlanningToolViewModel"));
            Assert.That(controller, Does.Not.Contain("private enum Mode"));
            Assert.That(controller, Does.Not.Contain("_mode"));
            Assert.That(
                CountOccurrences(controller, ".SetCombatActionMode("),
                Is.GreaterThan(0),
                "Controller should delegate combat mode mutations to the extracted adapter.");
        }

        [Test]
        public void PresentationAssembly_ShouldReferenceVContainer()
        {
            var asmdef = ResolveAssetPath("Scripts/Runtime/Presentation/Panoptes.Presentation.asmdef");
            var content = File.ReadAllText(asmdef);

            Assert.That(content, Does.Contain("\"VContainer\""));
        }

        [Test]
        public void GameScene_ShouldOwnGameLifetimeScope()
        {
            var scenePath = ResolveAssetPath("Scenes/Game.unity");
            var scriptMetaPath = ResolveAssetPath("Scripts/Runtime/Presentation/Composition/GameLifetimeScope.cs.meta");
            var sceneContent = File.ReadAllText(scenePath);
            var scriptGuid = ReadGuid(scriptMetaPath);

            Assert.That(sceneContent, Does.Contain("m_Name: Game Composition"));
            Assert.That(sceneContent, Does.Contain($"guid: {scriptGuid}"));
            Assert.That(sceneContent, Does.Contain("Panoptes.Presentation.Composition.GameLifetimeScope"));
        }

        private static List<string> FindTokenOffenders(IEnumerable<string> roots, string searchPattern, params string[] forbiddenTokens)
        {
            var offenders = new List<string>();
            foreach (var root in roots)
            {
                foreach (var path in EnumerateFiles(root, searchPattern))
                {
                    var content = File.ReadAllText(path);
                    for (var i = 0; i < forbiddenTokens.Length; i++)
                    {
                        var token = forbiddenTokens[i];
                        if (content.IndexOf(token, StringComparison.Ordinal) >= 0)
                        {
                            offenders.Add($"{ToProjectRelativePath(path)} contains {token}");
                        }
                    }
                }
            }

            return offenders;
        }

        private static IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        {
            if (File.Exists(path))
            {
                yield return path;
                yield break;
            }

            foreach (var file in Directory.EnumerateFiles(path, searchPattern, SearchOption.AllDirectories))
            {
                yield return file;
            }
        }

        private static string ResolveAssetPath(string assetRelativePath)
        {
            var localPath = Path.GetFullPath(Path.Combine("Assets", assetRelativePath));
            if (File.Exists(localPath) || Directory.Exists(localPath))
            {
                return localPath;
            }

            return Path.GetFullPath(Path.Combine("client", "Assets", assetRelativePath));
        }

        private static string ToProjectRelativePath(string path)
        {
            var fullPath = Path.GetFullPath(path).Replace('\\', '/');
            var assetsIndex = fullPath.LastIndexOf("/Assets/", StringComparison.Ordinal);
            return assetsIndex >= 0 ? fullPath.Substring(assetsIndex + 1) : fullPath;
        }

        private static string ReadGuid(string metaPath)
        {
            var guidLine = File.ReadLines(metaPath)
                .FirstOrDefault(line => line.StartsWith("guid:", StringComparison.Ordinal));
            Assert.That(guidLine, Is.Not.Null, $"{metaPath} does not contain a Unity guid.");
            return guidLine.Substring("guid:".Length).Trim();
        }

        private static int CountOccurrences(string content, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = content.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }
    }
}
