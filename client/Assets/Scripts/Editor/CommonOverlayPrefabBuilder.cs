using System.IO;
using Panoptes.Presentation.UI.Common;
using UnityEditor;
using UnityEngine;

namespace Panoptes.Editor
{
    [InitializeOnLoad]
    public static class CommonOverlayPrefabBuilder
    {
        private const string ErrorToastRuntimePrefabPath = "Assets/Resources/Prefabs/UI/ErrorToast.prefab";
        private const string ErrorToastUiPrefabPath = "Assets/Prefabs/UI/ErrorToast.prefab";
        private const string ConfirmDialogRuntimePrefabPath = "Assets/Resources/Prefabs/UI/ConfirmDialog.prefab";
        private const string ConfirmDialogUiPrefabPath = "Assets/Prefabs/UI/ConfirmDialog.prefab";

        static CommonOverlayPrefabBuilder()
        {
            EditorApplication.delayCall += AutoEnsurePrefabs;
        }

        [MenuItem("Panoptes/UI/Rebuild Common Overlay Prefabs")]
        public static void RebuildPrefabs()
        {
            EnsureErrorToastPrefab(ErrorToastRuntimePrefabPath, true);
            EnsureErrorToastPrefab(ErrorToastUiPrefabPath, true);
            EnsureConfirmDialogPrefab(ConfirmDialogRuntimePrefabPath, true);
            EnsureConfirmDialogPrefab(ConfirmDialogUiPrefabPath, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CommonOverlayPrefabBuilder] Rebuilt common overlay prefabs.");
        }

        private static void AutoEnsurePrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EnsureErrorToastPrefab(ErrorToastRuntimePrefabPath, false);
            EnsureErrorToastPrefab(ErrorToastUiPrefabPath, false);
            EnsureConfirmDialogPrefab(ConfirmDialogRuntimePrefabPath, false);
            EnsureConfirmDialogPrefab(ConfirmDialogUiPrefabPath, false);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureErrorToastPrefab(string prefabPath, bool forceRebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!forceRebuild && !IsErrorToastMissingOrEmpty(existing))
            {
                return;
            }

            EnsureFolder(prefabPath);
            var root = new GameObject("ErrorToast", typeof(RectTransform));
            try
            {
                var toast = root.AddComponent<ErrorToast>();
                toast.EditorRebuildUiForPrefab();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                NormalizePrefabRootTransform(prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureConfirmDialogPrefab(string prefabPath, bool forceRebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!forceRebuild && !IsConfirmDialogMissingOrEmpty(existing))
            {
                return;
            }

            EnsureFolder(prefabPath);
            var root = new GameObject("ConfirmDialog", typeof(RectTransform));
            try
            {
                var dialog = root.AddComponent<ConfirmDialog>();
                dialog.EditorRebuildUiForPrefab();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                NormalizePrefabRootTransform(prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static bool IsErrorToastMissingOrEmpty(GameObject prefab)
        {
            if (prefab == null || prefab.GetComponent<ErrorToast>() == null)
            {
                return true;
            }

            return prefab.transform.Find("ToastRoot") == null
                   || prefab.transform.Find("ToastRoot/Message") == null;
        }

        private static bool IsConfirmDialogMissingOrEmpty(GameObject prefab)
        {
            if (prefab == null || prefab.GetComponent<ConfirmDialog>() == null)
            {
                return true;
            }

            return prefab.transform.Find("Mask") == null
                   || prefab.transform.Find("PanelRoot") == null
                   || prefab.transform.Find("PanelRoot/TitleText") == null
                   || prefab.transform.Find("PanelRoot/MessageText") == null
                   || prefab.transform.Find("PanelRoot/ConfirmButton") == null
                   || prefab.transform.Find("PanelRoot/CancelButton") == null;
        }

        private static void EnsureFolder(string prefabPath)
        {
            var folder = Path.GetDirectoryName(prefabPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        // Unity 在某些版本里会把独立 RectTransform prefab 根节点重新写成 0 缩放；
        // 这里在保存后再对资产做一次归一化，避免运行时实例化出来整体不可见。
        private static void NormalizePrefabRootTransform(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null || prefab.transform is not RectTransform rectTransform)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
        }
    }
}
