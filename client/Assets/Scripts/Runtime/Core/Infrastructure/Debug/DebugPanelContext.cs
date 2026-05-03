#if UNITY_EDITOR || DEVELOPMENT_BUILD || PANOPTES_DEBUG_PANEL
using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using UnityEngine.SceneManagement;

namespace Panoptes.DebugTools
{
    public sealed class DebugPanelContext
    {
        public sealed class SharedState
        {
            public string RawMessageType = "MsgSubmitTurn";
            public string RawPayloadJson = "{}";
            public string RawStatus = string.Empty;
            public bool RawStatusIsError;
            public string RequestedTabId = string.Empty;
        }

        private readonly SharedState _state;
        private AppManager _appManager;
        private SessionManager _sessionManager;
        private RoomCache _roomCache;
        private GameStateCache _gameStateCache;
        private ClientRuntimeConfigCache _runtimeConfigCache;
        private NetworkManager _networkManager;

        public DebugPanelContext(SharedState state)
        {
            _state = state ?? new SharedState();
        }

        public SharedState State => _state;
        public string SceneName => SceneManager.GetActiveScene().name;
        public AppState? CurrentAppState => ResolveAppManager() != null ? ResolveAppManager().State : null;
        public SessionManager Session => ResolveSessionManager();
        public RoomCache Room => ResolveRoomCache();
        public GameStateCache GameState => ResolveGameStateCache();
        public ClientRuntimeConfigCache RuntimeConfig => ResolveRuntimeConfigCache();
        public MessageLogger Logger => MessageLogger.Instance;
        public bool IsConnected => ResolveNetworkManager() != null && ResolveNetworkManager().IsConnected;
        public bool IsConnecting => ResolveNetworkManager() != null && ResolveNetworkManager().IsConnecting;

        public void UseRuntimeServices(
            AppManager appManager,
            SessionManager sessionManager,
            RoomCache roomCache,
            GameStateCache gameStateCache,
            ClientRuntimeConfigCache runtimeConfigCache,
            NetworkManager networkManager)
        {
            _appManager = appManager;
            _sessionManager = sessionManager;
            _roomCache = roomCache;
            _gameStateCache = gameStateCache;
            _runtimeConfigCache = runtimeConfigCache;
            _networkManager = networkManager;
        }

        public void OpenRawSenderDraft(string messageType, string payloadJson, string status)
        {
            _state.RawMessageType = string.IsNullOrWhiteSpace(messageType) ? "MsgSubmitTurn" : messageType.Trim();
            _state.RawPayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
            _state.RawStatus = status ?? string.Empty;
            _state.RawStatusIsError = false;
            _state.RequestedTabId = "raw-sender";
        }

        public void SetRawSenderStatus(string status, bool isError)
        {
            _state.RawStatus = status ?? string.Empty;
            _state.RawStatusIsError = isError;
        }

        private AppManager ResolveAppManager() => _appManager != null ? _appManager : AppManager.Instance;
        private SessionManager ResolveSessionManager() => _sessionManager != null ? _sessionManager : SessionManager.Instance;
        private RoomCache ResolveRoomCache() => _roomCache != null ? _roomCache : RoomCache.Instance;
        private GameStateCache ResolveGameStateCache() => _gameStateCache != null ? _gameStateCache : GameStateCache.Instance;
        private ClientRuntimeConfigCache ResolveRuntimeConfigCache() => _runtimeConfigCache != null ? _runtimeConfigCache : ClientRuntimeConfigCache.Instance;
        private NetworkManager ResolveNetworkManager() => _networkManager != null ? _networkManager : NetworkManager.Instance;
    }
}
#endif
