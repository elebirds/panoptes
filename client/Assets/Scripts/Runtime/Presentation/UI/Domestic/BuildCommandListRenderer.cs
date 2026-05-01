using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    internal sealed class BuildCommandListRenderer
    {
        private readonly List<Button> _boundButtons = new();
        private readonly List<UnityAction> _boundActions = new();

        public void Render(
            RectTransform listContent,
            BuildGroupView groupPrefab,
            BuildItemView itemPrefab,
            IReadOnlyList<BuildGroupRenderModel> groups,
            Sprite lockedIcon,
            BuildTooltipView tooltipView,
            Func<string, BuildCommandPanel.BuildRule> resolveBuildRule,
            Action<string, BuildCommandPanel.BuildRule> triggerBuild)
        {
            Unbind();
            ClearRenderedItems(listContent, groupPrefab, itemPrefab);
            if (groups == null || groups.Count == 0)
            {
                return;
            }

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var groupModel = groups[groupIndex];
                if (groupModel == null || groupModel.Items.Count == 0)
                {
                    continue;
                }

                var groupView = InstantiateGroupView(listContent, groupPrefab);
                if (groupView == null)
                {
                    continue;
                }

                groupView.Bind(groupModel.Title);
                groupView.gameObject.SetActive(true);

                for (var itemIndex = 0; itemIndex < groupModel.Items.Count; itemIndex++)
                {
                    RenderItem(
                        listContent,
                        itemPrefab,
                        groupModel.Items[itemIndex],
                        lockedIcon,
                        tooltipView,
                        resolveBuildRule,
                        triggerBuild);
                }
            }
        }

        public void Clear(RectTransform listContent, BuildGroupView groupPrefab, BuildItemView itemPrefab)
        {
            Unbind();
            ClearRenderedItems(listContent, groupPrefab, itemPrefab);
        }

        public void Unbind()
        {
            var count = Mathf.Min(_boundButtons.Count, _boundActions.Count);
            for (var i = 0; i < count; i++)
            {
                var button = _boundButtons[i];
                var action = _boundActions[i];
                if (button != null && action != null)
                {
                    button.onClick.RemoveListener(action);
                }
            }

            _boundButtons.Clear();
            _boundActions.Clear();
        }

        private void RenderItem(
            RectTransform listContent,
            BuildItemView itemPrefab,
            BuildItemRenderModel itemModel,
            Sprite lockedIcon,
            BuildTooltipView tooltipView,
            Func<string, BuildCommandPanel.BuildRule> resolveBuildRule,
            Action<string, BuildCommandPanel.BuildRule> triggerBuild)
        {
            if (itemModel == null)
            {
                return;
            }

            var itemView = InstantiateItemView(listContent, itemPrefab);
            if (itemView == null)
            {
                return;
            }

            itemView.Bind(itemModel);
            itemView.SetLocked(itemModel.AvailabilityState == BuildItemAvailabilityState.Locked, lockedIcon);

            var targetBuilding = itemModel.BuildingId;
            var rule = resolveBuildRule != null
                ? resolveBuildRule(targetBuilding)
                : BuildCommandPanel.BuildRule.CityOnly;
            UnityAction action = () => triggerBuild?.Invoke(targetBuilding, rule);
            itemView.SetClickAction(action);
            if (itemView.ClickButton != null)
            {
                _boundButtons.Add(itemView.ClickButton);
                _boundActions.Add(action);
                InstallTooltip(itemView.ClickButton, tooltipView, itemModel.TooltipText);
            }

            itemView.gameObject.SetActive(true);
        }

        private static BuildGroupView InstantiateGroupView(RectTransform listContent, BuildGroupView template)
        {
            if (template == null || listContent == null)
            {
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(template.gameObject, listContent, false);
            instance.name = "BuildGroup";
            return instance.GetComponent<BuildGroupView>();
        }

        private static BuildItemView InstantiateItemView(RectTransform listContent, BuildItemView template)
        {
            if (template == null || listContent == null)
            {
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(template.gameObject, listContent, false);
            instance.name = "BuildItem";
            return instance.GetComponent<BuildItemView>();
        }

        private static void ClearRenderedItems(
            RectTransform listContent,
            BuildGroupView groupPrefab,
            BuildItemView itemPrefab)
        {
            if (listContent == null)
            {
                return;
            }

            for (var i = listContent.childCount - 1; i >= 0; i--)
            {
                var child = listContent.GetChild(i);
                if (child == null || IsTemplateTransform(child, groupPrefab, itemPrefab))
                {
                    continue;
                }

                DestroyUiObject(child.gameObject);
            }
        }

        private static bool IsTemplateTransform(
            Transform child,
            BuildGroupView groupPrefab,
            BuildItemView itemPrefab)
        {
            if (child == null)
            {
                return false;
            }

            return (groupPrefab != null && ReferenceEquals(child, groupPrefab.transform))
                   || (itemPrefab != null && ReferenceEquals(child, itemPrefab.transform));
        }

        private static void DestroyUiObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void InstallTooltip(Button button, BuildTooltipView tooltipView, string text)
        {
            if (button == null)
            {
                return;
            }

            var trigger = button.GetComponent<BuildTooltipTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<BuildTooltipTrigger>();
            }

            if (tooltipView == null || string.IsNullOrWhiteSpace(text))
            {
                trigger.Configure(null, string.Empty);
                return;
            }

            trigger.Configure(tooltipView, text.Trim());
        }
    }
}
