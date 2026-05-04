using System;
using Panoptes.Presentation.UI.Common;
using UnityEngine;

namespace Panoptes.Presentation.Composition
{
    public static class PanoptesCompositionBootstrap
    {
        private const string ProjectCompositionPrefabPath = "Prefabs/Composition/PanoptesProjectComposition";
        private static bool _projectScopeStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureProjectScope()
        {
            if (_projectScopeStarted)
            {
                return;
            }

            var prefab = Resources.Load<GameObject>(ProjectCompositionPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Composition] Missing project composition prefab at Resources/{ProjectCompositionPrefabPath}.prefab");
                return;
            }

            var managers = UnityEngine.Object.Instantiate(prefab);
            managers.name = "Managers";
            UnityEngine.Object.DontDestroyOnLoad(managers);
            EnsureOptionalDebugPanel(managers);
            var overlays = managers.GetComponent<ProjectOverlayRegistry>();
            if (overlays == null)
            {
                Debug.LogError("[Composition] Project composition prefab is missing ProjectOverlayRegistry.");
                return;
            }

            overlays.Configure(
                EnsureProjectOverlay<ErrorToast>("ErrorToast", "Prefabs/UI/ErrorToast"),
                EnsureProjectOverlay<ConfirmDialog>("ConfirmDialog", "Prefabs/UI/ConfirmDialog"));
            managers.SetActive(true);
            _projectScopeStarted = true;
        }

        private static void EnsureOptionalDebugPanel(GameObject owner)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PANOPTES_DEBUG_PANEL
            var debugPanelType = Type.GetType("Panoptes.DebugTools.DebugPanel, Panoptes.Core");
            if (debugPanelType == null || owner.GetComponent(debugPanelType) != null)
            {
                return;
            }

            owner.AddComponent(debugPanelType);
#endif
        }

        private static T EnsureProjectOverlay<T>(string objectName, string resourcePath) where T : Component
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[Composition] Missing overlay prefab at Resources/{resourcePath}.prefab");
                return null;
            }

            var overlayObject = UnityEngine.Object.Instantiate(prefab);
            overlayObject.name = objectName;
            overlayObject.transform.SetParent(null, false);
            overlayObject.transform.localScale = Vector3.one;
            UnityEngine.Object.DontDestroyOnLoad(overlayObject);
            return overlayObject.GetComponent<T>();
        }
    }
}
