/*************************************************
 * Project: Panoptes
 * File: SettlementTimeline.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Displays the latest settlement sections as a lightweight timeline.
 *************************************************/

using System.Text;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Turn
{
    public sealed class SettlementTimeline : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI timelineText;

        private GameStateCache _cache;

        private void Awake()
        {
            EnsureUi();
            _cache = GameStateCache.Instance;
        }

        private void OnEnable()
        {
            _cache = GameStateCache.Instance;
            if (_cache != null)
            {
                _cache.OnTurnSettled += OnTurnSettled;
            }
        }

        private void OnDisable()
        {
            if (_cache != null)
            {
                _cache.OnTurnSettled -= OnTurnSettled;
            }
        }

        private void OnTurnSettled(TurnSettledEvent evt)
        {
            EnsureUi();
            titleText.text = "结算时间线";
            if (evt?.Settlement?.Sections == null || evt.Settlement.Sections.Count == 0)
            {
                timelineText.text = "本回合无可播放结算";
                return;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < evt.Settlement.Sections.Count; i++)
            {
                var section = evt.Settlement.Sections[i];
                if (section == null)
                {
                    continue;
                }

                builder.Append(section.Section);
                builder.Append(" · ");
                builder.Append(section.Events != null ? section.Events.Count : 0);
                builder.Append(" 事件");
                if (i < evt.Settlement.Sections.Count - 1)
                {
                    builder.Append('\n');
                }
            }

            timelineText.text = builder.ToString();
        }

        private void EnsureUi()
        {
            root ??= GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(24f, 24f);
            root.sizeDelta = new Vector2(280f, 150f);

            background = EnsureImage("Background");
            background.color = new Color(0.08f, 0.09f, 0.13f, 0.88f);
            titleText ??= CreateText("Title", new Vector2(16f, -16f), new Vector2(248f, 24f), 24f, FontStyles.Bold);
            timelineText ??= CreateText("Timeline", new Vector2(16f, -50f), new Vector2(248f, 84f), 18f, FontStyles.Normal);
        }

        private Image EnsureImage(string objectName)
        {
            var existing = root.Find(objectName) as RectTransform;
            var rect = existing;
            if (rect == null)
            {
                var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root, false);
                rect = go.GetComponent<RectTransform>();
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect.GetComponent<Image>();
        }

        private TextMeshProUGUI CreateText(string objectName, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style)
        {
            var rect = EnsureRect(objectName, anchoredPosition, size);
            var text = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform EnsureRect(string objectName, Vector2 anchoredPosition, Vector2 size)
        {
            var existing = root.Find(objectName) as RectTransform;
            var rect = existing;
            if (rect == null)
            {
                var go = new GameObject(objectName, typeof(RectTransform));
                go.transform.SetParent(root, false);
                rect = go.GetComponent<RectTransform>();
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
