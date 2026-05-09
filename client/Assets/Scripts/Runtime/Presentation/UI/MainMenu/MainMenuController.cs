using Panoptes.Core.Application.App;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.Audio;
using Panoptes.Presentation.UI.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
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
        private PresentationAudioService _audioService;

        [Inject]
        private void Construct(PresentationAudioService audioService)
        {
            _audioService = audioService;
            ConfigureMinisterRecruitmentAudio();
        }

        private void Awake()
        {
            EnsureTitleFlameEffect();
            ConfigureMinisterRecruitmentAudio();

            if (!IsLoggedIn())
            {
                HideLobbySurface();
            }

            startButton?.onClick.AddListener(ShowLobbyEntry);
            quitButton?.onClick.AddListener(QuitGame);
            lobbyBackButton?.onClick.AddListener(OnBackToMainMenuClicked);
            settingsBackButton?.onClick.AddListener(OnBackToMainMenuClicked);
        }

        private void Start()
        {
            _lastLoggedIn = IsLoggedIn();
            ApplyLoginState(_lastLoggedIn);
            ShowMainMenu();
        }

        private void OnDestroy()
        {
            startButton?.onClick.RemoveListener(ShowLobbyEntry);
            quitButton?.onClick.RemoveListener(QuitGame);
            lobbyBackButton?.onClick.RemoveListener(OnBackToMainMenuClicked);
            settingsBackButton?.onClick.RemoveListener(OnBackToMainMenuClicked);
        }

        private void OnRectTransformDimensionsChange()
        {
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

            HideLobbySurface();
            SetPanelActive(settingsPanel, false);
        }

        public void ShowLobbyEntry()
        {
            _audioService?.PlayUiClick(UiClickAudioKind.Confirm);
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
            _audioService?.PlayUiClick(UiClickAudioKind.Soft);
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
            _audioService?.PlayUiClick(UiClickAudioKind.Back);
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnBackToMainMenuClicked()
        {
            _audioService?.PlayUiClick(UiClickAudioKind.Back);
            ShowMainMenu();
        }

        private void ApplyLoginState(bool loggedIn)
        {
            SetPanelActive(loginPanel, !loggedIn);
            if (!loggedIn)
            {
                HideLobbySurface();
            }

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

        private void ConfigureMinisterRecruitmentAudio()
        {
            var recruitmentPanel = GetComponent<MainMenuMinisterRecruitmentPanel>();
            if (recruitmentPanel != null)
            {
                recruitmentPanel.UseAudioService(_audioService);
            }
        }

        private void EnsureTitleFlameEffect()
        {
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var title = texts[i];
                if (title == null || !string.Equals((title.text ?? string.Empty).Trim(), "PANOPTES", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (title.GetComponent<MainMenuTitleFlameAnimator>() == null)
                {
                    title.gameObject.AddComponent<MainMenuTitleFlameAnimator>();
                }
                EnsureTitleLogo(title);
                return;
            }
        }

        private static void EnsureTitleLogo(TextMeshProUGUI title)
        {
            if (title == null || title.transform.parent == null)
            {
                return;
            }

            const string logoName = "TitleCrossedSwordsLogo";
            var parent = title.transform.parent;
            var existing = parent.Find(logoName);
            var logoRect = existing as RectTransform;
            if (logoRect == null)
            {
                logoRect = new GameObject(logoName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                logoRect.SetParent(parent, false);
            }

            logoRect.SetSiblingIndex(Mathf.Max(0, title.transform.GetSiblingIndex()));
            title.transform.SetSiblingIndex(logoRect.GetSiblingIndex() + 1);
            logoRect.anchorMin = title.rectTransform.anchorMin;
            logoRect.anchorMax = title.rectTransform.anchorMax;
            logoRect.pivot = title.rectTransform.pivot;
            logoRect.anchoredPosition = title.rectTransform.anchoredPosition + new Vector2(0f, -10f);
            logoRect.sizeDelta = new Vector2(230f, 230f);
            logoRect.localScale = Vector3.one;

            var image = logoRect.GetComponent<Image>();
            if (image == null)
            {
                image = logoRect.gameObject.AddComponent<Image>();
            }

            var sprite = Resources.Load<Sprite>("Icons/UI/main_title_crossed_swords_logo");
            if (sprite != null)
            {
                image.sprite = sprite;
            }

            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = new Color(1f, 0.85f, 0.52f, 0.62f);
        }

        private void HideLobbySurface()
        {
            lobbySceneController?.HidePanels();
            SetPanelActive(lobbyPanel, false);
            SetPanelActive(roomPanel, false);
            SetPanelActive(lobbyBackButtonRoot, false);
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
            var horizontalMargin = Mathf.Clamp(canvasSize.x * 0.055f, 28f, 120f);
            var verticalMargin = Mathf.Clamp(canvasSize.y * 0.075f, 24f, 96f);
            var availableWidth = Mathf.Max(300f, canvasSize.x - horizontalMargin * 2f);
            var availableHeight = Mathf.Max(300f, canvasSize.y - verticalMargin * 2f);
            var wideLayout = canvasSize.x >= 1180f && availableHeight >= 440f;
            var panelWidth = wideLayout
                ? Mathf.Min(Mathf.Clamp(canvasSize.x * 0.30f, 460f, 580f), availableWidth)
                : Mathf.Min(Mathf.Clamp(canvasSize.x - horizontalMargin * 2f, 320f, 560f), availableWidth);
            var panelHeight = Mathf.Min(Mathf.Clamp(canvasSize.y * 0.58f, 440f, 620f), availableHeight);

            if (wideLayout)
            {
                root.anchorMin = new Vector2(1f, 0.5f);
                root.anchorMax = new Vector2(1f, 0.5f);
                root.pivot = new Vector2(1f, 0.5f);
                root.anchoredPosition = new Vector2(-horizontalMargin, 0f);
            }
            else
            {
                root.anchorMin = new Vector2(0.5f, 0.5f);
                root.anchorMax = new Vector2(0.5f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = new Vector2(0f, -Mathf.Min(48f, verticalMargin * 0.5f));
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

            var padding = Mathf.Clamp(panelWidth * 0.085f, 28f, 56f);
            var compact = panelWidth < 470f;
            var labelWidth = compact ? 72f : 96f;
            var inputGap = compact ? 12f : 18f;
            var inputWidth = Mathf.Max(180f, panelWidth - padding * 2f - labelWidth - inputGap);
            var inputHeight = Mathf.Clamp(panelHeight * 0.10f, 44f, 56f);
            var titleY = -Mathf.Clamp(panelHeight * 0.08f, 30f, 48f);
            var firstRowY = -Mathf.Clamp(panelHeight * 0.28f, 118f, 158f);
            var rowGap = Mathf.Clamp(panelHeight * 0.15f, 76f, 96f);
            var buttonHeight = Mathf.Clamp(panelHeight * 0.11f, 54f, 64f);
            var buttonTopFromBottom = Mathf.Clamp(panelHeight * 0.14f, 64f, 86f);
            var buttonY = -panelHeight + buttonTopFromBottom + buttonHeight;
            var buttonGap = compact ? 12f : 22f;
            var buttonWidth = Mathf.Max(128f, (panelWidth - padding * 2f - buttonGap) * 0.5f);
            var passwordBottom = firstRowY - rowGap - inputHeight;
            var buttonTop = buttonY;
            if (passwordBottom - buttonTop < 34f)
            {
                buttonY = passwordBottom - 34f;
            }

            LayoutText("UserName", new Vector2(padding, firstRowY), new Vector2(labelWidth, inputHeight), "\u8d26\u53f7", 26f, TextAlignmentOptions.Left);
            LayoutText("Password", new Vector2(padding, firstRowY - rowGap), new Vector2(labelWidth, inputHeight), "\u5bc6\u7801", 26f, TextAlignmentOptions.Left);
            LayoutInput("UserNameInput", new Vector2(padding + labelWidth + inputGap, firstRowY + 4f), new Vector2(inputWidth, inputHeight));
            LayoutInput("PasswordInput", new Vector2(padding + labelWidth + inputGap, firstRowY - rowGap + 4f), new Vector2(inputWidth, inputHeight));
            LayoutButton("LoginButton", new Vector2(padding, buttonY), new Vector2(buttonWidth, buttonHeight), "\u767b\u5f55");
            LayoutButton("RegisterButton", new Vector2(padding + buttonWidth + buttonGap, buttonY), new Vector2(buttonWidth, buttonHeight), "\u6ce8\u518c");

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
                if (target == null)
                {
                    return;
                }

                if (target.TryGetComponent<Image>(out var image))
                {
                    image.color = new Color(1f, 0.97f, 0.9f, 0.94f);
                    image.raycastTarget = true;
                }

                NormalizeInputField(target.transform);
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
            var canvas = GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (canvasRect != null && canvasRect.rect.width > 0f && canvasRect.rect.height > 0f)
            {
                return canvasRect.rect.size;
            }

            var rect = transform as RectTransform;
            if (rect != null && rect.rect.width > 0f && rect.rect.height > 0f)
            {
                return rect.rect.size;
            }

            return new Vector2(1920f, 1080f);
        }

        private static void NormalizeInputField(Transform inputRoot)
        {
            var textArea = FindChild(inputRoot, "Text Area") as RectTransform;
            if (textArea != null)
            {
                SetStretch(textArea, new Vector2(12f, 6f), new Vector2(-12f, -6f));
            }

            foreach (var text in inputRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.fontSize = Mathf.Clamp(text.fontSize <= 0f ? 20f : text.fontSize, 18f, 24f);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
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
