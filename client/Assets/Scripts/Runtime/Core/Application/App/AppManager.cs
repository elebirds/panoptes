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
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Events;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PANOPTES_DEBUG_PANEL
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
            EnsureComponent<GameChatCache>(managers);
            EnsureComponent<PlanningDraftCache>(managers);
            EnsureComponent<LobbyMessageHandler>(managers);
            EnsureComponent<GameMessageHandler>(managers);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PANOPTES_DEBUG_PANEL
            EnsureComponent<DebugPanel>(managers);
#endif
            EnsureOptionalLoadingOverlay(managers);
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

        void Start()
        {
            RegisterGlobalHandlers();
            HydrateStaticCatalogStore(StaticCatalogCache.Instance);
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
                MessageDispatcher.Instance.Unregister("MsgStaticCatalogSectionChunk");
                MessageDispatcher.Instance.Unregister("MsgStaticCatalogSyncComplete");
                MessageDispatcher.Instance.Unregister("MsgStaticCatalogSnapshot");
                MessageDispatcher.Instance.Unregister("Problem");
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
                ClientRuntimeConfigCache.Instance?.Clear();
                ConfigCache.Instance?.Clear();
                GameStateCache.Instance?.Clear();
                GameChatCache.Instance?.Clear();
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
            MessageDispatcher.Instance.Register<MsgStaticCatalogSectionChunk>("MsgStaticCatalogSectionChunk", OnStaticCatalogSectionChunk);
            MessageDispatcher.Instance.Register<MsgStaticCatalogSyncComplete>("MsgStaticCatalogSyncComplete", OnStaticCatalogSyncComplete);
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
            var cache = StaticCatalogCache.EnsureInstance();
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
            MessageSender.Send(new MsgStaticCatalogSyncRequest
            {
                BundleHash = request.BundleHash,
                ForceFullSync = request.ForceFullSync,
                SectionNames = { request.SectionNames }
            });
        }

        private void OnStaticCatalogSectionChunk(MsgStaticCatalogSectionChunk msg)
        {
            StaticCatalogCache.EnsureInstance()?.ApplySectionChunk(msg);
        }

        private void OnStaticCatalogSyncComplete(MsgStaticCatalogSyncComplete msg)
        {
            var cache = StaticCatalogCache.EnsureInstance();
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
            var cache = StaticCatalogCache.EnsureInstance();
            cache?.ApplySnapshot(msg?.Snapshot);
            HydrateStaticCatalogStore(cache);
        }

        public void UseStaticCatalogStoreHydrator(StaticCatalogStoreHydrator hydrator)
        {
            _staticCatalogStoreHydrator = hydrator;
            HydrateStaticCatalogStore(StaticCatalogCache.Instance);
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
            GameChatCache.Instance?.Clear();
            GameStateCache.Instance?.ApplyGameInit(msg);
            RoomCache.Instance?.Clear();
            TransitionTo(AppState.Game);
        }

        private static void ClearLobbyRoomCache()
        {
            var roomCache = RoomCache.Instance;
            roomCache?.Clear();
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
