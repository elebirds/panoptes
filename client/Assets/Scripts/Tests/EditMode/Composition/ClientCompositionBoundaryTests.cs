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
        public void GameScope_ShouldNotRegisterLegacyCachesBeforeStoreMigration()
        {
            var root = ResolveAssetPath("Scripts/Runtime/Presentation/Composition");
            var offenders = FindTokenOffenders(
                new[] { root },
                "*.cs",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache");

            Assert.That(offenders, Is.Empty, "GameLifetimeScope must wait for final Stores instead of registering legacy cache facades.");
        }

        [Test]
        public void PresentationAssembly_ShouldReferenceVContainer()
        {
            var asmdef = ResolveAssetPath("Scripts/Runtime/Presentation/Panoptes.Presentation.asmdef");
            var content = File.ReadAllText(asmdef);

            Assert.That(content, Does.Contain("\"VContainer\""));
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
    }
}
