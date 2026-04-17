using System.Reflection;
using NUnit.Framework;
using Panoptes.Presentation.UI.Domestic;
using UnityEngine;

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
                SetPrivateField(panel, "_buildListBaseAnchoredPos", Vector2.zero);
                SetPrivateField(panel, "_buildListScrollOffset", 240f);
                SetPrivateField(panel, "_buildListScrollInitialized", true);

                InvokePrivateMethod(panel, "ResetBuildListScroll", true);

                var basePosition = GetPrivateField<Vector2>(panel, "_buildListBaseAnchoredPos");
                var scrollOffset = GetPrivateField<float>(panel, "_buildListScrollOffset");
                var initialized = GetPrivateField<bool>(panel, "_buildListScrollInitialized");

                Assert.That(basePosition.y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(scrollOffset, Is.EqualTo(0f).Within(0.001f));
                Assert.That(listRoot.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(initialized, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
                Object.DestroyImmediate(listObject);
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

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
            return (T)field!.GetValue(instance);
        }
    }
}
