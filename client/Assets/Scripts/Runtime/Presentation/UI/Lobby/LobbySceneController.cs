using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Service;
using UnityEngine;

namespace Panoptes.Presentation.UI.Lobby
{
    public sealed class LobbySceneController : MonoBehaviour
    {
        private GameObject _lobbyPanel;
        private GameObject _roomPanel;
        private LobbyPanelController _lobbyPanelController;
        private RoomPanelController _roomPanelController;
        private RoomCache _cache;

        private void Awake()
        {
            _cache = RoomCache.Instance;
            _lobbyPanel = transform.Find("LobbyPanel")?.gameObject;
            _roomPanel = transform.Find("RoomPanel")?.gameObject;
            _lobbyPanelController = _lobbyPanel != null ? _lobbyPanel.GetComponent<LobbyPanelController>() : null;
            _roomPanelController = _roomPanel != null ? _roomPanel.GetComponent<RoomPanelController>() : null;

            if (_cache == null)
            {
                return;
            }

            _cache.OnRoomCreated += OnRoomCreated;
            _cache.OnRoomStateChanged += OnRoomState;
            _cache.OnGameStarting += OnGameStarting;
            _cache.OnPlayerKicked += OnPlayerKicked;
            _cache.OnLobbyError += OnLobbyError;
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
            SetRoomVisible(false);
        }

        public void ShowRoomPanel()
        {
            SetRoomVisible(true);
        }

        private void OnRoomCreated(string roomId, string roomCode)
        {
            _lobbyPanelController?.HandleRoomCreated(roomId, roomCode);
        }

        private void OnRoomState()
        {
            _lobbyPanelController?.HandleRoomStateReceived();
            ShowRoomPanel();
        }

        private void OnGameStarting(int countdown)
        {
            ShowRoomPanel();
            _roomPanelController?.HandleGameStarting(countdown);
        }

        private void OnLobbyError(string code)
        {
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
            var selfPlayerId = SessionManager.Instance != null ? SessionManager.Instance.PlayerID : string.Empty;
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
            SetRoomVisible(_cache != null && !string.IsNullOrWhiteSpace(_cache.RoomID));
        }

        private void SetRoomVisible(bool visible)
        {
            if (_roomPanel != null)
            {
                _roomPanel.SetActive(visible);
            }

            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(!visible);
            }
        }
    }
}
