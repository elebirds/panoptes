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
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Events;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
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

        private bool _pendingCatalogSync;
        private MsgGameInit _deferredGameInit;
        private StaticCatalogStoreHydrator _staticCatalogStoreHydrator;
        private NetworkManager _networkManager;
        private MessageDispatcher _messageDispatcher;
        private SessionManager _sessionManager;
        private ClientRuntimeConfigCache _clientRuntimeConfigCache;
        private ConfigCache _configCache;
        private StaticCatalogCache _staticCatalogCache;
        private RoomCache _roomCache;
        private GameStateCache _gameStateCache;
        private GameChatCache _gameChatCache;
        private IClientMessageSender _messageSender;

        public void UseProjectServices(
            NetworkManager networkManager,
            MessageDispatcher messageDispatcher,
            SessionManager sessionManager,
            ClientRuntimeConfigCache clientRuntimeConfigCache,
            ConfigCache configCache,
            StaticCatalogCache staticCatalogCache,
            RoomCache roomCache,
            GameStateCache gameStateCache,
            GameChatCache gameChatCache,
            IClientMessageSender messageSender)
        {
            _networkManager = networkManager;
            _messageDispatcher = messageDispatcher;
            _sessionManager = sessionManager;
            _clientRuntimeConfigCache = clientRuntimeConfigCache;
            _configCache = configCache;
            _staticCatalogCache = staticCatalogCache;
            _roomCache = roomCache;
            _gameStateCache = gameStateCache;
            _gameChatCache = gameChatCache;
            _messageSender = messageSender;
            HydrateStaticCatalogStore(_staticCatalogCache);
        }

        void Start()
        {
            RegisterGlobalHandlers();
            HydrateStaticCatalogStore(_staticCatalogCache);
            if (bypassLoginForLocalTest)
            {
                EnterLocalTestMode();
                return;
            }

            TransitionTo(AppState.Login);
        }

        void OnDestroy()
        {
            if (_messageDispatcher != null)
            {
                _messageDispatcher.Unregister("MsgClientRuntimeConfig");
                _messageDispatcher.Unregister("MsgConfigBatchJson");
                _messageDispatcher.Unregister("MsgGameInit");
                _messageDispatcher.Unregister("MsgStaticCatalogManifest");
                _messageDispatcher.Unregister("MsgStaticCatalogSectionChunk");
                _messageDispatcher.Unregister("MsgStaticCatalogSyncComplete");
                _messageDispatcher.Unregister("MsgStaticCatalogSnapshot");
                _messageDispatcher.Unregister("Problem");
            }
        }

        public void TransitionTo(AppState newState)
        {
            State = newState;
            if (newState == AppState.Login)
            {
                _pendingCatalogSync = false;
                _deferredGameInit = null;
                ClearLobbyRoomCache();
                _clientRuntimeConfigCache?.Clear();
                _configCache?.Clear();
                _gameStateCache?.Clear();
                _gameChatCache?.Clear();
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
            if (_messageDispatcher == null)
            {
                return;
            }

            _messageDispatcher.Register<MsgClientRuntimeConfig>("MsgClientRuntimeConfig", OnClientRuntimeConfig);
            _messageDispatcher.Register<MsgConfigBatchJson>("MsgConfigBatchJson", OnConfigBatchJson);
            _messageDispatcher.Register<MsgStaticCatalogManifest>("MsgStaticCatalogManifest", OnStaticCatalogManifest);
            _messageDispatcher.Register<MsgStaticCatalogSectionChunk>("MsgStaticCatalogSectionChunk", OnStaticCatalogSectionChunk);
            _messageDispatcher.Register<MsgStaticCatalogSyncComplete>("MsgStaticCatalogSyncComplete", OnStaticCatalogSyncComplete);
            _messageDispatcher.Register<MsgStaticCatalogSnapshot>("MsgStaticCatalogSnapshot", OnStaticCatalogSnapshot);
            _messageDispatcher.Register<MsgGameInit>("MsgGameInit", OnGameInit);
            _messageDispatcher.Register<Problem>("Problem", OnProblem);
        }

        private void OnClientRuntimeConfig(MsgClientRuntimeConfig msg)
        {
            _clientRuntimeConfigCache?.Apply(msg);
        }

        private void OnConfigBatchJson(MsgConfigBatchJson msg)
        {
            _configCache?.ApplyBatch(msg);
        }

        private void OnStaticCatalogManifest(MsgStaticCatalogManifest msg)
        {
            var cache = _staticCatalogCache;
            var decision = cache?.CompareManifest(msg?.Manifest) ?? new StaticCatalogCache.CatalogSyncDecision();
            HydrateStaticCatalogStore(cache);
            cache?.BeginSectionSync(msg?.Manifest, decision.RequestedSections);

            _pendingCatalogSync = true;
            _deferredGameInit = null;

            var request = new MsgStaticCatalogSyncRequest
            {
                BundleHash = cache != null && cache.LocalManifest != null ? cache.LocalManifest.bundle_hash ?? string.Empty : string.Empty,
                ForceFullSync = decision.ForceFullSync,
            };
            if (decision.RequestedSections != null && decision.RequestedSections.Length > 0)
            {
                request.SectionNames.Add(decision.RequestedSections);
            }
            _messageSender?.Send(request);
        }

        private void OnStaticCatalogSectionChunk(MsgStaticCatalogSectionChunk msg)
        {
            _staticCatalogCache?.ApplySectionChunk(msg);
        }

        private void OnStaticCatalogSyncComplete(MsgStaticCatalogSyncComplete msg)
        {
            var cache = _staticCatalogCache;
            var synchronized = cache?.FinalizeSectionSync(msg) ?? false;
            _pendingCatalogSync = false;
            if (!synchronized)
            {
                _deferredGameInit = null;
                return;
            }

            HydrateStaticCatalogStore(cache);

            if (_deferredGameInit == null)
            {
                return;
            }

            ApplyGameInitAndTransition(_deferredGameInit);
            _deferredGameInit = null;
        }

        private void OnStaticCatalogSnapshot(MsgStaticCatalogSnapshot msg)
        {
            var cache = _staticCatalogCache;
            cache?.ApplySnapshot(msg?.Snapshot);
            HydrateStaticCatalogStore(cache);
        }

        public void UseStaticCatalogStoreHydrator(StaticCatalogStoreHydrator hydrator)
        {
            _staticCatalogStoreHydrator = hydrator;
            HydrateStaticCatalogStore(_staticCatalogCache);
        }

        private void HydrateStaticCatalogStore(StaticCatalogCache cache)
        {
            _staticCatalogStoreHydrator?.HydrateFromCache(cache);
        }

        private void OnGameInit(MsgGameInit msg)
        {
            if (_pendingCatalogSync)
            {
                _deferredGameInit = msg;
                return;
            }

            ApplyGameInitAndTransition(msg);
        }

        private void ApplyGameInitAndTransition(MsgGameInit msg)
        {
            _gameChatCache?.Clear();
            _gameStateCache?.ApplyGameInit(msg);
            _roomCache?.Clear();
            TransitionTo(AppState.Game);
        }

        private void ClearLobbyRoomCache()
        {
            _roomCache?.Clear();
        }

        private void OnProblem(Problem problem)
        {
            var code = problem != null ? (problem.Code ?? string.Empty) : string.Empty;
            var message = problem != null ? (problem.Message ?? string.Empty) : string.Empty;

            switch (State)
            {
                case AppState.Lobby:
                    _roomCache?.PublishLobbyError(new MsgLobbyError
                    {
                        Code = code,
                        Message = message
                    });
                    break;
                case AppState.Game:
                    _gameStateCache?.PublishGameError(new GameErrorEvent
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

            if (_networkManager == null ||
                _networkManager.IsConnected ||
                _networkManager.IsConnecting)
            {
                return;
            }

            if (_sessionManager == null || !_sessionManager.IsLoggedIn)
            {
                return;
            }

            try
            {
                await _networkManager.ConnectWithSessionAsync();
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
