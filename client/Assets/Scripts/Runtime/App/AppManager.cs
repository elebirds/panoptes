/*************************************************
 * Project: Panoptes
 * File: AppManager.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Global app state machine placeholder.
 *************************************************/

using UnityEngine;
using System.Threading.Tasks;
using Panoptes.Runtime.Protocol;
using Panoptes.Runtime.Network;
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

        // 登录后保存
        public string PlayerID { get; private set; }
        public string Username { get; private set; }
        public string Token { get; private set; }

        [Header("Config")]
        [SerializeField] private string serverUrl = "ws://localhost:8080/ws";

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

        async void Start()
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // 连接服务端
            await NetworkManager.Instance.ConnectAsync(serverUrl);

            // 注册全局消息处理
            RegisterGlobalHandlers();

            // 跳转登录
            TransitionTo(AppState.Login);
        }

        private void RegisterGlobalHandlers()
        {
            // 登录成功
            MessageDispatcher.Instance.Register<MsgLoginSuccess>(
                "MsgLoginSuccess", OnLoginSuccess);

            // 游戏开始
            MessageDispatcher.Instance.Register<MsgGameStarting>(
                "MsgGameStarting", OnGameStarting);

            // 游戏初始化
            MessageDispatcher.Instance.Register<MsgGameInit>(
                "MsgGameInit", OnGameInit);

            // 断线处理
            NetworkManager.Instance.OnDisconnected += OnDisconnected;
        }

        private void OnLoginSuccess(MsgLoginSuccess msg)
        {
            PlayerID = msg.PlayerId;
            Username = msg.Username;
            Token = msg.Token;

            // 保存 token 供重连使用
            PlayerPrefs.SetString("token", msg.Token);
            PlayerPrefs.SetString("player_id", msg.PlayerId);

            TransitionTo(AppState.Lobby);
        }

        private void OnGameStarting(MsgGameStarting msg)
        {
            // 倒计时由 LobbyPanel 处理，这里不做场景跳转
            // MsgGameInit 收到后再跳转
        }

        private void OnGameInit(MsgGameInit msg)
        {
            TransitionTo(AppState.Game);
        }

        private void OnDisconnected()
        {
            Debug.Log("[App] Disconnected, attempting reconnect...");
            // 断线重连逻辑由 NetworkManager 处理
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
