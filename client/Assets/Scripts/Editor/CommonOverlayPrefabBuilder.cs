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
            EditorApplication.delayCall += ValidatePrefabAssets;
        }

        [MenuItem("Panoptes/UI/Rebuild Common Overlay Prefabs")]
        public static void RebuildPrefabs()
        {
            SaveErrorToastPrefab(ErrorToastRuntimePrefabPath);
            SaveErrorToastPrefab(ErrorToastUiPrefabPath);
            SaveConfirmDialogPrefab(ConfirmDialogRuntimePrefabPath);
            SaveConfirmDialogPrefab(ConfirmDialogUiPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CommonOverlayPrefabBuilder] Rebuilt common overlay prefabs.");
        }

        private static void ValidatePrefabAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            WarnIfPrefabInvalid<ErrorToast>(ErrorToastRuntimePrefabPath, IsErrorToastMissingOrEmpty);
            WarnIfPrefabInvalid<ErrorToast>(ErrorToastUiPrefabPath, IsErrorToastMissingOrEmpty);
            WarnIfPrefabInvalid<ConfirmDialog>(ConfirmDialogRuntimePrefabPath, IsConfirmDialogMissingOrEmpty);
            WarnIfPrefabInvalid<ConfirmDialog>(ConfirmDialogUiPrefabPath, IsConfirmDialogMissingOrEmpty);
        }

        public static GameObject CreateErrorToastPrefabRoot()
        {
            var root = new GameObject("ErrorToast", typeof(RectTransform));
            var toast = root.AddComponent<ErrorToast>();
            toast.EditorRebuildUiForPrefab();
            NormalizeRootTransform(root);
            return root;
        }

        public static GameObject CreateConfirmDialogPrefabRoot()
        {
            var root = new GameObject("ConfirmDialog", typeof(RectTransform));
            var dialog = root.AddComponent<ConfirmDialog>();
            dialog.EditorRebuildUiForPrefab();
            NormalizeRootTransform(root);
            return root;
        }

        private static void SaveErrorToastPrefab(string prefabPath)
        {
            EnsureFolder(prefabPath);
            var root = CreateErrorToastPrefabRoot();
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                NormalizePrefabRootTransform(prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SaveConfirmDialogPrefab(string prefabPath)
        {
            EnsureFolder(prefabPath);
            var root = CreateConfirmDialogPrefabRoot();
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                NormalizePrefabRootTransform(prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void WarnIfPrefabInvalid<T>(string prefabPath, System.Func<GameObject, bool> isMissingOrEmpty)
            where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!isMissingOrEmpty(prefab))
            {
                return;
            }

            Debug.LogWarning(
                $"[CommonOverlayPrefabBuilder] {prefabPath} is missing or incomplete. " +
                $"Use Panoptes/UI/Rebuild Common Overlay Prefabs to regenerate {typeof(T).Name} assets.");
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

            NormalizeRectTransform(rectTransform);
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
        }

        private static void NormalizeRootTransform(GameObject root)
        {
            if (root == null || root.transform is not RectTransform rectTransform)
            {
                return;
            }

            NormalizeRectTransform(rectTransform);
        }

        private static void NormalizeRectTransform(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }
    }
}
