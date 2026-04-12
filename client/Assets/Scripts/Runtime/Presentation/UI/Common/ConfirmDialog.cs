/*************************************************
 * Project: Panoptes
 * File: ConfirmDialog.cs
 * Author: Panoptes Team
 * Date: 2026-04-12
 * Description: Global confirm dialog overlay.
 *************************************************/

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Panoptes.Presentation.UI.Common
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    [RequireComponent(typeof(CanvasGroup))]
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
        [SerializeField] private Color maskColor = new(0f, 0f, 0f, 0.42f);
        [SerializeField] private Color panelColor = new(0.11f, 0.14f, 0.19f, 0.93f);
        [SerializeField] private Color confirmButtonColor = new(0.26f, 0.49f, 0.76f, 0.92f);
        [SerializeField] private Color cancelButtonColor = new(0.24f, 0.27f, 0.31f, 0.82f);

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

            ConfigureCanvas();
            ApplyVisualStyle();
            WireButtons();
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ConfirmDialog 只负责展示和收集用户确认，不承载业务逻辑。
        // 调用方负责把真正的业务动作放在回调里。
        public void Show(string title, string message, Action onConfirm, Action onCancel)
        {
            if (!HasValidReferences())
            {
                Debug.LogError("[ConfirmDialog] Missing serialized references on prefab.");
                return;
            }

            _onConfirm = onConfirm;
            _onCancel = onCancel;

            titleText.text = title ?? string.Empty;
            messageText.text = message ?? string.Empty;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        public void Hide()
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void ConfigureCanvas()
        {
            var rootRect = transform as RectTransform;
            if (rootRect != null)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = Vector2.zero;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                rootRect.localScale = Vector3.one;
                rootRect.localRotation = Quaternion.identity;
            }

            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 960;

            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup ??= GetComponent<CanvasGroup>();
        }

        private bool HasValidReferences()
        {
            return canvasGroup != null
                   && maskButton != null
                   && maskImage != null
                   && panelRoot != null
                   && panelBackground != null
                   && titleText != null
                   && messageText != null
                   && confirmButton != null
                   && cancelButton != null;
        }

        private void ApplyVisualStyle()
        {
            if (!HasValidReferences())
            {
                return;
            }

            maskImage.color = maskColor;
            maskImage.raycastTarget = true;

            panelBackground.sprite = RoundedRectSpriteCache.GetSprite(96, 24);
            panelBackground.type = Image.Type.Sliced;
            panelBackground.color = panelColor;

            ApplyButtonStyle(confirmButton, confirmButtonColor);
            ApplyButtonStyle(cancelButton, cancelButtonColor);
            ConfigureText(titleText, 30f, FontStyles.Bold, TextAlignmentOptions.Center, TextWrappingModes.NoWrap);
            ConfigureText(messageText, 24f, FontStyles.Normal, TextAlignmentOptions.Midline, TextWrappingModes.Normal);
        }

        private void WireButtons()
        {
            if (_buttonHandlersBound || !HasValidReferences())
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

        private static void ApplyButtonStyle(Button button, Color backgroundColor)
        {
            var image = button.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.sprite = RoundedRectSpriteCache.GetSprite(64, 16);
            image.type = Image.Type.Sliced;
            image.color = backgroundColor;
        }

        private static void ConfigureText(
            TextMeshProUGUI text,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            TextWrappingModes wrappingMode)
        {
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.textWrappingMode = wrappingMode;
            text.color = Color.white;
            text.raycastTarget = false;
        }

#if UNITY_EDITOR
        // ConfirmDialog 的结构和引用完全由 prefab 决定。
        // 运行时代码不再补节点，因此编辑器重建时必须把层级和引用一次性写完整。
        public void EditorRebuildUiForPrefab()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            ConfigureCanvas();

            var maskRect = CreateUiObject(
                "Mask",
                transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            maskImage = maskRect.gameObject.AddComponent<Image>();
            maskButton = maskRect.gameObject.AddComponent<Button>();
            maskButton.targetGraphic = maskImage;

            panelRoot = CreateUiObject(
                "PanelRoot",
                transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-300f, -180f),
                new Vector2(300f, 180f));
            panelBackground = panelRoot.gameObject.AddComponent<Image>();

            titleText = CreateText(
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
                TextWrappingModes.NoWrap,
                "请确认");

            messageText = CreateText(
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
                TextWrappingModes.Normal,
                string.Empty);

            cancelButton = CreateButton(
                panelRoot,
                "CancelButton",
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(28f, 24f),
                new Vector2(-10f, 74f),
                "取消");

            confirmButton = CreateButton(
                panelRoot,
                "ConfirmButton",
                new Vector2(0.5f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(10f, 24f),
                new Vector2(-28f, 74f),
                "确认");

            _buttonHandlersBound = false;
            ApplyVisualStyle();
            WireButtons();
            Hide();
            EditorUtility.SetDirty(gameObject);
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
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static TextMeshProUGUI CreateText(
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
            TextWrappingModes wrappingMode,
            string defaultText)
        {
            var rect = CreateUiObject(name, parent, anchorMin, anchorMax, pivot, offsetMin, offsetMax);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(text, fontSize, style, alignment, wrappingMode);
            text.text = defaultText;
            return text;
        }

        private static Button CreateButton(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 offsetMin,
            Vector2 offsetMax,
            string label)
        {
            var rect = CreateUiObject(name, parent, anchorMin, anchorMax, pivot, offsetMin, offsetMax);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var labelRect = CreateUiObject(
                "Label",
                rect,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            var labelText = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(labelText, 24f, FontStyles.Bold, TextAlignmentOptions.Center, TextWrappingModes.NoWrap);
            labelText.text = label;

            return button;
        }
#endif
    }

    // 通用弹层统一复用这张代码生成的圆角 sprite，
    // 这样不需要维护额外贴图资源，样式也能始终保持一致。
    internal static class RoundedRectSpriteCache
    {
        private static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite GetSprite(int size, int radius)
        {
            var safeSize = Mathf.Max(16, size);
            var safeRadius = Mathf.Clamp(radius, 2, safeSize / 2);
            var key = $"{safeSize}:{safeRadius}";
            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(safeSize, safeSize, TextureFormat.ARGB32, false)
            {
                name = $"RoundedRect_{safeSize}_{safeRadius}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[safeSize * safeSize];
            var fill = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);
            for (var y = 0; y < safeSize; y++)
            {
                for (var x = 0; x < safeSize; x++)
                {
                    pixels[y * safeSize + x] = IsInsideRoundedRect(x, y, safeSize, safeRadius) ? fill : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, safeSize, safeSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(safeRadius, safeRadius, safeRadius, safeRadius));
            sprite.name = texture.name;
            Cache[key] = sprite;
            return sprite;
        }

        private static bool IsInsideRoundedRect(int x, int y, int size, int radius)
        {
            if ((x >= radius && x < size - radius) || (y >= radius && y < size - radius))
            {
                return true;
            }

            var cornerX = x < radius ? radius - 0.5f : size - radius - 0.5f;
            var cornerY = y < radius ? radius - 0.5f : size - radius - 0.5f;
            var dx = x + 0.5f - cornerX;
            var dy = y + 0.5f - cornerY;
            var limit = radius - 0.5f;
            return dx * dx + dy * dy <= limit * limit;
        }
    }
}
