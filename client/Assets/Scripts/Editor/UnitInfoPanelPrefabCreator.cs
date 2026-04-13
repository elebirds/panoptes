#if UNITY_EDITOR
using System.IO;
using Panoptes.Presentation.UI.HUD;
using UnityEditor;
using UnityEngine;

namespace Panoptes.EditorTools
{
    public static class UnitInfoPanelPrefabCreator
    {
        private const string PrefabPath = "Assets/Prefabs/UI/UnitInfoPanel.prefab";

        [MenuItem("Panoptes/UI/Create Unit Info Panel Prefab")]
        public static void CreatePrefab()
        {
            EnsureFolderHierarchy(Path.GetDirectoryName(PrefabPath)?.Replace("\\", "/"));

            var temp = new GameObject(
                "UnitInfoPanel",
                typeof(RectTransform),
                typeof(UnitInfoActionRegistry),
                typeof(SettlerUnitActionRegistrar),
                typeof(UnitInfoPanelController));

            var panel = temp.GetComponent<UnitInfoPanelController>();
            panel.BuildDefaultLayoutForEditor();

            var saved = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (saved != null)
            {
                Selection.activeObject = saved;
                Debug.Log($"[UnitInfoPanelPrefabCreator] Prefab created: {PrefabPath}");
            }
            else
            {
                Debug.LogError($"[UnitInfoPanelPrefabCreator] Failed to create prefab: {PrefabPath}");
            }
        }

        private static void EnsureFolderHierarchy(string targetFolder)
        {
            if (string.IsNullOrWhiteSpace(targetFolder) || AssetDatabase.IsValidFolder(targetFolder))
            {
                return;
            }

            var parts = targetFolder.Split('/');
            if (parts.Length == 0)
            {
                return;
            }

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif

