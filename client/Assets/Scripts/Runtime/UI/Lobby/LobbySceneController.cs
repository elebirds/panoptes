using Panoptes.Protocol.V1;
using Panoptes.Runtime.Cache;
using Panoptes.Runtime.Network;
using Panoptes.Runtime.Service;
using UnityEngine;

namespace Panoptes.Runtime.UI.Lobby
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

            if (MessageDispatcher.Instance == null)
            {
                return;
            }

            MessageDispatcher.Instance.Register<MsgRoomCreated>("MsgRoomCreated", OnRoomCreated);
            MessageDispatcher.Instance.Register<MsgRoomState>("MsgRoomState", OnRoomState);
            MessageDispatcher.Instance.Register<MsgGameStarting>("MsgGameStarting", OnGameStarting);
            MessageDispatcher.Instance.Register<MsgPlayerKicked>("MsgPlayerKicked", OnPlayerKicked);
            MessageDispatcher.Instance.Register<MsgLobbyError>("MsgLobbyError", OnLobbyError);
        }

        private void Start()
        {
            SyncPanelVisibility();
        }

        private void OnDestroy()
        {
            if (MessageDispatcher.Instance == null)
            {
                return;
            }

            MessageDispatcher.Instance.Unregister("MsgRoomCreated");
            MessageDispatcher.Instance.Unregister("MsgRoomState");
            MessageDispatcher.Instance.Unregister("MsgGameStarting");
            MessageDispatcher.Instance.Unregister("MsgPlayerKicked");
            MessageDispatcher.Instance.Unregister("MsgLobbyError");
        }

        public void ShowLobbyPanel()
        {
            SetRoomVisible(false);
        }

        public void ShowRoomPanel()
        {
            SetRoomVisible(true);
        }

        private void OnRoomCreated(MsgRoomCreated msg)
        {
            _lobbyPanelController?.HandleRoomCreated(msg);
        }

        private void OnRoomState(MsgRoomState msg)
        {
            _cache?.Apply(msg);
            _lobbyPanelController?.HandleRoomStateReceived();
            ShowRoomPanel();
        }

        private void OnGameStarting(MsgGameStarting msg)
        {
            ShowRoomPanel();
            _roomPanelController?.HandleGameStarting(msg);
        }

        private void OnLobbyError(MsgLobbyError msg)
        {
            var code = msg != null ? msg.Code : string.Empty;
            if (_roomPanel != null && _roomPanel.activeSelf)
            {
                _roomPanelController?.HandleLobbyError(msg);
                if (code == "room_dissolved")
                {
                    ShowLobbyPanel();
                    _lobbyPanelController?.ShowError("房主已离开，房间解散");
                }

                return;
            }

            _lobbyPanelController?.HandleLobbyError(msg);
        }

        private void OnPlayerKicked(MsgPlayerKicked msg)
        {
            var selfPlayerId = SessionManager.Instance != null ? SessionManager.Instance.PlayerID : string.Empty;
            if (msg == null || msg.PlayerId != selfPlayerId)
            {
                return;
            }

            _roomPanelController?.HandlePlayerKicked(msg);
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
