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

            titleText.text = "Turn Report";
            if (evt == null)
            {
                reportText.text = "Waiting settlement";
                return;
            }

            reportText.text =
                $"Built: {SafeCount(evt.BuiltNodeIDs)}\n" +
                $"Moved: {SafeCount(evt.MovedUnitIDs)}\n" +
                $"Lost: {SafeCount(evt.DeadUnitIDs)}\n" +
                $"City Core Hit: {(evt.CityCoreDamaged ? "Yes" : "No")}\n" +
                $"Next Phase: {evt.Settlement?.NextPhase ?? string.Empty}";
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

                if (reportText == null)
                {
                    var reportTransform = root.Find("Report");
                    if (reportTransform != null)
                    {
                        reportText = reportTransform.GetComponent<TextMeshProUGUI>();
                    }
                }
            }

            var ok = root != null && background != null && titleText != null && reportText != null;
            if (!ok && logWarning && !_warnedMissingUi)
            {
                _warnedMissingUi = true;
                Debug.LogWarning("[TurnReportPanel] Missing UI references. Assign root/background/titleText/reportText in prefab.");
            }

            return ok;
        }

        private static int SafeCount(System.Collections.ICollection values)
        {
            return values != null ? values.Count : 0;
        }
    }
}
