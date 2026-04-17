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

        public DebugPanelContext(SharedState state)
        {
            _state = state ?? new SharedState();
        }

        public SharedState State => _state;
        public string SceneName => SceneManager.GetActiveScene().name;
        public AppState? CurrentAppState => AppManager.Instance != null ? AppManager.Instance.State : null;
        public SessionManager Session => SessionManager.Instance;
        public RoomCache Room => RoomCache.Instance;
        public GameStateCache GameState => GameStateCache.Instance;
        public ClientRuntimeConfigCache RuntimeConfig => ClientRuntimeConfigCache.Instance;
        public MessageLogger Logger => MessageLogger.Instance;
        public bool IsConnected => NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
        public bool IsConnecting => NetworkManager.Instance != null && NetworkManager.Instance.IsConnecting;

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
    }
}
#endif
