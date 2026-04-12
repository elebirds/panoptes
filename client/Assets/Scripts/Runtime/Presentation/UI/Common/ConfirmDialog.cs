/*************************************************
 * Project: Panoptes
 * File: ConfirmDialog.cs
 * Author: Panoptes Team
 * Date: 2026-04-12
 * Description: Global confirm dialog overlay.
 *************************************************/

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Panoptes.Presentation.UI.Common
{
    public sealed class ConfirmDialog : MonoBehaviour
    {
        public static ConfirmDialog Instance { get; private set; }

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button maskButton;
        [SerializeField] private Image maskImage;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image panelBackground;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private bool autoBuildFallbackUi = true;
        [SerializeField] private bool autoBindByName = true;
        [SerializeField] private Color maskColor = new(0f, 0f, 0f, 0.62f);
        [SerializeField] private Color panelColor = new(0.12f, 0.15f, 0.2f, 0.98f);
        [SerializeField] private Color confirmButtonColor = new(0.28f, 0.49f, 0.78f, 1f);
        [SerializeField] private Color cancelButtonColor = new(0.26f, 0.28f, 0.31f, 1f);

        private Action _onConfirm;
        private Action _onCancel;
        private bool _buttonHandlersBound;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureUiReady();
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Show(string title, string message, Action onConfirm, Action onCancel)
        {
            EnsureUiReady();

            _onConfirm = onConfirm;
            _onCancel = onCancel;

            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
            }

            if (messageText != null)
            {
                messageText.text = message ?? string.Empty;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        private void EnsureUiReady()
        {
            EnsureCanvas();
            EnsureCanvasGroup();

            if (autoBindByName)
            {
                TryBindByName();
            }

            if (autoBuildFallbackUi && !HasEssentialReferences())
            {
                BuildFallbackUi();
                TryBindByName();
            }

            WireButtons();
        }

        private void EnsureCanvas()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 960;

            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private bool HasEssentialReferences()
        {
            return maskButton != null
                   && panelRoot != null
                   && titleText != null
                   && messageText != null
                   && confirmButton != null
                   && cancelButton != null;
        }

        private void TryBindByName()
        {
            if (maskButton == null)
            {
                var foundMask = transform.Find("Mask");
                maskButton = foundMask != null ? foundMask.GetComponent<Button>() : null;
                maskImage = foundMask != null ? foundMask.GetComponent<Image>() : null;
            }

            if (panelRoot == null)
            {
                panelRoot = transform.Find("PanelRoot") as RectTransform;
            }

            if (panelRoot != null)
            {
                if (panelBackground == null)
                {
                    panelBackground = panelRoot.GetComponent<Image>();
                }

                if (titleText == null)
                {
                    titleText = panelRoot.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
                }

                if (messageText == null)
                {
                    messageText = panelRoot.Find("MessageText")?.GetComponent<TextMeshProUGUI>();
                }

                if (confirmButton == null)
                {
                    confirmButton = panelRoot.Find("ConfirmButton")?.GetComponent<Button>();
                }

                if (cancelButton == null)
                {
                    cancelButton = panelRoot.Find("CancelButton")?.GetComponent<Button>();
                }
            }
        }

        private void BuildFallbackUi()
        {
            if (maskButton == null)
            {
                var mask = CreateUiObject(
                    "Mask",
                    transform,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero);
                maskImage = mask.gameObject.AddComponent<Image>();
                maskImage.color = maskColor;
                maskButton = mask.gameObject.AddComponent<Button>();
                maskButton.targetGraphic = maskImage;
            }

            if (panelRoot == null)
            {
                panelRoot = CreateUiObject(
                    "PanelRoot",
                    transform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-300f, -180f),
                    new Vector2(300f, 180f));
            }

            panelBackground = panelRoot.GetComponent<Image>();
            if (panelBackground == null)
            {
                panelBackground = panelRoot.gameObject.AddComponent<Image>();
            }
            panelBackground.color = panelColor;

            titleText = EnsureText(
                panelRoot,
                "TitleText",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(24f, -76f),
                new Vector2(-24f, -18f),
                30f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                "请确认");

            messageText = EnsureText(
                panelRoot,
                "MessageText",
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(28f, 86f),
                new Vector2(-28f, -94f),
                24f,
                FontStyles.Normal,
                TextAlignmentOptions.Midline,
                string.Empty);

            cancelButton = EnsureButton(
                panelRoot,
                "CancelButton",
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(28f, 24f),
                new Vector2(-10f, 74f),
                cancelButtonColor,
                "取消");

            confirmButton = EnsureButton(
                panelRoot,
                "ConfirmButton",
                new Vector2(0.5f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(10f, 24f),
                new Vector2(-28f, 74f),
                confirmButtonColor,
                "确认");
        }

        private void WireButtons()
        {
            if (_buttonHandlersBound || maskButton == null || confirmButton == null || cancelButton == null)
            {
                return;
            }

            maskButton.onClick.AddListener(HandleCancelClicked);
            confirmButton.onClick.AddListener(HandleConfirmClicked);
            cancelButton.onClick.AddListener(HandleCancelClicked);
            _buttonHandlersBound = true;
        }

        private void HandleConfirmClicked()
        {
            var callback = _onConfirm;
            Hide();
            callback?.Invoke();
        }

        private void HandleCancelClicked()
        {
            var callback = _onCancel;
            Hide();
            callback?.Invoke();
        }

        private static RectTransform CreateUiObject(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static TextMeshProUGUI EnsureText(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 offsetMin,
            Vector2 offsetMax,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            string defaultValue)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = CreateUiObject(name, parent, anchorMin, anchorMax, pivot, offsetMin, offsetMax);
            }

            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.color = Color.white;
            text.raycastTarget = false;
            if (string.IsNullOrEmpty(text.text))
            {
                text.text = defaultValue;
            }

            return text;
        }

        private static Button EnsureButton(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color backgroundColor,
            string label)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = CreateUiObject(name, parent, anchorMin, anchorMax, pivot, offsetMin, offsetMax);
            }

            var image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }
            image.color = backgroundColor;

            var button = rect.GetComponent<Button>();
            if (button == null)
            {
                button = rect.gameObject.AddComponent<Button>();
            }
            button.targetGraphic = image;

            var labelRect = rect.Find("Label") as RectTransform;
            if (labelRect == null)
            {
                labelRect = CreateUiObject("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            }

            var labelText = labelRect.GetComponent<TextMeshProUGUI>();
            if (labelText == null)
            {
                labelText = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            labelText.font = TMP_Settings.defaultFontAsset;
            labelText.fontSize = 24f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.enableWordWrapping = false;
            labelText.color = Color.white;
            labelText.raycastTarget = false;
            labelText.text = label;

            return button;
        }

#if UNITY_EDITOR
        public void EditorRebuildUiForPrefab()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != null)
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            canvasGroup = null;
            maskButton = null;
            maskImage = null;
            panelRoot = null;
            panelBackground = null;
            titleText = null;
            messageText = null;
            confirmButton = null;
            cancelButton = null;
            autoBindByName = true;
            autoBuildFallbackUi = true;
            _buttonHandlersBound = false;

            EnsureUiReady();
            Hide();
            EditorUtility.SetDirty(gameObject);
        }
#endif
    }
}
