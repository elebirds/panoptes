using System.Reflection;
using NUnit.Framework;
using Panoptes.Presentation.UI.Domestic;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class BuildCommandPanelTests
    {
        [Test]
        public void ResetBuildListScroll_ForceReset_ShouldNotRebaseToScrolledPosition()
        {
            var panelObject = new GameObject("BuildCommandPanel");
            var listObject = new GameObject("BuildItemList", typeof(RectTransform));

            try
            {
                var panel = panelObject.AddComponent<BuildCommandPanel>();
                var listRoot = listObject.GetComponent<RectTransform>();
                listRoot.anchoredPosition = new Vector2(0f, 240f);

                SetPrivateField(panel, "buildItemListRoot", listRoot);

                InvokePrivateMethod(panel, "ResetBuildListScroll", true);

                Assert.That(listRoot.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
                if (listObject != null)
                {
                    Object.DestroyImmediate(listObject);
                }
            }
        }

        [Test]
        public void RefreshBuildItems_RepeatedRefresh_ShouldReplaceRenderedRowsAndPreserveTemplates()
        {
            var panelObject = new GameObject("BuildCommandPanel", typeof(RectTransform));
            var listObject = new GameObject("BuildItemList", typeof(RectTransform));
            var groupTemplateObject = new GameObject("BuildGroupTemplate", typeof(RectTransform));
            var itemTemplateObject = new GameObject("BuildItemTemplate", typeof(RectTransform), typeof(Image), typeof(Button));

            try
            {
                listObject.transform.SetParent(panelObject.transform, false);
                groupTemplateObject.transform.SetParent(listObject.transform, false);
                itemTemplateObject.transform.SetParent(listObject.transform, false);

                var panel = panelObject.AddComponent<BuildCommandPanel>();
                var listRoot = listObject.GetComponent<RectTransform>();
                var groupTemplate = groupTemplateObject.AddComponent<BuildGroupView>();
                var itemTemplate = itemTemplateObject.AddComponent<BuildItemView>();

                SetPrivateField(panel, "listContent", listRoot);
                SetPrivateField(panel, "buildItemListRoot", listRoot);
                SetPrivateField(panel, "buildGroupPrefab", groupTemplate);
                SetPrivateField(panel, "buildItemPrefab", itemTemplate);
                SetPrivateField(panel, "buildConfigJson", new TextAsset(
                    "{\"buildings\":[{\"id\":\"farm\",\"name\":\"Farm\",\"description\":\"Food\",\"placement_kind\":\"resource_node\",\"required_resource_type\":\"grain\",\"sort_order\":1}]}"));

                panel.RefreshBuildItems();
                panel.RefreshBuildItems();

                Assert.That(listRoot.childCount, Is.EqualTo(4),
                    "Two inactive templates plus one rendered group and one rendered item should remain after refresh.");
                Assert.That(groupTemplateObject.activeSelf, Is.False);
                Assert.That(itemTemplateObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
                if (listObject != null)
                {
                    Object.DestroyImmediate(listObject);
                }
            }
        }

        [Test]
        public void RefreshBuildItems_MissingScrollReferences_ShouldCreateViewportAndContent()
        {
            var panelObject = new GameObject("BuildCommandPanel", typeof(RectTransform));
            var groupTemplateObject = new GameObject("BuildGroupTemplate", typeof(RectTransform));
            var itemTemplateObject = new GameObject("BuildItemTemplate", typeof(RectTransform), typeof(Image), typeof(Button));

            try
            {
                groupTemplateObject.transform.SetParent(panelObject.transform, false);
                itemTemplateObject.transform.SetParent(panelObject.transform, false);

                var panel = panelObject.AddComponent<BuildCommandPanel>();
                groupTemplateObject.AddComponent<BuildGroupView>();
                itemTemplateObject.AddComponent<BuildItemView>();
                SetPrivateField(panel, "buildConfigJson", new TextAsset(
                    "{\"buildings\":[{\"id\":\"farm\",\"name\":\"Farm\",\"description\":\"Food\",\"placement_kind\":\"resource_node\",\"required_resource_type\":\"grain\",\"sort_order\":1}]}"));

                panel.RefreshBuildItems();

                var scrollRect = panelObject.GetComponent<ScrollRect>();
                Assert.That(scrollRect, Is.Not.Null);
                Assert.That(scrollRect.viewport, Is.Not.Null);
                Assert.That(scrollRect.viewport.name, Is.EqualTo("Viewport"));
                Assert.That(scrollRect.content, Is.Not.Null);
                Assert.That(scrollRect.content.name, Is.EqualTo("Content"));
                Assert.That(scrollRect.content.childCount, Is.EqualTo(2),
                    "A rendered group and item should be created under generated content.");
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
            }
        }

        private static void InvokePrivateMethod(object instance, string methodName, params object[] args)
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method: {methodName}");
            method!.Invoke(instance, args);
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
            field!.SetValue(instance, value);
        }

    }
}
