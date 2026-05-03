using System.Collections;
using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.Lobby
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
        [SerializeField] private bool forceEnableAddBotInClient = false;

        private LobbyService _lobbySvc;
        private RoomCache _cache;
        private ClientRuntimeConfigCache _runtimeConfig;
        private SessionManager _sessionManager;
        private ConfirmDialog _confirmDialog;
        private ErrorToast _errorToast;
        private Coroutine _countdownCoroutine;
        private Coroutine _statusResetCoroutine;
        private TextMeshProUGUI _readyButtonText;
        private LobbySceneController _sceneController;
        private bool _isWaitingForGameInit;

        [Inject]
        public void Construct(
            LobbyService lobbyService,
            RoomCache roomCache,
            ClientRuntimeConfigCache runtimeConfig,
            SessionManager sessionManager,
            ConfirmDialog confirmDialog,
            ErrorToast errorToast,
            LobbySceneController sceneController)
        {
            _lobbySvc = lobbyService;
            _cache = roomCache;
            _runtimeConfig = runtimeConfig;
            _sessionManager = sessionManager;
            _confirmDialog = confirmDialog;
            _errorToast = errorToast;
            _sceneController = sceneController;
            SubscribeStores();
        }

        private void Awake()
        {
            _readyButtonText = readyButton != null ? readyButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;

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

        public void HandleGameStarting(int countdown)
        {
            _isWaitingForGameInit = false;
            StopCountdown();
            _countdownCoroutine = StartCoroutine(CountdownCoroutine(countdown));
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

        public void HandleLobbyError(string code)
        {
            if (code == "room_dissolved")
            {
                StopCountdown();
                _isWaitingForGameInit = false;
                _cache?.Clear();
                return;
            }

            ShowToast(MapLobbyError(code), false);

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

            ShowConfirmation(
                "开始游戏",
                "确认以当前准备状态开始游戏吗？",
                () => _lobbySvc.StartGame());
        }

        private void OnClickLeave()
        {
            var message = _cache != null && _cache.IsHost
                ? "你是房主，离开后当前房间会解散。确认离开吗？"
                : "确认离开当前房间吗？";

            ShowConfirmation(
                "离开房间",
                message,
                () =>
                {
                    StopCountdown();
                    _isWaitingForGameInit = false;
                    _lobbySvc.LeaveRoom();
                    _cache?.Clear();
                    _sceneController?.ShowLobbyPanel();
                });
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

            var myPlayerId = _sessionManager != null ? _sessionManager.PlayerID : string.Empty;
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

        public void HandlePlayerKicked(string username)
        {
            StopCountdown();
            _isWaitingForGameInit = false;
            StopStatusReset();
            ShowToast($"{(string.IsNullOrWhiteSpace(username) ? "你" : username)}已被移出房间", false);
            if (statusText != null)
            {
                statusText.text = $"{(string.IsNullOrWhiteSpace(username) ? "你" : username)}已被移出房间";
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

        private bool ShouldShowKickButton(RoomPlayerDto player)
        {
            if (player == null || _cache == null || !_cache.IsHost)
            {
                return false;
            }

            if (_countdownCoroutine != null || _isWaitingForGameInit)
            {
                return false;
            }

            var selfPlayerId = _sessionManager != null ? _sessionManager.PlayerID : string.Empty;
            return !string.IsNullOrWhiteSpace(player.PlayerId) && player.PlayerId != selfPlayerId;
        }

        private void OnClickKickPlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            var username = ResolvePlayerDisplayName(playerId);
            var message = string.IsNullOrWhiteSpace(username)
                ? "确认将这名玩家移出房间吗？"
                : $"确认将 {username} 移出房间吗？";

            ShowConfirmation(
                "移出玩家",
                message,
                () => _lobbySvc.KickPlayer(playerId));
        }

        // 房间里的敏感操作都先走这里，避免开始游戏/离开房间/踢人散落着各写一套确认逻辑。
        private void ShowConfirmation(string title, string message, System.Action onConfirm)
        {
            if (_confirmDialog != null)
            {
                _confirmDialog.Show(title, message, onConfirm, null);
                return;
            }

            Debug.LogWarning($"[RoomPanelController] ConfirmDialog is unavailable for action: {title}");
            ShowToast("确认面板未就绪，请稍后重试", false);
        }

        // 房间面板只负责把事件转成提示，不直接关心 toast 是通过 prefab 还是运行时补 UI 出来的。
        private void ShowToast(string message, bool success)
        {
            if (_errorToast != null)
            {
                _errorToast.Show(message, success);
                return;
            }

            if (success)
            {
                Debug.Log(message);
                return;
            }

            Debug.LogWarning(message);
        }

        private string ResolvePlayerDisplayName(string playerId)
        {
            if (_cache == null || string.IsNullOrWhiteSpace(playerId))
            {
                return string.Empty;
            }

            foreach (var player in _cache.Players)
            {
                if (player != null && player.PlayerId == playerId)
                {
                    return player.Username ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private void SubscribeStores()
        {
            if (_cache != null)
            {
                _cache.OnRoomStateChanged -= RefreshUI;
                _cache.OnRoomStateChanged += RefreshUI;
            }

            if (_runtimeConfig != null)
            {
                _runtimeConfig.OnConfigChanged -= RefreshUI;
                _runtimeConfig.OnConfigChanged += RefreshUI;
            }
        }
    }
}
