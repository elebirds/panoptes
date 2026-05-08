using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.Map
{
    public sealed class SettlementPlaybackControlOverlay : IDisposable
    {
        private static readonly Color PanelColor = new(0.08f, 0.1f, 0.12f, 0.82f);
        private static readonly Color ButtonColor = new(0.18f, 0.22f, 0.27f, 0.92f);
        private static readonly Color SelectedButtonColor = new(0.25f, 0.48f, 0.72f, 0.96f);
        private static readonly Color DisabledButtonColor = new(0.12f, 0.13f, 0.15f, 0.72f);
        private static readonly Color TextColor = new(0.92f, 0.95f, 0.98f, 1f);

        private readonly Action _skipPlayback;
        private readonly Action<SettlementPlaybackMode> _setPlaybackMode;

        private GameObject _root;
        private TextMeshProUGUI _statusText;
        private Button _skipButton;
        private Button _fullButton;
        private Button _fastButton;
        private Button _criticalOnlyButton;

        public SettlementPlaybackControlOverlay(
            Action skipPlayback,
            Action<SettlementPlaybackMode> setPlaybackMode)
        {
            _skipPlayback = skipPlayback;
            _setPlaybackMode = setPlaybackMode;
        }

        public void Refresh(SettlementPlaybackMode mode, bool isPlaying)
        {
            if (!EnsureRoot())
            {
                return;
            }

            _root.SetActive(true);
            _statusText.text = isPlaying ? "结算播放中" : "结算播放";
            _skipButton.interactable = isPlaying;
            ApplyButtonVisual(_skipButton, isPlaying ? ButtonColor : DisabledButtonColor);
            ApplyModeVisual(_fullButton, mode == SettlementPlaybackMode.Full);
            ApplyModeVisual(_fastButton, mode == SettlementPlaybackMode.Fast);
            ApplyModeVisual(_criticalOnlyButton, mode == SettlementPlaybackMode.CriticalOnly);
        }

        public void Dispose()
        {
            if (_root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }

            _root = null;
            _statusText = null;
            _skipButton = null;
            _fullButton = null;
            _fastButton = null;
            _criticalOnlyButton = null;
        }

        private bool EnsureRoot()
        {
            if (_root != null)
            {
                return true;
            }

            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                return false;
            }

            _root = new GameObject("SettlementPlaybackControls", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(canvas.transform, false);
            var rect = _root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -96f);
            rect.sizeDelta = new Vector2(384f, 88f);

            var panelImage = _root.GetComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;

            _statusText = CreateText(rect, "Status", "结算播放", new Vector2(16f, -10f), new Vector2(210f, 22f), 15f, TextAlignmentOptions.Left);
            _skipButton = CreateButton(rect, "SkipButton", "跳过", new Vector2(292f, -10f), new Vector2(76f, 28f), _skipPlayback);
            _fullButton = CreateButton(rect, "FullModeButton", "完整", new Vector2(16f, -48f), new Vector2(80f, 28f), () => _setPlaybackMode?.Invoke(SettlementPlaybackMode.Full));
            _fastButton = CreateButton(rect, "FastModeButton", "快速", new Vector2(106f, -48f), new Vector2(80f, 28f), () => _setPlaybackMode?.Invoke(SettlementPlaybackMode.Fast));
            _criticalOnlyButton = CreateButton(rect, "CriticalOnlyModeButton", "关键", new Vector2(196f, -48f), new Vector2(80f, 28f), () => _setPlaybackMode?.Invoke(SettlementPlaybackMode.CriticalOnly));
            return true;
        }

        private static Button CreateButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            Action onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = buttonObject.GetComponent<Image>();
            image.color = ButtonColor;
            image.raycastTarget = true;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            CreateText(rect, "Label", label, Vector2.zero, size, 14f, TextAlignmentOptions.Center);
            return button;
        }

        private static TextMeshProUGUI CreateText(
            RectTransform parent,
            string name,
            string text,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = TextColor;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static void ApplyModeVisual(Button button, bool selected)
        {
            ApplyButtonVisual(button, selected ? SelectedButtonColor : ButtonColor);
        }

        private static void ApplyButtonVisual(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            if (button.targetGraphic is Image image)
            {
                image.color = color;
            }
        }
    }
}
