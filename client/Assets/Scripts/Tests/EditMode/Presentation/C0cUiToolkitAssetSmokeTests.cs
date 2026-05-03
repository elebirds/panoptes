using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Panoptes.Presentation.Binders.UiToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class C0cUiToolkitAssetSmokeTests
    {
        private readonly List<UnityEngine.Object> _cleanup = new();

        private static readonly string[] SharedManagementPanelNames =
        {
            ManagementPanelUiToolkitRenderer.RootName,
            ManagementPanelUiToolkitRenderer.TitleName,
            ManagementPanelUiToolkitRenderer.EmptyName,
            ManagementPanelUiToolkitRenderer.GroupsName
        };

        private static IEnumerable<PrefabCase> AuthoredPrefabCases
        {
            get
            {
                yield return new PrefabCase(
                    "Prefabs/UI/ManagementHost",
                    typeof(ManagementHostUiToolkitBinder),
                    "Assets/UI/Toolkit/Management/ManagementHost.uxml",
                    "Assets/UI/Toolkit/Management/ManagementHost.uss",
                    new[]
                    {
                        ManagementHostUiToolkitBinder.RootName,
                        ManagementHostUiToolkitBinder.TitleName,
                        ManagementHostUiToolkitBinder.CloseButtonName,
                        ManagementHostUiToolkitBinder.OverviewButtonName,
                        ManagementHostUiToolkitBinder.TurnSummaryButtonName,
                        ManagementHostUiToolkitBinder.LedgerButtonName,
                        ManagementHostUiToolkitBinder.TechButtonName,
                        ManagementHostUiToolkitBinder.BuildButtonName,
                        ManagementHostUiToolkitBinder.RecipeButtonName,
                        ManagementHostUiToolkitBinder.PolicyButtonName,
                        ManagementHostUiToolkitBinder.OverviewPanelName,
                        ManagementHostUiToolkitBinder.TurnValueName,
                        ManagementHostUiToolkitBinder.PhaseValueName,
                        ManagementHostUiToolkitBinder.TokensValueName,
                        ManagementHostUiToolkitBinder.ResearchValueName,
                        ManagementHostUiToolkitBinder.PolicyValueName,
                        ManagementHostUiToolkitBinder.MetricsName,
                        ManagementHostUiToolkitBinder.ResourcesName,
                        ManagementHostUiToolkitBinder.EventsName,
                        ManagementHostUiToolkitBinder.EmptyEventsName
                    });
                yield return new PrefabCase(
                    "Prefabs/UI/BuildCatalog",
                    typeof(BuildCatalogUiToolkitBinder),
                    "Assets/UI/Toolkit/Management/BuildCatalog.uxml",
                    "Assets/UI/Toolkit/Management/BuildCatalog.uss",
                    new[]
                    {
                        BuildCatalogUiToolkitBinder.RootName,
                        BuildCatalogUiToolkitBinder.TitleName,
                        BuildCatalogUiToolkitBinder.EmptyName,
                        BuildCatalogUiToolkitBinder.GroupsName
                    });
                yield return new PrefabCase(
                    "Prefabs/UI/TechTree",
                    typeof(TechTreeUiToolkitBinder),
                    "Assets/UI/Toolkit/Management/TechTree.uxml",
                    "Assets/UI/Toolkit/Management/TechTree.uss",
                    SharedManagementPanelNames);
                yield return new PrefabCase(
                    "Prefabs/UI/PolicyFocus",
                    typeof(PolicyFocusUiToolkitBinder),
                    "Assets/UI/Toolkit/Management/PolicyFocus.uxml",
                    "Assets/UI/Toolkit/Management/PolicyFocus.uss",
                    SharedManagementPanelNames);
                yield return new PrefabCase(
                    "Prefabs/UI/NationalLedger",
                    typeof(NationalLedgerUiToolkitBinder),
                    "Assets/UI/Toolkit/Management/NationalLedger.uxml",
                    "Assets/UI/Toolkit/Management/NationalLedger.uss",
                    SharedManagementPanelNames);
                yield return new PrefabCase(
                    "Prefabs/UI/RecipeSynthesis",
                    typeof(RecipeSynthesisUiToolkitBinder),
                    "Assets/UI/Toolkit/Management/RecipeSynthesis.uxml",
                    "Assets/UI/Toolkit/Management/RecipeSynthesis.uss",
                    SharedManagementPanelNames);
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _cleanup.Count; i++)
            {
                if (_cleanup[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_cleanup[i]);
                }
            }

            _cleanup.Clear();
        }

        [TestCaseSource(nameof(AuthoredPrefabCases))]
        public void AuthoredManagementPrefabs_ShouldLoadBoundUiToolkitAssets(PrefabCase testCase)
        {
            var prefab = Resources.Load<GameObject>(testCase.ResourcePath);
            Assert.That(prefab, Is.Not.Null, testCase.ResourcePath + " must exist under Assets/Resources.");

            var instance = UnityEngine.Object.Instantiate(prefab);
            _cleanup.Add(instance);

            var binder = instance.GetComponent(testCase.BinderType);
            Assert.That(binder, Is.Not.Null, testCase.ResourcePath + " must carry " + testCase.BinderType.Name + ".");
            InvokeUnityMessage(binder, "Awake");
            InvokeUnityMessage(binder, "OnEnable");

            var visualTreeAsset = AssertSerializedAsset<VisualTreeAsset>(binder, "visualTreeAsset");
            var styleSheet = AssertSerializedAsset<StyleSheet>(binder, "styleSheet");
            Assert.That(AssetDatabase.GetAssetPath(visualTreeAsset), Is.EqualTo(testCase.VisualTreePath));
            Assert.That(AssetDatabase.GetAssetPath(styleSheet), Is.EqualTo(testCase.StyleSheetPath));

            var document = instance.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null, testCase.ResourcePath + " must provide a UIDocument.");
            Assert.That(document.panelSettings, Is.Not.Null, testCase.ResourcePath + " must initialize PanelSettings in EditMode.");
            Assert.That(document.rootVisualElement, Is.Not.Null);

            for (var i = 0; i < testCase.RequiredElementNames.Length; i++)
            {
                AssertNamedElement(document.rootVisualElement, testCase.RequiredElementNames[i], testCase.ResourcePath);
            }
        }

        [Test]
        public void TurnSummaryUxml_ShouldExposeBinderContractNames()
        {
            AssertVisualTreeAssetContains(
                "Assets/UI/Toolkit/Turn/TurnSummary.uxml",
                "Assets/UI/Toolkit/Turn/TurnSummary.uss",
                TurnSummaryUiToolkitBinder.RootName,
                TurnSummaryUiToolkitBinder.TitleName,
                TurnSummaryUiToolkitBinder.TurnValueName,
                TurnSummaryUiToolkitBinder.PhaseValueName,
                TurnSummaryUiToolkitBinder.TokensValueName,
                TurnSummaryUiToolkitBinder.NodesValueName,
                TurnSummaryUiToolkitBinder.UnitsValueName,
                TurnSummaryUiToolkitBinder.EventsListName,
                TurnSummaryUiToolkitBinder.EmptyEventsName);
        }

        private static void AssertVisualTreeAssetContains(
            string visualTreePath,
            string styleSheetPath,
            params string[] requiredElementNames)
        {
            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(visualTreePath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);
            Assert.That(visualTreeAsset, Is.Not.Null, visualTreePath + " must exist.");
            Assert.That(styleSheet, Is.Not.Null, styleSheetPath + " must exist.");

            var root = new VisualElement();
            visualTreeAsset.CloneTree(root);
            for (var i = 0; i < requiredElementNames.Length; i++)
            {
                AssertNamedElement(root, requiredElementNames[i], visualTreePath);
            }
        }

        private static TObject AssertSerializedAsset<TObject>(Component component, string fieldName)
            where TObject : UnityEngine.Object
        {
            var field = FindInstanceField(component.GetType(), fieldName);
            Assert.That(field, Is.Not.Null, component.GetType().Name + " must serialize " + fieldName + ".");
            var value = field.GetValue(component) as TObject;
            Assert.That(value, Is.Not.Null, component.GetType().Name + "." + fieldName + " must be assigned.");
            return value;
        }

        private static FieldInfo FindInstanceField(Type type, string fieldName)
        {
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static void InvokeUnityMessage(Component component, string methodName)
        {
            var method = FindInstanceMethod(component.GetType(), methodName);
            if (method != null)
            {
                method.Invoke(component, Array.Empty<object>());
            }
        }

        private static MethodInfo FindInstanceMethod(Type type, string methodName)
        {
            while (type != null)
            {
                var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static void AssertNamedElement(VisualElement root, string elementName, string owner)
        {
            Assert.That(root.Q<VisualElement>(elementName), Is.Not.Null, owner + " is missing UI Toolkit element '" + elementName + "'.");
        }

        public readonly struct PrefabCase
        {
            public PrefabCase(
                string resourcePath,
                Type binderType,
                string visualTreePath,
                string styleSheetPath,
                string[] requiredElementNames)
            {
                ResourcePath = resourcePath;
                BinderType = binderType;
                VisualTreePath = visualTreePath;
                StyleSheetPath = styleSheetPath;
                RequiredElementNames = requiredElementNames;
            }

            public string ResourcePath { get; }

            public Type BinderType { get; }

            public string VisualTreePath { get; }

            public string StyleSheetPath { get; }

            public string[] RequiredElementNames { get; }

            public override string ToString()
            {
                return ResourcePath;
            }
        }
    }
}
