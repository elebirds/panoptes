/*************************************************
 * Project: Panoptes
 * File: SettlementTimeline.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Displays the latest settlement sections as a lightweight timeline.
 *************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.Turn
{
    public sealed class SettlementTimeline : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI timelineText;

        private readonly List<TurnEventDto> _clickableEvents = new();
        private IDisposable _settlementSubscription;
        private SettlementStore _settlementStore;
        private SettlementPlaybackController _playbackController;
        private bool _warnedMissingUi;

        [Inject]
        private void Construct(SettlementStore settlementStore, SettlementPlaybackController playbackController)
        {
            _settlementStore = settlementStore;
            _playbackController = playbackController;
        }

        private void Awake()
        {
            TryResolveUiReferences(false);
        }

        private void OnEnable()
        {
            _settlementSubscription?.Dispose();
            _settlementSubscription = _settlementStore?.State.Subscribe(this, static (state, self) => self.OnSettlementChanged(state));
            OnSettlementChanged(_settlementStore?.Snapshot);
        }

        private void OnDisable()
        {
            _settlementSubscription?.Dispose();
            _settlementSubscription = null;
        }

        private void OnSettlementChanged(SettlementState state)
        {
            if (!TryResolveUiReferences(true))
            {
                return;
            }

            titleText.text = "结算时间线";
            var settlement = state?.Settlement;
            if (settlement?.Sections == null || settlement.Sections.Count == 0)
            {
                _clickableEvents.Clear();
                timelineText.text = "本回合没有结算提示";
                return;
            }

            var builder = new StringBuilder();
            _clickableEvents.Clear();
            var lineCount = 0;
            for (var i = 0; i < settlement.Sections.Count; i++)
            {
                var section = settlement.Sections[i];
                if (section?.Events == null)
                {
                    continue;
                }

                for (var eventIndex = 0; eventIndex < section.Events.Count; eventIndex++)
                {
                    var evt = section.Events[eventIndex];
                    if (!SettlementTimelineEventFormatter.TryDescribeEvent(evt, out var line))
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append('\n');
                    }

                    AppendLinkedLine(builder, line, evt);
                    lineCount++;
                }
            }

            timelineText.text = lineCount > 0
                ? builder.ToString()
                : "本回合没有建造或生产相关提示";
            timelineText.ForceMeshUpdate();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || timelineText == null || _clickableEvents.Count == 0)
            {
                return;
            }

            var linkIndex = TMP_TextUtilities.FindIntersectingLink(
                timelineText,
                eventData.position,
                eventData.pressEventCamera);
            if (linkIndex < 0 || linkIndex >= timelineText.textInfo.linkInfo.Length)
            {
                return;
            }

            var linkId = timelineText.textInfo.linkInfo[linkIndex].GetLinkID();
            if (!int.TryParse(linkId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var eventIndex) ||
                eventIndex < 0 ||
                eventIndex >= _clickableEvents.Count)
            {
                return;
            }

            _playbackController?.FocusSettlementEvent(_clickableEvents[eventIndex]);
        }

        private void AppendLinkedLine(StringBuilder builder, string line, TurnEventDto evt)
        {
            var linkId = _clickableEvents.Count.ToString(CultureInfo.InvariantCulture);
            _clickableEvents.Add(evt);
            builder
                .Append("<link=\"")
                .Append(linkId)
                .Append("\"><color=#D8ECFF>")
                .Append(SanitizeRichText(line))
                .Append("</color></link>");
        }

        private static string SanitizeRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("<", "＜").Replace(">", "＞");
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

                if (timelineText != null)
                {
                    timelineText.raycastTarget = true;
                    timelineText.richText = true;
                }
            }

            var ok = root != null && background != null && titleText != null && timelineText != null;
            if (!ok && logWarning && !_warnedMissingUi)
            {
                _warnedMissingUi = true;
                PanoptesLog.Warning("[SettlementTimeline] Missing UI references. Assign root/background/titleText/timelineText in prefab.");
            }

            return ok;
        }
    }
}
