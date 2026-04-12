/*************************************************
 * Project: Panoptes
 * File: GameOverOverlay.cs
 * Author: Panoptes Team
 * Date: 2026-04-13
 * Description: Runtime-built end-of-game overlay.
 *************************************************/

using Panoptes.Core.Application.Cache;
using Panoptes.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Game
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GameOverOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image panelBackground;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI reasonText;
        [SerializeField] private TextMeshProUGUI narrativeText;

        private GameStateCache _cache;

        private void Awake()
        {
            EnsureUi();
            Hide();
        }

        private void OnEnable()
        {
            _cache = GameStateCache.Instance;
            if (_cache != null)
            {
                _cache.OnGameOver += OnGameOver;
            }
        }

        private void OnDisable()
        {
            if (_cache != null)
            {
                _cache.OnGameOver -= OnGameOver;
            }
        }

        private void OnGameOver(GameOverEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            EnsureUi();
            titleText.text = evt.IsWinner
                ? "胜利"
                : evt.Reason == "timeout_draw" ? "平局" : "失利";
            reasonText.text = MapReason(evt.Reason);
            narrativeText.text = string.IsNullOrWhiteSpace(evt.Narrative)
                ? "史官正在整理本局记录。"
                : evt.Narrative;

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = false;
        }

        private void Hide()
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void EnsureUi()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();

            var rootRect = transform as RectTransform;
            if (rootRect == null)
            {
                return;
            }

            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            panelRoot ??= EnsureRect("Panel", rootRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(720f, 420f), Vector2.zero);
            panelBackground ??= EnsureImage(panelRoot.gameObject, new Color(0.08f, 0.10f, 0.14f, 0.94f));
            titleText ??= EnsureText("Title", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(600f, 56f), 42f, FontStyles.Bold, TextAlignmentOptions.Center);
            reasonText ??= EnsureText("Reason", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(620f, 42f), 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            narrativeText ??= EnsureText("Narrative", panelRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(620f, 220f), 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            narrativeText.enableWordWrapping = true;
        }

        private static RectTransform EnsureRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 anchoredPosition)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                existing.anchorMin = anchorMin;
                existing.anchorMax = anchorMax;
                existing.pivot = pivot;
                existing.sizeDelta = size;
                existing.anchoredPosition = anchoredPosition;
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        private static Image EnsureImage(GameObject go, Color color)
        {
            var image = go.GetComponent<Image>();
            if (image == null)
            {
                image = go.AddComponent<Image>();
            }

            image.color = color;
            return image;
        }

        private static TextMeshProUGUI EnsureText(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var rect = EnsureRect(name, parent, anchorMin, anchorMax, pivot, size, anchoredPosition);
            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static string MapReason(string reason)
        {
            return reason switch
            {
                "castle_destroyed" => "主城已被攻陷",
                "timeout_draw" => "达到最大回合，判定为平局",
                _ => string.IsNullOrWhiteSpace(reason) ? "对局结束" : reason
            };
        }
    }
}
