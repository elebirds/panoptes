using System.IO;
using Panoptes.Presentation.UI.Domestic;
using UnityEditor;
using UnityEngine;

namespace Panoptes.Editor
{
    [InitializeOnLoad]
    public static class CastleProductionPanelPrefabBuilder
    {
        private const string RuntimePrefabPath = "Assets/Resources/Prefabs/UI/CastleProductionPanel.prefab";
        private const string UiPrefabPath = "Assets/Prefabs/UI/CastleProductionPanel.prefab";

        static CastleProductionPanelPrefabBuilder()
        {
            EditorApplication.delayCall += AutoEnsurePrefabs;
        }

        [MenuItem("Panoptes/UI/Rebuild Castle Production Panel Prefabs")]
        public static void RebuildPrefabs()
        {
            EnsurePrefab(RuntimePrefabPath, forceRebuild: true);
            EnsurePrefab(UiPrefabPath, forceRebuild: true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CastleProductionPanelPrefabBuilder] Rebuilt CastleProductionPanel prefabs.");
        }

        private static void AutoEnsurePrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EnsurePrefab(RuntimePrefabPath, forceRebuild: false);
            EnsurePrefab(UiPrefabPath, forceRebuild: false);
            AssetDatabase.SaveAssets();
        }

        private static void EnsurePrefab(string prefabPath, bool forceRebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var needsRebuild = forceRebuild || IsMissingOrEmpty(existing);
            if (!needsRebuild)
            {
                return;
            }

            var folder = Path.GetDirectoryName(prefabPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var root = new GameObject("CastleProductionPanel", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<CastleProductionPanel>();
                panel.EditorRebuildUiForPrefab();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static bool IsMissingOrEmpty(GameObject prefab)
        {
            if (prefab == null)
            {
                return true;
            }

            var panel = prefab.GetComponent<CastleProductionPanel>();
            if (panel == null)
            {
                return true;
            }

            var root = prefab.transform;
            if (root == null || root.childCount == 0)
            {
                return true;
            }

            return root.Find("PanelRoot") == null;
        }
    }
}
