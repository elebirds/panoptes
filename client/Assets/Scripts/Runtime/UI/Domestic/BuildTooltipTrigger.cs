/*************************************************
 * Project: Panoptes
 * File: BuildTooltipTrigger.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Hover trigger for build button tooltip.
 *************************************************/

using UnityEngine;
using UnityEngine.EventSystems;

namespace Panoptes.Runtime.UI.Domestic
{
    public sealed class BuildTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private BuildTooltipView tooltipView;
        [TextArea] [SerializeField] private string tooltipText;
        [SerializeField] private bool followPointer = false;
        [SerializeField] private bool forcePointerPosition = false;

        private RectTransform _selfRect;

        private void Awake()
        {
            _selfRect = transform as RectTransform;
        }

        public void Configure(BuildTooltipView view, string text)
        {
            tooltipView = view;
            tooltipText = text;
            followPointer = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipView == null || string.IsNullOrWhiteSpace(tooltipText))
            {
                return;
            }

            if (forcePointerPosition)
            {
                tooltipView.Show(tooltipText, eventData.position);
                return;
            }

            if (_selfRect != null)
            {
                tooltipView.ShowAbove(tooltipText, _selfRect, eventData != null ? eventData.enterEventCamera : null);
                return;
            }

            tooltipView.Show(tooltipText, eventData.position);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!followPointer || tooltipView == null)
            {
                return;
            }

            if (forcePointerPosition)
            {
                tooltipView.Move(eventData.position);
                return;
            }

            if (_selfRect != null)
            {
                tooltipView.MoveAbove(_selfRect, eventData != null ? eventData.enterEventCamera : null);
                return;
            }

            tooltipView.Move(eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipView != null)
            {
                tooltipView.Hide();
            }
        }
    }
}
