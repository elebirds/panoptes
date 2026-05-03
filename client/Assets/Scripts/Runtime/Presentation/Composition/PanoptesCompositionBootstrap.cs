using System;
using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Handler;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.UI.Common;
using UnityEngine;

namespace Panoptes.Presentation.Composition
{
    public sealed class ProjectOverlayRegistry : MonoBehaviour
    {
        public ErrorToast ErrorToast { get; private set; }
        public ConfirmDialog ConfirmDialog { get; private set; }

        public void Configure(ErrorToast errorToast, ConfirmDialog confirmDialog)
        {
            ErrorToast = errorToast;
            ConfirmDialog = confirmDialog;
        }
    }

    public static class PanoptesCompositionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureProjectScope()
        {
            var managers = GameObject.Find("Managers");
            if (managers == null)
            {
                managers = new GameObject("Managers");
            }

            UnityEngine.Object.DontDestroyOnLoad(managers);
            EnsureComponent<AppManager>(managers);
            EnsureComponent<NetworkManager>(managers);
            EnsureComponent<MessageDispatcher>(managers);
            EnsureComponent<SessionManager>(managers);
            EnsureComponent<ClientRuntimeConfigCache>(managers);
            EnsureComponent<ConfigCache>(managers);
            EnsureComponent<StaticCatalogCache>(managers);
            EnsureComponent<RoomCache>(managers);
            EnsureComponent<GameStateCache>(managers);
            EnsureComponent<GameChatCache>(managers);
            EnsureComponent<PlanningDraftCache>(managers);
            EnsureComponent<LobbyMessageHandler>(managers);
            EnsureComponent<GameMessageHandler>(managers);
            EnsureComponent<LoadingOverlay>(managers);
            EnsureOptionalDebugPanel(managers);
            var overlays = EnsureComponent<ProjectOverlayRegistry>(managers);
            overlays.Configure(
                EnsureProjectOverlay<ErrorToast>("ErrorToast", "Prefabs/UI/ErrorToast"),
                EnsureProjectOverlay<ConfirmDialog>("ConfirmDialog", "Prefabs/UI/ConfirmDialog"));
            EnsureComponent<ProjectLifetimeScope>(managers);
        }

        private static T EnsureComponent<T>(GameObject owner) where T : Component
        {
            var component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
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
            var instance = UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (instance != null)
            {
                return instance;
            }

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
