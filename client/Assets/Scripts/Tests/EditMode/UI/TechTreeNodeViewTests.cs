using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class TechTreeNodeViewTests
    {
        private const string ViewTypeName = "Panoptes.Presentation.UI.Domestic.TechTreeNodeView, Panoptes.Presentation";
        private const string ModelTypeName = "Panoptes.Presentation.UI.Domestic.TechTreeNodeRenderModel, Panoptes.Presentation";

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [Test]
        public void Bind_ShouldApplyStatusLabel_ProgressText_AndInteractableState()
        {
            var viewType = Type.GetType(ViewTypeName);
            var modelType = Type.GetType(ModelTypeName);
            Assert.That(viewType, Is.Not.Null, "缺少 TechTreeNodeView。");
            Assert.That(modelType, Is.Not.Null, "缺少 TechTreeNodeRenderModel。");

            _root = CreateNodeHierarchy();
            var view = _root.AddComponent(viewType!);

            var clickedTechnologyId = string.Empty;
            var bindMethod = viewType!.GetMethod("Bind", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(bindMethod, Is.Not.Null, "TechTreeNodeView 必须提供 Bind(...)。");

            var model = CreateModel(modelType!, "tech_selected", "研究中", "Researching", 3, 5, false);
            bindMethod!.Invoke(view, new object[] { model, null, new Action<string>(id => clickedTechnologyId = id) });

            var button = _root.GetComponent<Button>();
            var statusText = _root.transform.Find("StatusBadge/StatusBadgeText")?.GetComponent<TextMeshProUGUI>();
            var progressText = _root.transform.Find("ProgressRoot/ProgressText")?.GetComponent<TextMeshProUGUI>();

            Assert.That(button, Is.Not.Null);
            Assert.That(statusText, Is.Not.Null);
            Assert.That(progressText, Is.Not.Null);
            Assert.That(button!.interactable, Is.False, "研究中节点不应可点击。");
            Assert.That(statusText!.text, Is.EqualTo("研究中"));
            Assert.That(progressText!.text, Is.EqualTo("3 / 5"));

            model = CreateModel(modelType!, "tech_available", "可研究", "Available", 0, 2, true);
            bindMethod.Invoke(view, new object[] { model, null, new Action<string>(id => clickedTechnologyId = id) });

            Assert.That(button.interactable, Is.True, "可研究节点必须可点击。");
            Assert.That(statusText.text, Is.EqualTo("可研究"));
            Assert.That(progressText.text, Is.EqualTo("0 / 2"));

            button.onClick.Invoke();
            Assert.That(clickedTechnologyId, Is.EqualTo("tech_available"));
        }

        private static object CreateModel(Type modelType, string technologyId, string statusLabel, string statusEnumName, int currentProgress, int requiredProgress, bool isInteractable)
        {
            var model = Activator.CreateInstance(modelType);
            Assert.That(model, Is.Not.Null);

            SetProperty(modelType, model!, "TechnologyId", technologyId);
            SetProperty(modelType, model, "Title", technologyId);
            SetProperty(modelType, model, "Description", $"{technologyId}_desc");
            SetProperty(modelType, model, "IconKey", $"{technologyId}_icon");
            SetProperty(modelType, model, "StatusLabel", statusLabel);
            SetProperty(modelType, model, "CurrentProgress", currentProgress);
            SetProperty(modelType, model, "RequiredProgress", requiredProgress);
            SetProperty(modelType, model, "IsInteractable", isInteractable);

            var statusProperty = modelType.GetProperty("Status", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(statusProperty, Is.Not.Null, "TechTreeNodeRenderModel 缺少 Status。");
            var statusEnumValue = Enum.Parse(statusProperty!.PropertyType, statusEnumName);
            statusProperty.SetValue(model, statusEnumValue);
            return model;
        }

        private static void SetProperty(Type targetType, object target, string propertyName, object value)
        {
            var property = targetType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"缺少属性 {propertyName}。");
            property!.SetValue(target, value);
        }

        private static GameObject CreateNodeHierarchy()
        {
            var root = CreateUiObject("TechNodeItem", null, typeof(Image), typeof(Button), typeof(Outline));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(420f, 132f);

            CreateUiObject("StateFrame", root.transform, typeof(Image));
            CreateUiObject("Icon", root.transform, typeof(Image));

            var content = CreateUiObject("Content", root.transform);
            CreateText("TitleText", content.transform);
            CreateText("DescriptionText", content.transform);

            var statusBadge = CreateUiObject("StatusBadge", root.transform, typeof(Image));
            CreateText("StatusBadgeText", statusBadge.transform);

            var progressRoot = CreateUiObject("ProgressRoot", root.transform, typeof(Image));
            CreateUiObject("ProgressBackground", progressRoot.transform, typeof(Image));
            var progressFill = CreateUiObject("ProgressFill", progressRoot.transform, typeof(Image));
            progressFill.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
            progressFill.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            progressFill.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            CreateText("ProgressText", progressRoot.transform);

            return root;
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] additionalComponents)
        {
            var types = new Type[additionalComponents.Length + 2];
            types[0] = typeof(RectTransform);
            types[1] = typeof(CanvasRenderer);
            for (var i = 0; i < additionalComponents.Length; i++)
            {
                types[i + 2] = additionalComponents[i];
            }

            var go = new GameObject(name, types);
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 40f);
            return go;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent)
        {
            var go = CreateUiObject(name, parent);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.text = string.Empty;
            return text;
        }
    }
}
