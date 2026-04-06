/*************************************************
 * Project: Panoptes
 * File: AppManager.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Global app state machine placeholder.
 *************************************************/

using System.Threading.Tasks;
using Panoptes.Protocol.V1;
using Panoptes.Protocol.V1.Auth;
using Panoptes.Runtime.Cache;
using Panoptes.Runtime.Network;
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

        public string PlayerID { get; private set; }
        public string Username { get; private set; }
        public string Token { get; private set; }

        [Header("Config")]
        [SerializeField] private string serverUrl = "ws://localhost:8080/ws";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            EnsureGlobalRuntimeServices();

            await NetworkManager.Instance.ConnectAsync(serverUrl);
            RegisterGlobalHandlers();
            TransitionTo(AppState.Login);
        }

        private void EnsureGlobalRuntimeServices()
        {
            ConfigCache.EnsureInstance();

            if (UnityEngine.Object.FindObjectOfType<ConfigMessageBridge>() == null)
            {
                var go = new GameObject("ConfigMessageBridge");
                go.AddComponent<ConfigMessageBridge>();
            }
        }

        private void RegisterGlobalHandlers()
        {
            MessageDispatcher.Instance.Register<MsgLoginSuccess>("MsgLoginSuccess", OnLoginSuccess);
            MessageDispatcher.Instance.Register<MsgGameStarting>("MsgGameStarting", OnGameStarting);
            MessageDispatcher.Instance.Register<MsgGameInit>("MsgGameInit", OnGameInit);

            NetworkManager.Instance.OnDisconnected += OnDisconnected;
        }

        private void OnLoginSuccess(MsgLoginSuccess msg)
        {
            PlayerID = msg.PlayerId;
            Username = msg.Username;
            Token = msg.Token;

            PlayerPrefs.SetString("token", msg.Token);
            PlayerPrefs.SetString("player_id", msg.PlayerId);

            TransitionTo(AppState.Lobby);
        }

        private void OnGameStarting(MsgGameStarting msg)
        {
            // Handled by lobby UI countdown. Scene transition waits for MsgGameInit.
        }

        private void OnGameInit(MsgGameInit msg)
        {
            if (GameStateCache.Instance != null)
            {
                GameStateCache.Instance.ApplyGameInit(msg);
            }

            TransitionTo(AppState.Game);
        }

        private void OnDisconnected()
        {
            Debug.Log("[App] Disconnected, attempting reconnect...");
        }

        public void TransitionTo(AppState newState)
        {
            State = newState;
            switch (newState)
            {
                case AppState.Login:
                    SceneManager.LoadScene("Login");
                    break;
                case AppState.Lobby:
                    SceneManager.LoadScene("Lobby");
                    break;
                case AppState.Game:
                    SceneManager.LoadScene("Game");
                    break;
            }
        }

        public void Logout()
        {
            PlayerID = null;
            Username = null;
            Token = null;
            PlayerPrefs.DeleteKey("token");
            PlayerPrefs.DeleteKey("player_id");
            TransitionTo(AppState.Login);
        }
    }
}
