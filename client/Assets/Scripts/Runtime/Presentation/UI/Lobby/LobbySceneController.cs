using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Service;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.UI.Lobby
{
    public sealed class LobbySceneController : MonoBehaviour
    {
        private GameObject _lobbyPanel;
        private GameObject _roomPanel;
        private LobbyPanelController _lobbyPanelController;
        private RoomPanelController _roomPanelController;
        private RoomCache _cache;
        private SessionManager _sessionManager;

        [Inject]
        public void Construct(RoomCache roomCache, SessionManager sessionManager)
        {
            _cache = roomCache;
            _sessionManager = sessionManager;
            SubscribeStore();
        }

        private void Awake()
        {
            _lobbyPanel = transform.Find("LobbyPanel")?.gameObject;
            _roomPanel = transform.Find("RoomPanel")?.gameObject;
            _lobbyPanelController = _lobbyPanel != null ? _lobbyPanel.GetComponent<LobbyPanelController>() : null;
            _roomPanelController = _roomPanel != null ? _roomPanel.GetComponent<RoomPanelController>() : null;
        }

        private void Start()
        {
            SyncPanelVisibility();
        }

        private void OnDestroy()
        {
            if (_cache == null)
            {
                return;
            }

            _cache.OnRoomCreated -= OnRoomCreated;
            _cache.OnRoomStateChanged -= OnRoomState;
            _cache.OnGameStarting -= OnGameStarting;
            _cache.OnPlayerKicked -= OnPlayerKicked;
            _cache.OnLobbyError -= OnLobbyError;
        }

        public void ShowLobbyPanel()
        {
            if (!CanShowLobbyPanels())
            {
                HidePanels();
                return;
            }

            SetRoomVisible(false);
        }

        public void ShowRoomPanel()
        {
            if (!CanShowLobbyPanels())
            {
                HidePanels();
                return;
            }

            SetRoomVisible(true);
        }

        public void HidePanels()
        {
            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(false);
            }

            if (_roomPanel != null)
            {
                _roomPanel.SetActive(false);
            }
        }

        private void OnRoomCreated(string roomId, string roomCode)
        {
            if (!CanShowLobbyPanels())
            {
                HidePanels();
                return;
            }

            _lobbyPanelController?.HandleRoomCreated(roomId, roomCode);
        }

        private void OnRoomState()
        {
            if (!CanShowLobbyPanels())
            {
                HidePanels();
                return;
            }

            _lobbyPanelController?.HandleRoomStateReceived();
            ShowRoomPanel();
        }

        private void OnGameStarting(int countdown)
        {
            if (!CanShowLobbyPanels())
            {
                HidePanels();
                return;
            }

            ShowRoomPanel();
            _roomPanelController?.HandleGameStarting(countdown);
        }

        private void OnLobbyError(string code)
        {
            if (!CanShowLobbyPanels())
            {
                HidePanels();
                return;
            }

            if (_roomPanel != null && _roomPanel.activeSelf)
            {
                _roomPanelController?.HandleLobbyError(code);
                if (code == "room_dissolved")
                {
                    ShowLobbyPanel();
                    _lobbyPanelController?.ShowError("房主已离开，房间解散");
                }

                return;
            }

            _lobbyPanelController?.HandleLobbyError(code);
        }

        private void OnPlayerKicked(string playerId, string username)
        {
            var selfPlayerId = _sessionManager != null ? _sessionManager.PlayerID : string.Empty;
            if (playerId != selfPlayerId)
            {
                return;
            }

            _roomPanelController?.HandlePlayerKicked(username);
            _cache?.Clear();
            ShowLobbyPanel();
            _lobbyPanelController?.ShowError("你已被移出房间");
        }

        private void SyncPanelVisibility()
        {
            if (!CanShowLobbyPanels())
            {
                HidePanels();
                return;
            }

            SetRoomVisible(_cache != null && !string.IsNullOrWhiteSpace(_cache.RoomID));
        }

        private void SetRoomVisible(bool visible)
        {
            if (!CanShowLobbyPanels())
            {
                HidePanels();
                return;
            }

            if (_roomPanel != null)
            {
                _roomPanel.SetActive(visible);
            }

            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(!visible);
            }
        }

        private bool CanShowLobbyPanels()
        {
            return _sessionManager != null && _sessionManager.IsLoggedIn;
        }

        private void SubscribeStore()
        {
            if (_cache == null)
            {
                return;
            }

            _cache.OnRoomCreated -= OnRoomCreated;
            _cache.OnRoomStateChanged -= OnRoomState;
            _cache.OnGameStarting -= OnGameStarting;
            _cache.OnPlayerKicked -= OnPlayerKicked;
            _cache.OnLobbyError -= OnLobbyError;
            _cache.OnRoomCreated += OnRoomCreated;
            _cache.OnRoomStateChanged += OnRoomState;
            _cache.OnGameStarting += OnGameStarting;
            _cache.OnPlayerKicked += OnPlayerKicked;
            _cache.OnLobbyError += OnLobbyError;
        }
    }
}
