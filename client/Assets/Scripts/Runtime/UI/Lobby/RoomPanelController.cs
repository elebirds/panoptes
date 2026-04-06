using System.Collections;
using Panoptes.Protocol.V1;
using Panoptes.Runtime.App;
using Panoptes.Runtime.Cache;
using Panoptes.Runtime.Network;
using Panoptes.Runtime.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Runtime.UI.Lobby
{
    public sealed class RoomPanelController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Transform playerSlotContainer;
        [SerializeField] private GameObject playerSlotPrefab;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private TextMeshProUGUI statusText;

        private LobbyService _lobbySvc;
        private RoomCache _cache;
        private Coroutine _countdownCoroutine;
        private Coroutine _statusResetCoroutine;
        private TextMeshProUGUI _readyButtonText;
        private GameObject _lobbyPanel;

        private void Awake()
        {
            _lobbySvc = new LobbyService();
            _cache = RoomCache.Instance;
            _readyButtonText = readyButton != null ? readyButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            _lobbyPanel = transform.parent != null ? transform.parent.Find("LobbyPanel")?.gameObject : null;

            if (MessageDispatcher.Instance != null)
            {
                MessageDispatcher.Instance.Register<MsgRoomState>("MsgRoomState", OnRoomState);
                MessageDispatcher.Instance.Register<MsgGameStarting>("MsgGameStarting", OnGameStarting);
                MessageDispatcher.Instance.Register<MsgLobbyError>("MsgLobbyError", OnLobbyError);
            }

            if (_cache != null)
            {
                _cache.OnRoomStateChanged += RefreshUI;
            }

            readyButton?.onClick.AddListener(OnClickReady);
            leaveButton?.onClick.AddListener(OnClickLeave);
        }

        private void Start()
        {
            RefreshUI();
        }

        private void OnDestroy()
        {
            if (MessageDispatcher.Instance != null)
            {
                MessageDispatcher.Instance.Unregister("MsgRoomState");
                MessageDispatcher.Instance.Unregister("MsgGameStarting");
                MessageDispatcher.Instance.Unregister("MsgLobbyError");
            }

            if (_cache != null)
            {
                _cache.OnRoomStateChanged -= RefreshUI;
            }

            readyButton?.onClick.RemoveListener(OnClickReady);
            leaveButton?.onClick.RemoveListener(OnClickLeave);

            StopCountdown();
            StopStatusReset();
        }

        private void OnRoomState(MsgRoomState msg)
        {
            RoomCache.Instance?.Apply(msg);

            if (!gameObject.activeSelf)
            {
                SetRoomPanelVisible(true);
            }
        }

        private void RefreshUI()
        {
            if (_cache == null)
            {
                return;
            }

            if (roomNameText != null)
            {
                roomNameText.text = _cache.RoomName;
            }

            if (roomCodeText != null)
            {
                roomCodeText.text = $"邀请码：{_cache.RoomCode}";
            }

            if (playerCountText != null)
            {
                playerCountText.text = $"{_cache.Players.Count} / {_cache.MaxPlayers}";
            }

            RebuildPlayerSlots();
            RefreshReadyButton();

            if (_countdownCoroutine == null)
            {
                SetButtonsInteractable(true);
            }

            switch (_cache.Status)
            {
                case "waiting":
                    if (statusText != null)
                    {
                        statusText.text = "等待玩家准备...";
                    }
                    break;
                case "ready":
                    if (statusText != null)
                    {
                        statusText.text = "全员已准备！";
                    }
                    break;
            }
        }

        private void OnGameStarting(MsgGameStarting msg)
        {
            StopCountdown();
            _countdownCoroutine = StartCoroutine(CountdownCoroutine(msg != null ? msg.Countdown : 0));
        }

        private IEnumerator CountdownCoroutine(int seconds)
        {
            SetButtonsInteractable(false);

            if (seconds <= 0)
            {
                if (AppManager.Instance != null)
                {
                    AppManager.Instance.TransitionTo(AppState.Game);
                }
                yield break;
            }

            for (var remaining = seconds; remaining > 0; remaining--)
            {
                if (statusText != null)
                {
                    statusText.text = $"游戏即将开始 {remaining}...";
                }

                yield return new WaitForSeconds(1f);
            }

            _countdownCoroutine = null;
            if (AppManager.Instance != null)
            {
                AppManager.Instance.TransitionTo(AppState.Game);
            }
        }

        private void OnLobbyError(MsgLobbyError msg)
        {
            var code = msg != null ? msg.Code : string.Empty;
            if (code == "room_dissolved")
            {
                StopCountdown();
                RoomCache.Instance?.Clear();
                SetRoomPanelVisible(false);
                return;
            }

            if (statusText != null)
            {
                statusText.text = MapLobbyError(code);
            }

            StopStatusReset();
            _statusResetCoroutine = StartCoroutine(RestoreStatusAfterDelay());
        }

        private IEnumerator RestoreStatusAfterDelay()
        {
            yield return new WaitForSeconds(3f);
            _statusResetCoroutine = null;
            RefreshUI();
        }

        private void OnClickReady()
        {
            _lobbySvc.ReadyUp();
        }

        private void OnClickLeave()
        {
            StopCountdown();
            _lobbySvc.LeaveRoom();
            RoomCache.Instance?.Clear();
            SetRoomPanelVisible(false);
        }

        private void RebuildPlayerSlots()
        {
            if (playerSlotContainer == null)
            {
                return;
            }

            for (var i = playerSlotContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(playerSlotContainer.GetChild(i).gameObject);
            }

            if (playerSlotPrefab == null)
            {
                return;
            }

            foreach (var player in _cache.Players)
            {
                var slotObject = Instantiate(playerSlotPrefab, playerSlotContainer, false);
                var slotView = slotObject.GetComponent<PlayerSlotView>();
                if (slotView != null)
                {
                    slotView.Setup(player, player.IsHost);
                }
            }
        }

        private void RefreshReadyButton()
        {
            if (_readyButtonText == null)
            {
                return;
            }

            var myPlayerId = SessionManager.Instance != null ? SessionManager.Instance.PlayerID : string.Empty;
            var isReady = false;
            foreach (var player in _cache.Players)
            {
                if (player.PlayerId == myPlayerId)
                {
                    isReady = player.IsReady;
                    break;
                }
            }

            _readyButtonText.text = isReady ? "取消准备" : "准备";
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (readyButton != null)
            {
                readyButton.interactable = interactable;
            }

            if (leaveButton != null)
            {
                leaveButton.interactable = interactable;
            }
        }

        private void StopCountdown()
        {
            if (_countdownCoroutine == null)
            {
                return;
            }

            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }

        private void StopStatusReset()
        {
            if (_statusResetCoroutine == null)
            {
                return;
            }

            StopCoroutine(_statusResetCoroutine);
            _statusResetCoroutine = null;
        }

        private void SetRoomPanelVisible(bool visible)
        {
            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(!visible);
            }

            gameObject.SetActive(visible);
        }

        private static string MapLobbyError(string code)
        {
            return code switch
            {
                "room_full" => "房间已满",
                "room_not_found" => "房间不存在",
                "already_in_room" => "你已在房间中",
                "invalid_status" => "房间状态不允许此操作",
                "room_dissolved" => "房主已离开，房间解散",
                _ => "操作失败，请重试"
            };
        }
    }
}
