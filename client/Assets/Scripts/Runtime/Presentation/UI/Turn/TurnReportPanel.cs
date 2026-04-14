/*************************************************
 * Project: Panoptes
 * File: TurnReportPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Displays the latest turn settlement summary for player review.
 *************************************************/

using Panoptes.Core.Application.Cache;
using Panoptes.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Turn
{
    public sealed class TurnReportPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI reportText;

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
            titleText.text = "回合简报";
            if (evt == null)
            {
                reportText.text = "等待结算";
                return;
            }

            reportText.text =
                $"建筑新增：{SafeCount(evt.BuiltNodeIDs)}\n" +
                $"单位移动：{SafeCount(evt.MovedUnitIDs)}\n" +
                $"单位损失：{SafeCount(evt.DeadUnitIDs)}\n" +
                $"城堡受击：{(evt.CastleDamaged ? "是" : "否")}\n" +
                $"下一阶段：{evt.Settlement?.NextPhase ?? string.Empty}";
        }

        private static int SafeCount(System.Collections.ICollection values)
        {
            return values != null ? values.Count : 0;
        }

        private void EnsureUi()
        {
            root ??= GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            root.anchorMin = new Vector2(1f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-24f, -24f);
            root.sizeDelta = new Vector2(260f, 150f);

            background = EnsureImage("Background");
            background.color = new Color(0.08f, 0.09f, 0.13f, 0.88f);
            titleText ??= CreateText("Title", new Vector2(-16f, -16f), new Vector2(228f, 24f), 24f, FontStyles.Bold, TextAlignmentOptions.TopRight);
            reportText ??= CreateText("Report", new Vector2(-16f, -50f), new Vector2(228f, 84f), 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
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

        private TextMeshProUGUI CreateText(string objectName, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var rect = EnsureRect(objectName, anchoredPosition, size);
            var text = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = alignment;
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

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
