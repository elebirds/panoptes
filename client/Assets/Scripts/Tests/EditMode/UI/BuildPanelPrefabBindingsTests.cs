using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class BuildPanelPrefabBindingsTests
    {
        [Test]
        public void BuildPanelRootPrefab_ShouldUseGroupedScrollRectBindings()
        {
            var prefabText = ReadPrefabText("Prefabs/UI/Build/BuildPanelRoot.prefab");

            StringAssert.Contains("UnityEngine.UI::UnityEngine.UI.ScrollRect", prefabText);
            StringAssert.Contains("m_Name: Viewport", prefabText);
            StringAssert.Contains("m_Name: Content", prefabText);
            StringAssert.Contains("listScrollRect: {fileID:", prefabText);
            StringAssert.Contains("listContent: {fileID:", prefabText);
            StringAssert.Contains("buildGroupPrefab: {fileID:", prefabText);
            StringAssert.Contains("buildItemPrefab: {fileID:", prefabText);
            StringAssert.DoesNotContain("modeToggleGroup:", prefabText);
            StringAssert.DoesNotContain("buildButtons:", prefabText);
            StringAssert.DoesNotContain("buildStatusText:", prefabText);
        }

        [Test]
        public void BuildGroupPrefab_ShouldBindBuildGroupView_InsteadOfBuildItemView()
        {
            var prefabText = ReadPrefabText("Prefabs/UI/Build/BuildGroup.prefab");

            StringAssert.Contains("Panoptes.Presentation.UI.Domestic.BuildGroupView", prefabText);
            StringAssert.DoesNotContain("Panoptes.Presentation.UI.Domestic.BuildItemView", prefabText);
        }

        [Test]
        public void BuildItemPrefab_ShouldKeepNewBuildItemViewFields()
        {
            var prefabText = ReadPrefabText("Prefabs/UI/Build/BuildItem.prefab");

            StringAssert.Contains("backgroundImage:", prefabText);
            StringAssert.Contains("metricTemplate:", prefabText);
            StringAssert.DoesNotContain("needMatrialListRoot:", prefabText);
            StringAssert.DoesNotContain("availableAccentColor:", prefabText);
        }

        private static string ReadPrefabText(string relativeAssetPath)
        {
            var path = Path.Combine(Application.dataPath, relativeAssetPath);
            Assert.That(File.Exists(path), Is.True, $"缺少 prefab: {path}");
            return File.ReadAllText(path);
        }
    }
}
