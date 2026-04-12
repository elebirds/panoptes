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
    public sealed class ErrorToast : MonoBehaviour
    {
        public static ErrorToast Instance { get; private set; }

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform toastRoot;
        [SerializeField] private Image toastBackground;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private float defaultDuration = 2.4f;
        [SerializeField] private bool autoBuildFallbackUi = true;
        [SerializeField] private bool autoBindByName = true;
        [SerializeField] private Color errorBackgroundColor = new(0.36f, 0.12f, 0.12f, 0.95f);
        [SerializeField] private Color successBackgroundColor = new(0.12f, 0.32f, 0.22f, 0.95f);
        [SerializeField] private Color messageColor = Color.white;

        private Coroutine _hideCoroutine;

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

        public void Show(string message, bool success)
        {
            Show(message, success, -1f);
        }

        public void Show(string message, bool success = false, float duration = -1f)
        {
            EnsureUiReady();

            if (messageText != null)
            {
                messageText.text = message ?? string.Empty;
                messageText.color = messageColor;
            }

            if (toastBackground != null)
            {
                toastBackground.color = success ? successBackgroundColor : errorBackgroundColor;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

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

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
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
        }

        private void EnsureCanvas()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;

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
            return toastRoot != null && toastBackground != null && messageText != null;
        }

        private void TryBindByName()
        {
            if (toastRoot == null)
            {
                var foundRoot = transform.Find("ToastRoot");
                toastRoot = foundRoot as RectTransform;
            }

            if (toastBackground == null && toastRoot != null)
            {
                toastBackground = toastRoot.GetComponent<Image>();
            }

            if (messageText == null && toastRoot != null)
            {
                var foundMessage = toastRoot.Find("Message");
                messageText = foundMessage != null ? foundMessage.GetComponent<TextMeshProUGUI>() : null;
            }
        }

        private void BuildFallbackUi()
        {
            if (toastRoot == null)
            {
                toastRoot = CreateUiObject(
                    "ToastRoot",
                    transform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(-300f, -148f),
                    new Vector2(300f, -68f));
            }

            toastBackground = toastRoot.GetComponent<Image>();
            if (toastBackground == null)
            {
                toastBackground = toastRoot.gameObject.AddComponent<Image>();
            }
            toastBackground.color = errorBackgroundColor;
            toastBackground.raycastTarget = false;

            var message = toastRoot.Find("Message") as RectTransform;
            if (message == null)
            {
                message = CreateUiObject(
                    "Message",
                    toastRoot,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(18f, 12f),
                    new Vector2(-18f, -12f));
                messageText = message.gameObject.AddComponent<TextMeshProUGUI>();
            }
            else if (messageText == null)
            {
                messageText = message.GetComponent<TextMeshProUGUI>();
                if (messageText == null)
                {
                    messageText = message.gameObject.AddComponent<TextMeshProUGUI>();
                }
            }

            ConfigureMessageText(messageText);
        }

        private void ConfigureMessageText(TextMeshProUGUI text)
        {
            if (text == null)
            {
                return;
            }

            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 28f;
            text.color = messageColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
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
            toastRoot = null;
            toastBackground = null;
            messageText = null;
            autoBindByName = true;
            autoBuildFallbackUi = true;

            EnsureUiReady();
            Hide();
            EditorUtility.SetDirty(gameObject);
        }
#endif
    }
}
