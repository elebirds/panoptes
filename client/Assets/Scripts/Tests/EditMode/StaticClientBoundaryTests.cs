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

        private static readonly IReadOnlyList<LineCountBaseline> HighRiskLineBaselines = new[]
        {
            new LineCountBaseline("Runtime/Presentation/Map/MapPlanningInputController.cs", 3376),
            new LineCountBaseline("Runtime/Presentation/UI/Turn/RecipeSynthesisPanel.cs", 1392),
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

        private static List<string> FindTokenOffenders(string root, IReadOnlyList<string> forbiddenTokens)
        {
            var offenders = new List<string>();
            foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(path);
                for (var i = 0; i < forbiddenTokens.Count; i++)
                {
                    var token = forbiddenTokens[i];
                    if (content.IndexOf(token, StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    offenders.Add($"{ToProjectRelativePath(path)} contains {token}");
                }
            }

            return offenders;
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
