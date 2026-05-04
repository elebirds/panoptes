using Panoptes.Presentation.Binders.UiToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Panoptes.Editor
{
    public static class PolicyFocusPanelPrefabBuilder
    {
        private const string PrefabPath = "Assets/Resources/Prefabs/UI/Policy/PolicyFocusPanel.prefab";
        private const string UxmlPath = "Assets/UI/Toolkit/Policy/PolicyFocusPanel.uxml";
        private const string UssPath = "Assets/UI/Toolkit/Policy/PolicyFocusPanel.uss";
        private const string PanelSettingsPath = "Assets/UI/Toolkit/Policy/PolicyFocusPanelSettings.asset";

        [MenuItem("Panoptes/UI/Rebuild Policy Focus Panel Prefab")]
        public static void Rebuild()
        {
            EnsureFolder("Assets/Resources/Prefabs/UI", "Policy");

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (visualTree == null || styleSheet == null)
            {
                Debug.LogError($"[PolicyFocusPanelPrefabBuilder] Missing UI Toolkit assets. UXML={visualTree != null} USS={styleSheet != null}");
                return;
            }

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            var temp = new GameObject("PolicyFocusPanel", typeof(RectTransform));
            try
            {
                var rect = temp.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var document = temp.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = visualTree;

                var binder = temp.AddComponent<PolicyFocusUiToolkitBinder>();
                var serializedBinder = new SerializedObject(binder);
                serializedBinder.FindProperty("visualTreeAsset").objectReferenceValue = visualTree;
                serializedBinder.FindProperty("styleSheet").objectReferenceValue = styleSheet;
                serializedBinder.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[PolicyFocusPanelPrefabBuilder] Failed to create prefab: {PrefabPath}");
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[PolicyFocusPanelPrefabBuilder] Prefab created: {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
