using System.Collections;
using Panoptes.Protocol.V1;
using Panoptes.Runtime.App;
using Panoptes.Runtime.Cache;
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
        [SerializeField] private Button addBotButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Local Test")]
        [SerializeField] private bool forceEnableAddBotInClient = true;

        private LobbyService _lobbySvc;
        private RoomCache _cache;
        private ClientRuntimeConfigCache _runtimeConfig;
        private Coroutine _countdownCoroutine;
        private Coroutine _statusResetCoroutine;
        private TextMeshProUGUI _readyButtonText;
        private LobbySceneController _sceneController;
        private bool _isWaitingForGameInit;

        private void Awake()
        {
            _lobbySvc = new LobbyService();
            _cache = RoomCache.Instance;
            _runtimeConfig = ClientRuntimeConfigCache.Instance;
            _readyButtonText = readyButton != null ? readyButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            _sceneController = transform.parent != null ? transform.parent.GetComponent<LobbySceneController>() : null;

            if (_cache != null)
            {
                _cache.OnRoomStateChanged += RefreshUI;
            }

            if (_runtimeConfig != null)
            {
                _runtimeConfig.OnConfigChanged += RefreshUI;
            }

            addBotButton?.onClick.AddListener(OnClickAddBot);
            startGameButton?.onClick.AddListener(OnClickStartGame);
            readyButton?.onClick.AddListener(OnClickReady);
            leaveButton?.onClick.AddListener(OnClickLeave);
        }

        private void Start()
        {
            RefreshUI();
        }

        private void OnDestroy()
        {
            if (_cache != null)
            {
                _cache.OnRoomStateChanged -= RefreshUI;
            }

            if (_runtimeConfig != null)
            {
                _runtimeConfig.OnConfigChanged -= RefreshUI;
            }

            addBotButton?.onClick.RemoveListener(OnClickAddBot);
            startGameButton?.onClick.RemoveListener(OnClickStartGame);
            readyButton?.onClick.RemoveListener(OnClickReady);
            leaveButton?.onClick.RemoveListener(OnClickLeave);

            StopCountdown();
            StopStatusReset();
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
            RefreshAddBotButton();
            RefreshStartGameButton();

            if (_countdownCoroutine == null)
            {
                SetButtonsInteractable(true);
            }

            switch (_cache.Status)
            {
                case "waiting":
                    if (statusText != null)
                    {
                        statusText.text = _isWaitingForGameInit ? "等待游戏初始化..." : "等待玩家准备...";
                    }
                    break;
                case "ready":
                    if (statusText != null)
                    {
                        statusText.text = _isWaitingForGameInit ? "等待游戏初始化..." : "全员已准备！";
                    }
                    break;
                case "starting":
                    if (statusText != null && !_isWaitingForGameInit)
                    {
                        statusText.text = "游戏即将开始...";
                    }
                    break;
            }
        }

        public void HandleGameStarting(MsgGameStarting msg)
        {
            _isWaitingForGameInit = false;
            StopCountdown();
            _countdownCoroutine = StartCoroutine(CountdownCoroutine(msg != null ? msg.Countdown : 0));
        }

        private IEnumerator CountdownCoroutine(int seconds)
        {
            SetButtonsInteractable(false);

            if (seconds <= 0)
            {
                _countdownCoroutine = null;
                _isWaitingForGameInit = true;
                if (statusText != null)
                {
                    statusText.text = "等待游戏初始化...";
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
            _isWaitingForGameInit = true;
            if (statusText != null)
            {
                statusText.text = "等待游戏初始化...";
            }
        }

        public void HandleLobbyError(MsgLobbyError msg)
        {
            var code = msg != null ? msg.Code : string.Empty;
            if (code == "room_dissolved")
            {
                StopCountdown();
                _isWaitingForGameInit = false;
                RoomCache.Instance?.Clear();
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

        private void OnClickAddBot()
        {
            if (!CanAddBot())
            {
                return;
            }

            _lobbySvc.AddBot();
        }

        private void OnClickStartGame()
        {
            if (!CanStartGame())
            {
                return;
            }

            _lobbySvc.StartGame();
        }

        private void OnClickLeave()
        {
            StopCountdown();
            _isWaitingForGameInit = false;
            _lobbySvc.LeaveRoom();
            RoomCache.Instance?.Clear();
            _sceneController?.ShowLobbyPanel();
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
                    slotView.Setup(player, ShouldShowKickButton(player), OnClickKickPlayer);
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
            if (addBotButton != null)
            {
                addBotButton.interactable = interactable && CanAddBot();
            }

            if (startGameButton != null)
            {
                startGameButton.interactable = interactable && CanStartGame();
            }

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

        public void HandlePlayerKicked(MsgPlayerKicked msg)
        {
            StopCountdown();
            _isWaitingForGameInit = false;
            StopStatusReset();
            if (statusText != null)
            {
                statusText.text = $"{msg?.Username ?? "你"}已被移出房间";
            }
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

        private void RefreshAddBotButton()
        {
            if (addBotButton == null)
            {
                return;
            }

            var visible = _cache != null &&
                          _cache.IsHost &&
                          IsAddBotFeatureEnabled();
            addBotButton.gameObject.SetActive(visible);
            addBotButton.interactable = visible && CanAddBot();
        }

        private void RefreshStartGameButton()
        {
            if (startGameButton == null)
            {
                return;
            }

            var visible = _cache != null && _cache.IsHost;
            startGameButton.gameObject.SetActive(visible);
            startGameButton.interactable = visible && CanStartGame();
        }

        private bool CanAddBot()
        {
            if (_cache == null)
            {
                return false;
            }

            if (!IsAddBotFeatureEnabled() || !_cache.IsHost)
            {
                return false;
            }

            if (_countdownCoroutine != null || _isWaitingForGameInit)
            {
                return false;
            }

            if (_cache.Status != "waiting")
            {
                return false;
            }

            if (_cache.Players.Count >= _cache.MaxPlayers)
            {
                return false;
            }

            return _cache.GetBotCount() < _cache.MaxPlayers - 1;
        }

        private bool IsAddBotFeatureEnabled()
        {
            var serverDevMode = _runtimeConfig != null && _runtimeConfig.DevMode;
            return serverDevMode || forceEnableAddBotInClient;
        }

        private bool CanStartGame()
        {
            if (_cache == null || !_cache.IsHost)
            {
                return false;
            }

            if (_countdownCoroutine != null || _isWaitingForGameInit)
            {
                return false;
            }

            return _cache.Status == "ready";
        }

        private bool ShouldShowKickButton(RoomPlayer player)
        {
            if (player == null || _cache == null || !_cache.IsHost)
            {
                return false;
            }

            if (_countdownCoroutine != null || _isWaitingForGameInit)
            {
                return false;
            }

            var selfPlayerId = SessionManager.Instance != null ? SessionManager.Instance.PlayerID : string.Empty;
            return !string.IsNullOrWhiteSpace(player.PlayerId) && player.PlayerId != selfPlayerId;
        }

        private void OnClickKickPlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            _lobbySvc.KickPlayer(playerId);
        }
    }
}
