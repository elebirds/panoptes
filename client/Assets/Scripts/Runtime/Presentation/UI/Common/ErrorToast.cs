/*************************************************
 * Project: Panoptes
 * File: ErrorToast.cs
 * Author: Panoptes Team
 * Date: 2026-04-12
 * Description: Global short toast overlay for success/error feedback.
 *************************************************/

using System.Collections;
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
    public sealed class ErrorToast : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform toastRoot;
        [SerializeField] private Image toastBackground;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private float defaultDuration = 2.4f;
        [SerializeField] private Color errorBackgroundColor = new(0.33f, 0.12f, 0.12f, 0.84f);
        [SerializeField] private Color successBackgroundColor = new(0.11f, 0.30f, 0.22f, 0.82f);
        [SerializeField] private Color messageColor = Color.white;

        private Coroutine _hideCoroutine;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            ConfigureCanvas();
            ApplyVisualStyle();
            Hide();
        }

        // 业务侧只传文案和语义，不关心 prefab 层级。
        public void Show(string message, bool success)
        {
            Show(message, success, -1f);
        }

        // duration < 0 时使用默认值；重复调用会覆盖上一条提示。
        public void Show(string message, bool success, float duration)
        {
            if (!HasValidReferences())
            {
                Debug.LogError("[ErrorToast] Missing serialized references on prefab.");
                return;
            }

            messageText.text = message ?? string.Empty;
            messageText.color = messageColor;
            toastBackground.color = success ? successBackgroundColor : errorBackgroundColor;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            StopHideCoroutine();
            var targetDuration = duration > 0f ? duration : defaultDuration;
            if (Application.isPlaying && targetDuration > 0f && isActiveAndEnabled)
            {
                _hideCoroutine = StartCoroutine(HideAfterDelay(targetDuration));
            }
        }

        public void Hide()
        {
            StopHideCoroutine();
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
            canvas.sortingOrder = 950;

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
                   && toastRoot != null
                   && toastBackground != null
                   && messageText != null;
        }

        private void ApplyVisualStyle()
        {
            if (!HasValidReferences())
            {
                return;
            }

            toastBackground.sprite = RoundedRectSpriteCache.GetSprite(64, 18);
            toastBackground.type = Image.Type.Sliced;
            toastBackground.pixelsPerUnitMultiplier = 1f;
            toastBackground.raycastTarget = false;
            toastBackground.color = errorBackgroundColor;

            messageText.font = TMP_Settings.defaultFontAsset;
            messageText.fontSize = 27f;
            messageText.color = messageColor;
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.textWrappingMode = TextWrappingModes.Normal;
            messageText.raycastTarget = false;
        }

        private IEnumerator HideAfterDelay(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            Hide();
        }

        private void StopHideCoroutine()
        {
            if (_hideCoroutine == null)
            {
                return;
            }

            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

#if UNITY_EDITOR
        // Prefab 是 ErrorToast 的唯一结构来源。
        // 运行时代码不再补结构，因此这里需要一次性把引用和样式全部建完整。
        public void EditorRebuildUiForPrefab()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            ConfigureCanvas();

            toastRoot = CreateUiObject(
                "ToastRoot",
                transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-300f, -148f),
                new Vector2(300f, -68f));
            toastBackground = toastRoot.gameObject.AddComponent<Image>();

            var messageRoot = CreateUiObject(
                "Message",
                toastRoot,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                new Vector2(18f, 12f),
                new Vector2(-18f, -12f));
            messageText = messageRoot.gameObject.AddComponent<TextMeshProUGUI>();

            ApplyVisualStyle();
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
#endif
    }
}
