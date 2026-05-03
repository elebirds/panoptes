using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Panoptes.Tests.EditMode
{
    public sealed class StaticClientBoundaryTests
    {
        private static readonly string[] PresentationProtocolForbiddenTokens =
        {
            "Panoptes.Protocol"
        };

        private static readonly string[] FoundationProtocolForbiddenTokens =
        {
            "Panoptes.Protocol"
        };

        private static readonly string[] UiNetworkForbiddenTokens =
        {
            "NetworkManager.Instance"
        };

        private static readonly string[] PresentationCommandForbiddenTokens =
        {
            "MessageSender.Send",
            "GameIntents.",
            "NetworkManager.Instance"
        };

        private static readonly IReadOnlyList<LineCountBaseline> HighRiskLineBaselines = new[]
        {
            new LineCountBaseline("Runtime/Presentation/Map/MapPlanningInputController.cs", 3376),
            new LineCountBaseline("Runtime/Presentation/UI/HUD/CityCoreBuildingActionRegistrar.cs", 650),
        };

        [Test]
        public void PresentationRuntime_ShouldNotReferenceProtocolAssembly()
        {
            var presentationRoot = ResolveAssetPath("Scripts/Runtime/Presentation");
            Assert.That(Directory.Exists(presentationRoot), Is.True, "Presentation runtime source directory is missing.");

            var offenders = FindTokenOffenders(presentationRoot, PresentationProtocolForbiddenTokens);

            Assert.That(offenders, Is.Empty, "Presentation must consume Core DTO/cache APIs instead of generated protocol types.");
        }

        [Test]
        public void PresentationUi_ShouldNotCallNetworkManagerSingletonDirectly()
        {
            var uiRoot = ResolveAssetPath("Scripts/Runtime/Presentation/UI");
            Assert.That(Directory.Exists(uiRoot), Is.True, "Presentation UI source directory is missing.");

            var offenders = FindTokenOffenders(uiRoot, UiNetworkForbiddenTokens);

            Assert.That(offenders, Is.Empty, "UI scripts must send through services/intents, not NetworkManager.Instance.");
        }

        [Test]
        public void PresentationRuntimeOutsideComposition_ShouldNotUseLegacyCommandEntrypoints()
        {
            var presentationRoot = ResolveAssetPath("Scripts/Runtime/Presentation");
            var files = Directory.GetFiles(presentationRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Composition{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .ToArray();

            var offenders = FindTokenOffenders(files, "*.cs", PresentationCommandForbiddenTokens);

            Assert.That(offenders, Is.Empty, "Presentation commands must route through injected Core services and IClientMessageSender.");
        }

        [Test]
        public void FormalCoreRuntime_ShouldNotUseLegacySingletonCompatibilityEntrypoints()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Core/Infrastructure/Mapper/NodeMapper.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Infrastructure/Mapper/UnitMapper.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Infrastructure/Network/GameEventSessionGate.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Infrastructure/Network/ServerEndpointResolver.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Application/Services/LocalGameSessionResetService.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Application/Cache/GameStateCache.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "StaticCatalogCache.Instance",
                "GameStateCache.Instance",
                "PlanningDraftCache.EnsureInstance",
                "PlanningDraftCache.Instance",
                "GameChatCache.Instance",
                "NetworkManager.Instance");

            Assert.That(offenders, Is.Empty, "Formal runtime bridges must receive dependencies from composition instead of static compatibility entrypoints.");
        }

        [Test]
        public void ArchitectureDocs_ShouldNotTeachLegacyClientCommandPath()
        {
            var roots = new[]
            {
                Path.GetFullPath("AGENTS.md"),
                Path.GetFullPath("docs/PANOPTES_AGENT_FRONTEND.md"),
                Path.GetFullPath("docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md"),
                Path.GetFullPath("docs/2026-05-01-client-reactive-ui-architecture-plan.md"),
                Path.GetFullPath(".trellis/spec/frontend/state-management.md")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.md",
                "MessageSender.Send",
                "GameIntents.",
                "NetworkManager.Instance",
                "MessageDispatcher.Instance.Register");

            Assert.That(offenders, Is.Empty, "Architecture docs must teach VContainer + Store/ViewModel/Binder + IClientMessageSender, not the retired static path.");
        }

        [Test]
        public void CoreFoundation_ShouldNotReferenceProtocolAssembly()
        {
            var foundationRoot = ResolveAssetPath("Scripts/Runtime/Core/Foundation");
            Assert.That(Directory.Exists(foundationRoot), Is.True, "Core/Foundation runtime source directory is missing.");

            var offenders = FindTokenOffenders(foundationRoot, FoundationProtocolForbiddenTokens);

            Assert.That(offenders, Is.Empty, "Core/Foundation must stay as pure DTO/value objects; protocol conversion belongs in Infrastructure/Mapper.");
        }

        [Test]
        public void HighRiskClientScripts_ShouldNotGrowPastC0p0Baseline()
        {
            var failures = new List<string>();
            for (var i = 0; i < HighRiskLineBaselines.Count; i++)
            {
                var baseline = HighRiskLineBaselines[i];
                var path = ResolveAssetPath($"Scripts/{baseline.RelativeScriptPath}");
                Assert.That(File.Exists(path), Is.True, $"{baseline.RelativeScriptPath} is missing.");

                var lineCount = File.ReadLines(path).Count();
                TestContext.Out.WriteLine($"{baseline.RelativeScriptPath}: {lineCount} lines (C0p0 baseline {baseline.MaxLines})");
                if (lineCount > baseline.MaxLines)
                {
                    failures.Add($"{baseline.RelativeScriptPath}: {lineCount} > {baseline.MaxLines}");
                }
            }

            Assert.That(failures, Is.Empty, "High-risk preflight scripts grew past the captured C0p0 baseline.");
        }

        [Test]
        public void FinalBuildCatalogSlice_ShouldNotReferenceLegacyBuildPanelOrSingletons()
        {
            var relativePaths = new[]
            {
                "Runtime/Presentation/Binders/UiToolkit/BuildCatalogUiToolkitBinder.cs",
                "Runtime/Presentation/UI/HUD/ResourceHUD.cs",
                "Runtime/Presentation/UI/HUD/CityCoreBuildingActionRegistrar.cs",
                "Runtime/Presentation/UI/HUD/CityCoreBuildingActionResolver.cs"
            };
            var forbidden = new[]
            {
                "BuildCommandPanel",
                "StaticCatalogCache",
                "GameStateCache",
                "PlanningDraftCache",
                "MapPlanningInputController.Instance",
                "NetworkManager.Instance",
                "Panoptes.Protocol",
                ".Instance"
            };

            var offenders = new List<string>();
            for (var i = 0; i < relativePaths.Length; i++)
            {
                var path = ResolveAssetPath($"Scripts/{relativePaths[i]}");
                Assert.That(File.Exists(path), Is.True, $"{relativePaths[i]} is missing.");
                var content = File.ReadAllText(path);
                for (var j = 0; j < forbidden.Length; j++)
                {
                    if (content.Contains(forbidden[j]))
                    {
                        offenders.Add($"{relativePaths[i]} contains {forbidden[j]}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, "Final build catalog slice must stay on Store/ViewModel/Binder/service dependencies.");
        }

        [Test]
        public void FinalRecipeSynthesisSlice_ShouldNotReferenceLegacyUguiPanelOrPrefabs()
        {
            var deletedPaths = new[]
            {
                "Scripts/Runtime/Presentation/UI/Turn/RecipeSynthesisPanel.cs",
                "Scripts/Runtime/Presentation/UI/Turn/RecipeSynthesisItemView.cs",
                "Scripts/Runtime/Presentation/UI/Turn/RecipeSynthesisRenderedItemRegistry.cs",
                "Scripts/Runtime/Presentation/UI/Domestic/BuildPanelSlideToggle.cs",
                "Prefabs/UI/RecipeSynthesisPanel.prefab",
                "Prefabs/UI/RecipeSynthesisItem.prefab"
            };

            for (var i = 0; i < deletedPaths.Length; i++)
            {
                Assert.That(File.Exists(ResolveAssetPath(deletedPaths[i])), Is.False, $"{deletedPaths[i]} should stay deleted.");
            }

            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation"),
                ResolveAssetPath("Scenes"),
                ResolveAssetPath("Prefabs")
            };
            var offenders = FindTokenOffenders(
                roots,
                "*.*",
                "RecipeSynthesisPanel",
                "RecipeSynthesisItemView",
                "RecipeSynthesisRenderedItemRegistry",
                "BuildPanelSlideToggle",
                "c3e5f1ab47d34b89b2a6d7e8f9012345",
                "d4f6a2bc58e14c79a3b7e8f901234567",
                "a1c3b74de4f24f2bb8e6528a4a6f9f11",
                "b2d4e95fa63f4e2cae58a2d5d6c4f123",
                "dd4396002ff64220943d245cfdea0462",
                "fbeaa39d74c5488997e1df03bd49a651");

            Assert.That(offenders, Is.Empty, "Final recipe synthesis must stay on context Store/ViewModel/UI Toolkit, without legacy uGUI refs.");
        }

        private static List<string> FindTokenOffenders(string root, IReadOnlyList<string> forbiddenTokens)
        {
            return FindTokenOffenders(new[] { root }, "*.cs", forbiddenTokens.ToArray());
        }

        private static List<string> FindTokenOffenders(
            IReadOnlyList<string> roots,
            string searchPattern,
            params string[] forbiddenTokens)
        {
            var offenders = new List<string>();
            for (var rootIndex = 0; rootIndex < roots.Count; rootIndex++)
            {
                var root = roots[rootIndex];
                foreach (var path in EnumerateFiles(root, searchPattern))
                {
                    var content = File.ReadAllText(path);
                    for (var i = 0; i < forbiddenTokens.Length; i++)
                    {
                        var token = forbiddenTokens[i];
                        if (content.IndexOf(token, StringComparison.Ordinal) < 0)
                        {
                            continue;
                        }

                        offenders.Add($"{ToProjectRelativePath(path)} contains {token}");
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

            if (!Directory.Exists(path))
            {
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

        private readonly struct LineCountBaseline
        {
            public readonly string RelativeScriptPath;
            public readonly int MaxLines;

            public LineCountBaseline(string relativeScriptPath, int maxLines)
            {
                RelativeScriptPath = relativeScriptPath;
                MaxLines = maxLines;
            }
        }
    }
}
