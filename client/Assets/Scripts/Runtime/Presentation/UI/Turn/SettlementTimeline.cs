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
        private bool _warnedMissingUi;

        private void Awake()
        {
            TryResolveUiReferences(false);
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
            if (!TryResolveUiReferences(true))
            {
                return;
            }

            titleText.text = "Settlement Timeline";
            if (evt?.Settlement?.Sections == null || evt.Settlement.Sections.Count == 0)
            {
                timelineText.text = "No settlement sections this turn";
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
                builder.Append(" - ");
                builder.Append(section.Events != null ? section.Events.Count : 0);
                builder.Append(" events");
                if (i < evt.Settlement.Sections.Count - 1)
                {
                    builder.Append('\n');
                }
            }

            timelineText.text = builder.ToString();
        }

        private bool TryResolveUiReferences(bool logWarning)
        {
            if (root == null)
            {
                root = GetComponent<RectTransform>();
            }

            if (root != null)
            {
                if (background == null)
                {
                    var bgTransform = root.Find("Background");
                    if (bgTransform != null)
                    {
                        background = bgTransform.GetComponent<Image>();
                    }
                }

                if (titleText == null)
                {
                    var titleTransform = root.Find("Title");
                    if (titleTransform != null)
                    {
                        titleText = titleTransform.GetComponent<TextMeshProUGUI>();
                    }
                }

                if (timelineText == null)
                {
                    var timelineTransform = root.Find("Timeline");
                    if (timelineTransform != null)
                    {
                        timelineText = timelineTransform.GetComponent<TextMeshProUGUI>();
                    }
                }
            }

            var ok = root != null && background != null && titleText != null && timelineText != null;
            if (!ok && logWarning && !_warnedMissingUi)
            {
                _warnedMissingUi = true;
                Debug.LogWarning("[SettlementTimeline] Missing UI references. Assign root/background/titleText/timelineText in prefab.");
            }

            return ok;
        }
    }
}
