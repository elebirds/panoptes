/*************************************************
 * Project: Panoptes
 * File: AppManager.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Global app state machine placeholder.
 *************************************************/

using Panoptes.Protocol.V1;
using Panoptes.Core.Application.Handler;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Events;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Panoptes.DebugTools;
#endif
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Panoptes.Core.Application.App
{
    public enum AppState
    {
        Initializing,
        Login,
        Lobby,
        Game
    }

    public class AppManager : MonoBehaviour
    {
        public static AppManager Instance { get; private set; }

        public AppState State { get; private set; } = AppState.Initializing;

        [Header("Config")]
        [SerializeField] private string loginSceneName = "Login";
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private string gameSceneName = "Game";

        [Header("Local Test")]
        [SerializeField] private bool bypassLoginForLocalTest = false;
        [SerializeField] private string localTestSceneName = "Game";
        [SerializeField] private AppState localTestState = AppState.Game;
        [SerializeField] private bool logLocalTestBypass = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureManagersBootstrap()
        {
            var managers = GameObject.Find("Managers");
            if (managers == null)
            {
                managers = new GameObject("Managers");
            }

            DontDestroyOnLoad(managers);
            EnsureComponent<AppManager>(managers);
            EnsureComponent<NetworkManager>(managers);
            EnsureComponent<MessageDispatcher>(managers);
            EnsureComponent<SessionManager>(managers);
            EnsureComponent<ClientRuntimeConfigCache>(managers);
            EnsureComponent<ConfigCache>(managers);
            EnsureComponent<StaticCatalogCache>(managers);
            EnsureComponent<RoomCache>(managers);
            EnsureComponent<GameStateCache>(managers);
            EnsureComponent<PlanningDraftCache>(managers);
            EnsureComponent<LobbyMessageHandler>(managers);
            EnsureComponent<GameMessageHandler>(managers);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureComponent<DebugPanel>(managers);
#endif
            EnsureOptionalLoadingOverlay(managers);
            EnsureOptionalErrorToast(managers);
            EnsureOptionalConfirmDialog(managers);
        }

        private static void EnsureComponent<T>(GameObject owner) where T : Component
        {
            if (owner.GetComponent<T>() == null)
            {
                owner.AddComponent<T>();
            }
        }

        private static void EnsureOptionalLoadingOverlay(GameObject owner)
        {
            var overlayType = Type.GetType("Panoptes.Presentation.UI.Common.LoadingOverlay, Panoptes.Presentation");
            if (overlayType == null || owner.GetComponent(overlayType) != null)
            {
                return;
            }

            owner.AddComponent(overlayType);
        }

        private static void EnsureOptionalErrorToast(GameObject owner)
        {
            var overlayType = Type.GetType("Panoptes.Presentation.UI.Common.ErrorToast, Panoptes.Presentation");
            EnsureOptionalOverlayPrefab(owner, overlayType, "ErrorToast", "Prefabs/UI/ErrorToast");
        }

        private static void EnsureOptionalConfirmDialog(GameObject owner)
        {
            var overlayType = Type.GetType("Panoptes.Presentation.UI.Common.ConfirmDialog, Panoptes.Presentation");
            EnsureOptionalOverlayPrefab(owner, overlayType, "ConfirmDialog", "Prefabs/UI/ConfirmDialog");
        }

        // LoadingOverlay 直接挂在 Managers 上，因此其他通用弹层必须作为独立根对象存在，
        // 否则会被 Managers 上的 CanvasGroup 一起隐藏。
        private static void EnsureOptionalOverlayPrefab(GameObject owner, Type overlayType, string objectName, string resourcePath)
        {
            if (overlayType == null || owner == null)
            {
                return;
            }

            var existing = GameObject.Find(objectName);
            if (existing != null && existing.GetComponent(overlayType) != null)
            {
                return;
            }

            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[AppManager] Missing overlay prefab at Resources/{resourcePath}.prefab");
                return;
            }

            var overlayObject = UnityEngine.Object.Instantiate(prefab);
            overlayObject.name = objectName;
            overlayObject.transform.SetParent(null, false);
            overlayObject.transform.localScale = Vector3.one;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            RegisterGlobalHandlers();
            if (bypassLoginForLocalTest)
            {
                EnterLocalTestMode();
                return;
            }

            TransitionTo(AppState.Login);
        }

        void OnDestroy()
        {
            if (MessageDispatcher.Instance != null)
            {
                MessageDispatcher.Instance.Unregister("MsgClientRuntimeConfig");
                MessageDispatcher.Instance.Unregister("MsgConfigBatchJson");
                MessageDispatcher.Instance.Unregister("MsgGameInit");
                MessageDispatcher.Instance.Unregister("MsgStaticCatalogManifest");
                MessageDispatcher.Instance.Unregister("MsgStaticCatalogSnapshot");
                MessageDispatcher.Instance.Unregister("Problem");
            }
        }

        public void TransitionTo(AppState newState)
        {
            State = newState;
            if (newState == AppState.Login)
            {
                RoomCache.Instance?.Clear();
                ClientRuntimeConfigCache.Instance?.Clear();
                ConfigCache.Instance?.Clear();
                GameStateCache.Instance?.Clear();
            }

            EnsureRealtimeConnectionIfNeeded(newState);

            var sceneName = newState switch
            {
                AppState.Login => loginSceneName,
                AppState.Lobby => lobbySceneName,
                AppState.Game => gameSceneName,
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene().name;
            if (activeScene == sceneName)
            {
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        private void RegisterGlobalHandlers()
        {
            if (MessageDispatcher.Instance == null)
            {
                return;
            }

            MessageDispatcher.Instance.Register<MsgClientRuntimeConfig>("MsgClientRuntimeConfig", OnClientRuntimeConfig);
            MessageDispatcher.Instance.Register<MsgConfigBatchJson>("MsgConfigBatchJson", OnConfigBatchJson);
            MessageDispatcher.Instance.Register<MsgStaticCatalogManifest>("MsgStaticCatalogManifest", OnStaticCatalogManifest);
            MessageDispatcher.Instance.Register<MsgStaticCatalogSnapshot>("MsgStaticCatalogSnapshot", OnStaticCatalogSnapshot);
            MessageDispatcher.Instance.Register<MsgGameInit>("MsgGameInit", OnGameInit);
            MessageDispatcher.Instance.Register<Problem>("Problem", OnProblem);
        }

        private void OnClientRuntimeConfig(MsgClientRuntimeConfig msg)
        {
            ClientRuntimeConfigCache.Instance?.Apply(msg);
        }

        private void OnConfigBatchJson(MsgConfigBatchJson msg)
        {
            ConfigCache.Instance?.ApplyBatch(msg);
        }

        private void OnStaticCatalogManifest(MsgStaticCatalogManifest msg)
        {
            StaticCatalogCache.EnsureInstance()?.ApplyManifest(msg?.Manifest);
        }

        private void OnStaticCatalogSnapshot(MsgStaticCatalogSnapshot msg)
        {
            StaticCatalogCache.EnsureInstance()?.ApplySnapshot(msg?.Snapshot);
        }

        private void OnGameInit(MsgGameInit msg)
        {
            GameStateCache.Instance?.ApplyGameInit(msg);
            TransitionTo(AppState.Game);
        }

        private void OnProblem(Problem problem)
        {
            var code = problem != null ? (problem.Code ?? string.Empty) : string.Empty;
            var message = problem != null ? (problem.Message ?? string.Empty) : string.Empty;

            switch (State)
            {
                case AppState.Lobby:
                    RoomCache.Instance?.PublishLobbyError(new MsgLobbyError
                    {
                        Code = code,
                        Message = message
                    });
                    break;
                case AppState.Game:
                    GameStateCache.Instance?.PublishGameError(new GameErrorEvent
                    {
                        Code = code,
                        Message = message
                    });
                    break;
                default:
                    Debug.LogWarning($"[AppManager] Problem received code={code} message={message}");
                    break;
            }
        }

        private async void EnsureRealtimeConnectionIfNeeded(AppState state)
        {
            if (state != AppState.Lobby && state != AppState.Game)
            {
                return;
            }

            if (NetworkManager.Instance == null ||
                NetworkManager.Instance.IsConnected ||
                NetworkManager.Instance.IsConnecting)
            {
                return;
            }

            if (SessionManager.Instance == null || !SessionManager.Instance.IsLoggedIn)
            {
                return;
            }

            try
            {
                await NetworkManager.Instance.ConnectWithSessionAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AppManager] Failed to establish realtime connection: {e.Message}");
            }
        }

        private void EnterLocalTestMode()
        {
            State = localTestState;

            var sceneName = string.IsNullOrWhiteSpace(localTestSceneName)
                ? gameSceneName
                : localTestSceneName.Trim();

            if (logLocalTestBypass)
            {
                Debug.Log($"[AppManager] Local test mode enabled, bypass login and load scene '{sceneName}'.");
            }

            if (!CanLoadScene(sceneName))
            {
                Debug.LogWarning($"[AppManager] Local test scene '{sceneName}' is not loadable. Fallback to '{gameSceneName}'.");
                sceneName = gameSceneName;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene().name;
            if (activeScene == sceneName)
            {
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        private static bool CanLoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            return UnityEngine.Application.CanStreamedLevelBeLoaded(sceneName);
        }
    }
}
