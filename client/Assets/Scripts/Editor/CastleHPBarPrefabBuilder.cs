using System.IO;
using Panoptes.Presentation.UI.HUD;
using UnityEditor;
using UnityEngine;

namespace Panoptes.Editor
{
    [InitializeOnLoad]
    public static class CastleHPBarPrefabBuilder
    {
        private const string RuntimePrefabPath = "Assets/Resources/Prefabs/UI/CastleHPBar.prefab";
        private const string UiPrefabPath = "Assets/Prefabs/UI/CastleHPBar.prefab";

        static CastleHPBarPrefabBuilder()
        {
            EditorApplication.delayCall += AutoEnsurePrefabs;
        }

        [MenuItem("Panoptes/UI/Rebuild Castle HP Bar Prefabs")]
        public static void RebuildPrefabs()
        {
            EnsurePrefab(RuntimePrefabPath, forceRebuild: true);
            EnsurePrefab(UiPrefabPath, forceRebuild: true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CastleHPBarPrefabBuilder] Rebuilt CastleHPBar prefabs.");
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
            var needsRebuild = forceRebuild || IsMissingOrInvalid(existing);
            if (!needsRebuild)
            {
                return;
            }

            var folder = Path.GetDirectoryName(prefabPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var root = new GameObject("CastleHPBar", typeof(RectTransform));
            try
            {
                var hpBar = root.AddComponent<CastleHPBar>();
                hpBar.EditorRebuildUiForPrefab();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static bool IsMissingOrInvalid(GameObject prefab)
        {
            if (prefab == null)
            {
                return true;
            }

            var hpBar = prefab.GetComponent<CastleHPBar>();
            if (hpBar == null)
            {
                return true;
            }

            var root = prefab.transform;
            if (root == null || root.childCount == 0)
            {
                return true;
            }

            return root.Find("FactionPlate") == null || root.Find("HpBarBackground") == null;
        }
    }
}
