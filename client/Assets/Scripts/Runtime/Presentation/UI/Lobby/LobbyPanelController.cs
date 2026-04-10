using System.Collections;
using Panoptes.Core.Infrastructure.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Lobby
{
    public sealed class LobbyPanelController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_InputField roomNameField;
        [SerializeField] private TMP_Dropdown maxPlayersDropdown;
        [SerializeField] private Button createButton;
        [SerializeField] private TMP_InputField roomCodeField;
        [SerializeField] private Button joinButton;
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private TextMeshProUGUI usernameText;

        private LobbyService _lobbySvc;
        private Coroutine _createTimeoutCoroutine;
        private Coroutine _joinTimeoutCoroutine;

        private void Awake()
        {
            _lobbySvc = new LobbyService();

            createButton?.onClick.AddListener(OnClickCreateRoom);
            joinButton?.onClick.AddListener(OnClickJoinRoom);
        }

        private void Start()
        {
            if (usernameText != null)
            {
                usernameText.text = SessionManager.Instance != null
                    ? SessionManager.Instance.Username ?? string.Empty
                    : string.Empty;
            }

            HideError();
        }

        private void OnDestroy()
        {
            createButton?.onClick.RemoveListener(OnClickCreateRoom);
            joinButton?.onClick.RemoveListener(OnClickJoinRoom);

            StopTimeoutCoroutine(ref _createTimeoutCoroutine);
            StopTimeoutCoroutine(ref _joinTimeoutCoroutine);
        }

        public void ShowError(string message)
        {
            if (errorText == null)
            {
                return;
            }

            errorText.text = message;
            errorText.gameObject.SetActive(true);
        }

        public void HandleRoomCreated(string roomId, string roomCode)
        {
            Debug.Log($"[LobbyPanel] Room created: {roomId} / {roomCode}");
        }

        public void HandleRoomStateReceived()
        {
            StopTimeoutCoroutine(ref _createTimeoutCoroutine);
            StopTimeoutCoroutine(ref _joinTimeoutCoroutine);

            if (createButton != null)
            {
                createButton.interactable = true;
            }

            if (joinButton != null)
            {
                joinButton.interactable = true;
            }

            HideError();
        }

        public void HandleLobbyError(string code)
        {
            StopTimeoutCoroutine(ref _createTimeoutCoroutine);
            StopTimeoutCoroutine(ref _joinTimeoutCoroutine);

            if (createButton != null)
            {
                createButton.interactable = true;
            }

            if (joinButton != null)
            {
                joinButton.interactable = true;
            }

            ShowError(MapLobbyError(code));
        }

        private void OnClickCreateRoom()
        {
            HideError();

            var roomName = roomNameField != null ? roomNameField.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(roomName))
            {
                ShowError("请输入房间名");
                return;
            }

            if (createButton != null)
            {
                createButton.interactable = false;
            }

            _lobbySvc.CreateRoom(roomName, ResolveMaxPlayers());
            RestartTimeout(ref _createTimeoutCoroutine, createButton);
        }

        private void OnClickJoinRoom()
        {
            HideError();

            var roomCode = roomCodeField != null ? roomCodeField.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length != 6)
            {
                ShowError("请输入6位邀请码");
                return;
            }

            if (joinButton != null)
            {
                joinButton.interactable = false;
            }

            _lobbySvc.JoinRoom(roomCode.ToUpperInvariant());
            RestartTimeout(ref _joinTimeoutCoroutine, joinButton);
        }

        private int ResolveMaxPlayers()
        {
            if (maxPlayersDropdown == null || maxPlayersDropdown.options.Count == 0)
            {
                return 2;
            }

            var optionText = maxPlayersDropdown.options[maxPlayersDropdown.value].text;
            return int.TryParse(optionText, out var parsed) ? parsed : 2;
        }

        private void RestartTimeout(ref Coroutine coroutine, Button button)
        {
            StopTimeoutCoroutine(ref coroutine);
            coroutine = StartCoroutine(EnableButtonAfterDelay(button, 2f));
        }

        private IEnumerator EnableButtonAfterDelay(Button button, float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            if (button != null)
            {
                button.interactable = true;
            }
        }

        private void StopTimeoutCoroutine(ref Coroutine coroutine)
        {
            if (coroutine == null)
            {
                return;
            }

            StopCoroutine(coroutine);
            coroutine = null;
        }

        private void HideError()
        {
            if (errorText == null)
            {
                return;
            }

            errorText.text = string.Empty;
            errorText.gameObject.SetActive(false);
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
                "invalid_player_count" => "玩家数量不合法",
                "player_not_found" => "玩家不存在",
                _ => "操作失败，请重试"
            };
        }
    }
}
