using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    internal static class BuildCommandPanelViewResolver
    {
        public static bool Resolve(
            Component owner,
            ref BuildTooltipView tooltipView,
            ref ScrollRect listScrollRect,
            ref RectTransform listContent,
            ref RectTransform buildItemListRoot,
            ref BuildGroupView buildGroupPrefab,
            ref BuildItemView buildItemPrefab)
        {
            if (owner == null)
            {
                return false;
            }

            ResolveTooltipView(owner, ref tooltipView);
            ResolveScrollRect(owner, ref listScrollRect);
            ResolveListContent(owner, ref listScrollRect, ref listContent, ref buildItemListRoot);
            ResolveGroupPrefab(owner, ref buildGroupPrefab);
            ResolveItemPrefab(owner, ref buildItemPrefab);

            return listContent != null && buildGroupPrefab != null && buildItemPrefab != null;
        }

        public static BuildGroupView ResolveGroupPrefab(Component owner, ref BuildGroupView buildGroupPrefab)
        {
            if (buildGroupPrefab != null)
            {
                buildGroupPrefab.gameObject.SetActive(false);
                return buildGroupPrefab;
            }

            if (owner == null)
            {
                return null;
            }

            buildGroupPrefab = owner.GetComponentInChildren<BuildGroupView>(true);
            if (buildGroupPrefab != null)
            {
                buildGroupPrefab.gameObject.SetActive(false);
            }

            return buildGroupPrefab;
        }

        public static BuildItemView ResolveItemPrefab(Component owner, ref BuildItemView buildItemPrefab)
        {
            if (buildItemPrefab != null)
            {
                buildItemPrefab.gameObject.SetActive(false);
                return buildItemPrefab;
            }

            if (owner == null)
            {
                return null;
            }

            var candidates = owner.GetComponentsInChildren<BuildItemView>(true);
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate.GetComponent<BuildGroupView>() != null)
                {
                    continue;
                }

                buildItemPrefab = candidate;
                buildItemPrefab.gameObject.SetActive(false);
                break;
            }

            return buildItemPrefab;
        }

        private static void ResolveTooltipView(Component owner, ref BuildTooltipView tooltipView)
        {
            if (tooltipView != null || owner == null)
            {
                return;
            }

            tooltipView = owner.GetComponentInChildren<BuildTooltipView>(true);
            if (tooltipView == null)
            {
                tooltipView = Object.FindAnyObjectByType<BuildTooltipView>();
            }
        }

        private static void ResolveScrollRect(Component owner, ref ScrollRect listScrollRect)
        {
            if (owner == null)
            {
                return;
            }

            if (listScrollRect == null)
            {
                listScrollRect = owner.GetComponent<ScrollRect>();
            }

            if (listScrollRect == null)
            {
                listScrollRect = owner.gameObject.AddComponent<ScrollRect>();
            }

            listScrollRect.horizontal = false;
            listScrollRect.vertical = true;
            listScrollRect.movementType = ScrollRect.MovementType.Clamped;
            listScrollRect.scrollSensitivity = 20f;
        }

        private static void ResolveListContent(
            Component owner,
            ref ScrollRect listScrollRect,
            ref RectTransform listContent,
            ref RectTransform buildItemListRoot)
        {
            if (owner == null || listScrollRect == null)
            {
                return;
            }

            if (listContent == null && listScrollRect.content != null)
            {
                listContent = listScrollRect.content;
            }

            if (listContent == null && buildItemListRoot != null)
            {
                listContent = buildItemListRoot;
            }

            var viewport = ResolveViewport(owner.transform);
            EnsureViewportComponents(viewport);

            if (listContent == null)
            {
                var existingContent = viewport != null ? viewport.Find("Content") as RectTransform : null;
                listContent = existingContent != null ? existingContent : CreateContentRoot(viewport);
            }

            EnsureContentLayout(listContent);
            buildItemListRoot = listContent;
            listScrollRect.viewport = viewport;
            listScrollRect.content = listContent;
        }

        private static RectTransform ResolveViewport(Transform ownerTransform)
        {
            if (ownerTransform == null)
            {
                return null;
            }

            var viewport = ownerTransform.Find("Viewport") as RectTransform;
            if (viewport != null)
            {
                return viewport;
            }

            var legacyViewport = ownerTransform.Find("OneGroup") as RectTransform;
            if (legacyViewport != null)
            {
                legacyViewport.name = "Viewport";
                return legacyViewport;
            }

            return CreateViewport(ownerTransform);
        }

        private static RectTransform CreateViewport(Transform ownerTransform)
        {
            if (ownerTransform == null)
            {
                return null;
            }

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(ownerTransform, false);
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = new Vector2(0f, -28f);
            viewport.sizeDelta = new Vector2(-20f, -120f);
            return viewport;
        }

        private static void EnsureViewportComponents(RectTransform viewport)
        {
            if (viewport == null)
            {
                return;
            }

            var image = viewport.GetComponent<Image>();
            if (image == null)
            {
                image = viewport.gameObject.AddComponent<Image>();
            }

            image.color = new Color(0.02f, 0.03f, 0.06f, 0.15f);
            var mask = viewport.GetComponent<Mask>();
            if (mask == null)
            {
                mask = viewport.gameObject.AddComponent<Mask>();
            }

            mask.showMaskGraphic = false;
        }

        private static RectTransform CreateContentRoot(RectTransform viewport)
        {
            if (viewport == null)
            {
                return null;
            }

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(-6f, 0f);
            return content;
        }

        private static void EnsureContentLayout(RectTransform content)
        {
            if (content == null)
            {
                return;
            }

            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(0, 0, 0, 12);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
