/*************************************************
 * Project: Panoptes
 * File: AppManager.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Global app state machine placeholder.
 *************************************************/

using Panoptes.Protocol.V1;
using Panoptes.Runtime.Cache;
using Panoptes.Runtime.Network;
using Panoptes.Runtime.Service;
using Panoptes.Runtime.UI.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Panoptes.Runtime.App
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
        [SerializeField] private bool bypassLoginForLocalTest = true;
        [SerializeField] private string localTestSceneName = "MapEditor";
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
            EnsureComponent<StaticCatalogCache>(managers);
            EnsureComponent<RoomCache>(managers);
            EnsureComponent<GameStateCache>(managers);
            EnsureComponent<LoadingOverlay>(managers);
        }

        private static void EnsureComponent<T>(GameObject owner) where T : Component
        {
            if (owner.GetComponent<T>() == null)
            {
                owner.AddComponent<T>();
            }
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
                MessageDispatcher.Instance.Unregister("MsgGameInit");
                MessageDispatcher.Instance.Unregister("MsgStaticCatalogManifest");
            }
        }

        public void TransitionTo(AppState newState)
        {
            State = newState;
            if (newState == AppState.Login)
            {
                RoomCache.Instance?.Clear();
                ClientRuntimeConfigCache.Instance?.Clear();
                StaticCatalogCache.Instance?.Clear();
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
            MessageDispatcher.Instance.Register<MsgStaticCatalogManifest>("MsgStaticCatalogManifest", OnStaticCatalogManifest);
            MessageDispatcher.Instance.Register<MsgGameInit>("MsgGameInit", OnGameInit);
        }

        private void OnClientRuntimeConfig(MsgClientRuntimeConfig msg)
        {
            ClientRuntimeConfigCache.Instance?.Apply(msg);
        }

        private void OnStaticCatalogManifest(MsgStaticCatalogManifest msg)
        {
            StaticCatalogCache.EnsureInstance()?.ApplyManifest(msg?.Manifest);
        }

        private void OnGameInit(MsgGameInit msg)
        {
            GameStateCache.Instance?.ApplyGameInit(msg);
            TransitionTo(AppState.Game);
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

            return Application.CanStreamedLevelBeLoaded(sceneName);
        }
    }
}
