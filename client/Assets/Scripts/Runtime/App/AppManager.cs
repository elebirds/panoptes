/*************************************************
 * Project: Panoptes
 * File: AppManager.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Global app state machine placeholder.
 *************************************************/

using UnityEngine;
using Panoptes.Protocol.V1;
using Panoptes.Runtime.Network;
using Panoptes.Runtime.Service;
using Panoptes.Runtime.Cache;
using Panoptes.Runtime.UI.Common;
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
            TransitionTo(AppState.Login);
        }

        void OnDestroy()
        {
            if (MessageDispatcher.Instance != null)
            {
                MessageDispatcher.Instance.Unregister("MsgClientRuntimeConfig");
                MessageDispatcher.Instance.Unregister("MsgGameInit");
            }
        }

        public void TransitionTo(AppState newState)
        {
            State = newState;
            if (newState == AppState.Login)
            {
                RoomCache.Instance?.Clear();
                ClientRuntimeConfigCache.Instance?.Clear();
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
            MessageDispatcher.Instance.Register<MsgGameInit>("MsgGameInit", OnGameInit);
        }

        private void OnClientRuntimeConfig(MsgClientRuntimeConfig msg)
        {
            ClientRuntimeConfigCache.Instance?.Apply(msg);
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
    }
}
