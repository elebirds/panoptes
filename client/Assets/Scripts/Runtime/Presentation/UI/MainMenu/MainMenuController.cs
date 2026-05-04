using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.UI.Lobby;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Panoptes.Presentation.UI.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject roomPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject lobbyBackButtonRoot;
        [SerializeField] private LobbySceneController lobbySceneController;

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button lobbyBackButton;
        [SerializeField] private Button settingsBackButton;

        private bool _lastLoggedIn;

        private void Awake()
        {
            startButton?.onClick.AddListener(ShowLobbyEntry);
            settingsButton?.onClick.AddListener(OpenSettings);
            quitButton?.onClick.AddListener(QuitGame);
            lobbyBackButton?.onClick.AddListener(ShowMainMenu);
            settingsBackButton?.onClick.AddListener(ShowMainMenu);
        }

        private void Start()
        {
            _lastLoggedIn = IsLoggedIn();
            ApplyLoginState(_lastLoggedIn);

            if (!_lastLoggedIn)
            {
                ShowMainMenu();
                return;
            }

            if (HasActiveRoom())
            {
                ShowRoomEntry();
                return;
            }

            ShowMainMenu();
        }

        private void OnDestroy()
        {
            startButton?.onClick.RemoveListener(ShowLobbyEntry);
            settingsButton?.onClick.RemoveListener(OpenSettings);
            quitButton?.onClick.RemoveListener(QuitGame);
            lobbyBackButton?.onClick.RemoveListener(ShowMainMenu);
            settingsBackButton?.onClick.RemoveListener(ShowMainMenu);
        }

        private void Update()
        {
            var loggedIn = IsLoggedIn();
            if (loggedIn == _lastLoggedIn)
            {
                return;
            }

            _lastLoggedIn = loggedIn;
            ApplyLoginState(loggedIn);

            if (!loggedIn)
            {
                ShowMainMenu();
                return;
            }

            if (HasActiveRoom())
            {
                ShowRoomEntry();
                return;
            }

            ShowMainMenu();
        }

        public void ShowMainMenu()
        {
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(true);
            }

            if (lobbyBackButtonRoot != null)
            {
                lobbyBackButtonRoot.SetActive(false);
            }

            lobbySceneController?.HidePanels();
            SetPanelActive(lobbyPanel, false);
            SetPanelActive(roomPanel, false);
            SetPanelActive(settingsPanel, false);
        }

        public void ShowLobbyEntry()
        {
            AppManager.Instance?.TransitionTo(AppState.Lobby);

            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(false);
            }

            if (lobbyBackButtonRoot != null)
            {
                lobbyBackButtonRoot.SetActive(true);
            }

            lobbySceneController?.ShowLobbyPanel();
        }

        public void ShowRoomEntry()
        {
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(false);
            }

            if (lobbyBackButtonRoot != null)
            {
                lobbyBackButtonRoot.SetActive(true);
            }

            lobbySceneController?.ShowRoomPanel();
        }

        private void OpenSettings()
        {
            if (!IsLoggedIn())
            {
                return;
            }

            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(false);
            }

            if (lobbyBackButtonRoot != null)
            {
                lobbyBackButtonRoot.SetActive(false);
            }

            lobbySceneController?.HidePanels();
            SetPanelActive(lobbyPanel, false);
            SetPanelActive(roomPanel, false);
            SetPanelActive(settingsPanel, true);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static bool HasActiveRoom()
        {
            return RoomCache.Instance != null && !string.IsNullOrWhiteSpace(RoomCache.Instance.RoomID);
        }

        private void ApplyLoginState(bool loggedIn)
        {
            SetPanelActive(loginPanel, !loggedIn);

            if (startButton != null)
            {
                startButton.interactable = loggedIn;
            }

            if (settingsButton != null)
            {
                settingsButton.interactable = loggedIn;
            }

            if (quitButton != null)
            {
                quitButton.interactable = true;
            }
        }

        private static bool IsLoggedIn()
        {
            return SessionManager.Instance != null && SessionManager.Instance.IsLoggedIn;
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }
    }
}
