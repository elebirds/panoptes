using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.UI.Lobby;
using TMPro;
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
        private bool _loginLayoutApplied;

        private void Awake()
        {
            ApplyEmbeddedLoginLayout();
            startButton?.onClick.AddListener(ShowLobbyEntry);
            settingsButton?.onClick.AddListener(OpenSettings);
            quitButton?.onClick.AddListener(QuitGame);
            lobbyBackButton?.onClick.AddListener(ShowMainMenu);
            settingsBackButton?.onClick.AddListener(ShowMainMenu);
        }

        private void Start()
        {
            ApplyEmbeddedLoginLayout();
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

        private void OnRectTransformDimensionsChange()
        {
            _loginLayoutApplied = false;
            ApplyEmbeddedLoginLayout();
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
            ApplyLoginState(IsLoggedIn());

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
            if (!IsLoggedIn())
            {
                ShowMainMenu();
                return;
            }

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
            SetPanelActive(loginPanel, false);
            SetPanelActive(settingsPanel, false);
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
            SetPanelActive(loginPanel, false);
            SetPanelActive(settingsPanel, false);
        }

        private void OpenSettings()
        {
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(false);
            }

            if (lobbyBackButtonRoot != null)
            {
                lobbyBackButtonRoot.SetActive(false);
            }

            lobbySceneController?.HidePanels();
            SetPanelActive(loginPanel, false);
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
                settingsButton.interactable = true;
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

        private void ApplyEmbeddedLoginLayout()
        {
            if (_loginLayoutApplied || loginPanel == null)
            {
                return;
            }

            var root = loginPanel.transform as RectTransform;
            if (root == null)
            {
                return;
            }

            _loginLayoutApplied = true;

            var canvasSize = ResolveCanvasSize();
            var wideLayout = canvasSize.x >= 1180f;
            var panelWidth = wideLayout
                ? Mathf.Clamp(canvasSize.x * 0.30f, 500f, 580f)
                : Mathf.Clamp(canvasSize.x - 96f, 440f, 560f);
            var panelHeight = Mathf.Clamp(canvasSize.y * 0.48f, 430f, 520f);

            if (wideLayout)
            {
                root.anchorMin = new Vector2(1f, 0.5f);
                root.anchorMax = new Vector2(1f, 0.5f);
                root.pivot = new Vector2(1f, 0.5f);
                root.anchoredPosition = new Vector2(-Mathf.Clamp(canvasSize.x * 0.065f, 88f, 150f), 0f);
            }
            else
            {
                root.anchorMin = new Vector2(0.5f, 0.5f);
                root.anchorMax = new Vector2(0.5f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = new Vector2(0f, -Mathf.Clamp(canvasSize.y * 0.05f, 28f, 70f));
            }

            root.sizeDelta = new Vector2(panelWidth, panelHeight);
            root.localScale = Vector3.one;
            root.localRotation = Quaternion.identity;

            if (loginPanel.TryGetComponent<Image>(out var panelImage))
            {
                panelImage.color = new Color(0.045f, 0.028f, 0.022f, 0.86f);
                panelImage.raycastTarget = true;
            }

            LayoutDecorativeBackground("Bakcground");
            LayoutDecorativeBackground("Background");

            var padding = Mathf.Clamp(panelWidth * 0.09f, 42f, 56f);
            var labelWidth = 96f;
            var inputGap = 18f;
            var inputWidth = Mathf.Max(220f, panelWidth - padding * 2f - labelWidth - inputGap);
            var inputHeight = 52f;
            var titleY = -Mathf.Clamp(panelHeight * 0.09f, 34f, 48f);
            var firstRowY = -Mathf.Clamp(panelHeight * 0.26f, 112f, 136f);
            var rowGap = Mathf.Clamp(panelHeight * 0.17f, 72f, 88f);
            var buttonY = -panelHeight + Mathf.Clamp(panelHeight * 0.18f, 76f, 92f);
            var buttonGap = 22f;
            var buttonWidth = (panelWidth - padding * 2f - buttonGap) * 0.5f;

            LayoutText("UserName", new Vector2(padding, firstRowY), new Vector2(labelWidth, inputHeight), "\u8d26\u53f7", 26f, TextAlignmentOptions.Left);
            LayoutText("Password", new Vector2(padding, firstRowY - rowGap), new Vector2(labelWidth, inputHeight), "\u5bc6\u7801", 26f, TextAlignmentOptions.Left);
            LayoutInput("UserNameInput", new Vector2(padding + labelWidth + inputGap, firstRowY + 4f), new Vector2(inputWidth, inputHeight));
            LayoutInput("PasswordInput", new Vector2(padding + labelWidth + inputGap, firstRowY - rowGap + 4f), new Vector2(inputWidth, inputHeight));
            LayoutButton("LoginButton", new Vector2(padding, buttonY), new Vector2(buttonWidth, 58f), "\u767b\u5f55");
            LayoutButton("RegisterButton", new Vector2(padding + buttonWidth + buttonGap, buttonY), new Vector2(buttonWidth, 58f), "\u6ce8\u518c");

            var firstTitle = loginPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            if (firstTitle != null &&
                firstTitle.transform.parent == loginPanel.transform &&
                firstTitle.name != "UserName" &&
                firstTitle.name != "Password")
            {
                SetRect(firstTitle.transform as RectTransform, new Vector2(padding, titleY), new Vector2(panelWidth - padding * 2f, 58f));
                firstTitle.text = "\u767b\u5f55";
                firstTitle.fontSize = 34f;
                firstTitle.alignment = TextAlignmentOptions.Left;
                firstTitle.color = new Color(1f, 0.86f, 0.58f, 1f);
            }

            return;

            void LayoutText(string name, Vector2 position, Vector2 size, string fallbackText, float fontSize, TextAlignmentOptions alignment)
            {
                var target = FindChild(loginPanel.transform, name);
                if (target == null)
                {
                    return;
                }

                SetRect(target as RectTransform, position, size);
                if (target.TryGetComponent<TextMeshProUGUI>(out var text))
                {
                    if (string.IsNullOrWhiteSpace(text.text))
                    {
                        text.text = fallbackText;
                    }

                    text.fontSize = fontSize;
                    text.alignment = alignment;
                    text.color = new Color(0.95f, 0.88f, 0.76f, 1f);
                    text.raycastTarget = false;
                }
            }

            void LayoutInput(string name, Vector2 position, Vector2 size)
            {
                var target = FindChild(loginPanel.transform, name);
                SetRect(target as RectTransform, position, size);
            }

            void LayoutDecorativeBackground(string name)
            {
                var target = FindDirectChild(loginPanel.transform, name);
                if (target == null)
                {
                    return;
                }

                SetStretch(target as RectTransform, new Vector2(20f, 20f), new Vector2(-20f, -20f));
                if (target.TryGetComponent<Image>(out var image))
                {
                    image.raycastTarget = false;
                    image.color = new Color(1f, 1f, 1f, 0.16f);
                }
            }

            void LayoutButton(string name, Vector2 position, Vector2 size, string fallbackText)
            {
                var target = FindChild(loginPanel.transform, name);
                if (target == null)
                {
                    return;
                }

                SetRect(target as RectTransform, position, size);
                if (target.TryGetComponent<Image>(out var image))
                {
                    image.color = new Color(0.34f, 0.12f, 0.07f, 0.94f);
                }

                var label = target.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    if (string.IsNullOrWhiteSpace(label.text))
                    {
                        label.text = fallbackText;
                    }

                    label.fontSize = 24f;
                    label.alignment = TextAlignmentOptions.Center;
                    label.color = new Color(1f, 0.92f, 0.78f, 1f);
                }
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                var nested = FindChild(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private Vector2 ResolveCanvasSize()
        {
            var rect = transform as RectTransform;
            if (rect != null && rect.rect.width > 0f && rect.rect.height > 0f)
            {
                return rect.rect.size;
            }

            return new Vector2(1920f, 1080f);
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }
}
