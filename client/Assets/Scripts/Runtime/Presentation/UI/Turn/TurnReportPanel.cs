/*************************************************
 * Project: Panoptes
 * File: TurnReportPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Displays the latest turn settlement summary for player review.
 *************************************************/

using System;
using Panoptes.Core.Application.Stores;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.Turn
{
    public sealed class TurnReportPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI reportText;

        private IDisposable _settlementSubscription;
        private SettlementStore _settlementStore;
        private bool _warnedMissingUi;

        [Inject]
        private void Construct(SettlementStore settlementStore)
        {
            _settlementStore = settlementStore;
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

            titleText.text = "Turn Report";
            var settlement = state?.Settlement;
            if (settlement == null)
            {
                reportText.text = "Waiting settlement";
                return;
            }

            reportText.text =
                $"Built: {SafeCount(settlement.BuiltNodeIDs)}\n" +
                $"Moved: {SafeCount(settlement.MovedUnitIDs)}\n" +
                $"Lost: {SafeCount(settlement.DeadUnitIDs)}\n" +
                $"City Core Hit: {(settlement.CityCoreDamaged ? "Yes" : "No")}\n" +
                $"Next Phase: {settlement.NextPhase ?? string.Empty}";
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
                PanoptesLog.Warning("[TurnReportPanel] Missing UI references. Assign root/background/titleText/reportText in prefab.");
            }

            return ok;
        }

        private static int SafeCount(System.Collections.ICollection values)
        {
            return values != null ? values.Count : 0;
        }
    }
}
