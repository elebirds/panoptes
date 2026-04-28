using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using TMPro;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class UnitInfoPlanningSummaryPresenter
    {
        private readonly TMP_Text _summaryText;

        public UnitInfoPlanningSummaryPresenter(TMP_Text summaryText)
        {
            _summaryText = summaryText;
        }

        public void Refresh(UnitView currentUnit, PlanningDraftCache draftCache)
        {
            if (_summaryText == null)
            {
                return;
            }

            if (currentUnit == null || draftCache == null)
            {
                Hide();
                return;
            }

            var currentUnitId = NormalizeToken(currentUnit.UnitId);
            var orders = draftCache.GetOrdersInDisplayOrder();
            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null || !string.Equals(NormalizeToken(order.UnitId), currentUnitId, StringComparison.Ordinal))
                {
                    continue;
                }

                _summaryText.text = FormatSummary(order);
                _summaryText.gameObject.SetActive(true);
                return;
            }

            Hide();
        }

        public static string FormatSummary(QueuedUnitOrderDto order)
        {
            if (order == null)
            {
                return string.Empty;
            }

            var action = string.IsNullOrWhiteSpace(order.Action) ? "order" : order.Action.Trim();
            var target = !string.IsNullOrWhiteSpace(order.TargetNodeId)
                ? order.TargetNodeId.Trim()
                : order.TargetUnitId?.Trim();
            return string.IsNullOrWhiteSpace(target) ? $"Planned: {action}" : $"Planned: {action} -> {target}";
        }

        private void Hide()
        {
            _summaryText.text = string.Empty;
            _summaryText.gameObject.SetActive(false);
        }

        private static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
