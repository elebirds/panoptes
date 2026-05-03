using System;
using System.Reflection;
using NUnit.Framework;
using Panoptes.Presentation.UI.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class CommonOverlayTests
    {
        private const string ErrorToastRuntimePath = "Assets/Resources/Prefabs/UI/ErrorToast.prefab";
        private const string ConfirmDialogRuntimePath = "Assets/Resources/Prefabs/UI/ConfirmDialog.prefab";
        private const string CompositionBootstrapType = "Panoptes.Presentation.Composition.PanoptesCompositionBootstrap, Panoptes.Presentation";

        [TearDown]
        public void TearDown()
        {
            DestroySingleton<ErrorToast>();
            DestroySingleton<ConfirmDialog>();
            DestroySingleton<LoadingOverlay>();
            DestroyNamedObject("Managers");
            DestroyNamedObject("ErrorToast");
            DestroyNamedObject("ConfirmDialog");
        }

        [Test]
        public void ErrorToast_Show_ShouldRenderMessageAndAssignDefaultTmpFont()
        {
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null, "TMP 默认字体未配置。");

            var toast = InstantiateOverlayPrefab<ErrorToast>(ErrorToastRuntimePath);
            try
            {
                toast.Show("insufficient resources", false);

                var canvasGroup = toast.GetComponent<CanvasGroup>();
                Assert.That(canvasGroup, Is.Not.Null);
                Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(canvasGroup.blocksRaycasts, Is.False);

                var toastRoot = toast.transform.Find("ToastRoot");
                var message = toastRoot?.Find("Message")?.GetComponent<TextMeshProUGUI>();
                Assert.That(message, Is.Not.Null);
                Assert.That(message.text, Is.EqualTo("insufficient resources"));
                Assert.That(message.font, Is.EqualTo(TMP_Settings.defaultFontAsset));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(toast.gameObject);
            }
        }

        [Test]
        public void ErrorToast_Hide_ShouldHideCanvasGroup()
        {
            var toast = InstantiateOverlayPrefab<ErrorToast>(ErrorToastRuntimePath);
            try
            {
                toast.Show("hide me", false);
                toast.Hide();

                var canvasGroup = toast.GetComponent<CanvasGroup>();
                Assert.That(canvasGroup, Is.Not.Null);
                Assert.That(canvasGroup.alpha, Is.EqualTo(0f));
                Assert.That(canvasGroup.blocksRaycasts, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(toast.gameObject);
            }
        }

        [Test]
        public void ErrorToast_Show_ShouldUseDistinctVisualState_ForErrorAndSuccess()
        {
            var toast = InstantiateOverlayPrefab<ErrorToast>(ErrorToastRuntimePath);
            try
            {
                toast.Show("first message", false);
                var background = toast.transform.Find("ToastRoot")?.GetComponent<Image>();
                Assert.That(background, Is.Not.Null);
                var errorColor = background.color;
                Assert.That(background.sprite, Is.Not.Null);
                Assert.That(background.type, Is.EqualTo(Image.Type.Sliced));

                toast.Show("second message", true);

                var message = toast.transform.Find("ToastRoot/Message")?.GetComponent<TextMeshProUGUI>();
                Assert.That(message, Is.Not.Null);
                Assert.That(message.text, Is.EqualTo("second message"));
                Assert.That(background.color, Is.Not.EqualTo(errorColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(toast.gameObject);
            }
        }

        [Test]
        public void ConfirmDialog_Show_ShouldRenderRequiredNodes_AndAssignDefaultTmpFonts()
        {
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null, "TMP 默认字体未配置。");

            var dialog = InstantiateOverlayPrefab<ConfirmDialog>(ConfirmDialogRuntimePath);
            try
            {
                dialog.Show("Confirm Exit", "Leave current room?", null, null);

                var canvasGroup = dialog.GetComponent<CanvasGroup>();
                Assert.That(canvasGroup, Is.Not.Null);
                Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(canvasGroup.blocksRaycasts, Is.True);

                var panelRoot = dialog.transform.Find("PanelRoot");
                Assert.That(dialog.transform.Find("Mask"), Is.Not.Null);
                Assert.That(panelRoot, Is.Not.Null);
                Assert.That(panelRoot.Find("ConfirmButton"), Is.Not.Null);
                Assert.That(panelRoot.Find("CancelButton"), Is.Not.Null);

                var title = panelRoot.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
                var message = panelRoot.Find("MessageText")?.GetComponent<TextMeshProUGUI>();
                var panelBackground = panelRoot.GetComponent<Image>();

                Assert.That(title, Is.Not.Null);
                Assert.That(message, Is.Not.Null);
                Assert.That(title.text, Is.EqualTo("Confirm Exit"));
                Assert.That(message.text, Is.EqualTo("Leave current room?"));
                Assert.That(panelBackground, Is.Not.Null);
                Assert.That(panelBackground.sprite, Is.Not.Null);
                Assert.That(panelBackground.type, Is.EqualTo(Image.Type.Sliced));

                foreach (var text in dialog.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    Assert.That(text.font, Is.EqualTo(TMP_Settings.defaultFontAsset));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dialog.gameObject);
            }
        }

        [Test]
        public void ConfirmDialog_Buttons_ShouldInvokeMatchingCallbacks_AndHideDialog()
        {
            var dialog = InstantiateOverlayPrefab<ConfirmDialog>(ConfirmDialogRuntimePath);
            try
            {
                var confirmCount = 0;
                var cancelCount = 0;

                dialog.Show("Confirm", "Continue?",
                    () => confirmCount++,
                    () => cancelCount++);

                var panelRoot = dialog.transform.Find("PanelRoot");
                Assert.That(panelRoot, Is.Not.Null);

                panelRoot.Find("ConfirmButton")?.GetComponent<Button>().onClick.Invoke();
                Assert.That(confirmCount, Is.EqualTo(1));
                Assert.That(cancelCount, Is.EqualTo(0));
                AssertCanvasHidden(dialog.gameObject);

                dialog.Show("Confirm", "Continue?",
                    () => confirmCount += 10,
                    () => cancelCount += 10);
                panelRoot.Find("CancelButton")?.GetComponent<Button>().onClick.Invoke();
                Assert.That(confirmCount, Is.EqualTo(1));
                Assert.That(cancelCount, Is.EqualTo(10));
                AssertCanvasHidden(dialog.gameObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dialog.gameObject);
            }
        }

        [Test]
        public void AppManager_Bootstrap_ShouldPlaceCommonOverlays_OutsideManagersCanvasGroup()
        {
            var managers = new GameObject("Managers");
            try
            {
                managers.hideFlags = HideFlags.HideAndDontSave;
                managers.AddComponent<CanvasGroup>().alpha = 0f;

                InvokeProjectOverlayBootstrap<ErrorToast>("ErrorToast", "Prefabs/UI/ErrorToast");
                InvokeProjectOverlayBootstrap<ConfirmDialog>("ConfirmDialog", "Prefabs/UI/ConfirmDialog");

                var toastObject = GameObject.Find("ErrorToast");
                var dialogObject = GameObject.Find("ConfirmDialog");
                Assert.That(toastObject, Is.Not.Null);
                Assert.That(dialogObject, Is.Not.Null);

                var toast = toastObject.GetComponent<ErrorToast>();
                var dialog = dialogObject.GetComponent<ConfirmDialog>();
                InvokeLifecycle(toast, "Awake");
                InvokeLifecycle(dialog, "Awake");

                Assert.That(
                    typeof(ErrorToast).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static),
                    Is.Null);
                Assert.That(
                    typeof(ConfirmDialog).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static),
                    Is.Null);
                Assert.That(toast.transform.parent, Is.Null);
                Assert.That(dialog.transform.parent, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managers);
            }
        }

        private static T InstantiateOverlayPrefab<T>(string assetPath) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.That(prefab, Is.Not.Null, $"{assetPath} 不存在。");

            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            var component = instance.GetComponent<T>();
            Assert.That(component, Is.Not.Null, $"{assetPath} 缺少 {typeof(T).Name}。");
            InvokeLifecycle(component, "Awake");
            return component;
        }

        private static void AssertCanvasHidden(GameObject host)
        {
            var canvasGroup = host.GetComponent<CanvasGroup>();
            Assert.That(canvasGroup, Is.Not.Null);
            Assert.That(canvasGroup.alpha, Is.EqualTo(0f));
            Assert.That(canvasGroup.blocksRaycasts, Is.False);
        }

        private static object InvokePrivateStaticMethod(string typeName, string methodName, params object[] args)
        {
            var type = Type.GetType(typeName);
            Assert.That(type, Is.Not.Null, $"{typeName} 类型不存在。");

            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{typeName} 缺少私有静态方法 {methodName}。");
            return method.Invoke(null, args);
        }

        private static void InvokeProjectOverlayBootstrap<T>(string objectName, string resourcePath) where T : Component
        {
            var type = Type.GetType(CompositionBootstrapType);
            Assert.That(type, Is.Not.Null, $"{CompositionBootstrapType} 类型不存在。");

            var method = type.GetMethod("EnsureProjectOverlay", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{CompositionBootstrapType} 缺少私有静态方法 EnsureProjectOverlay。");
            var overlay = method.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { objectName, resourcePath });
            Assert.That(overlay, Is.Not.Null);
        }

        private static void InvokeLifecycle(Component component, string methodName)
        {
            var method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{component.GetType().Name} 缺少生命周期方法 {methodName}。");
            method.Invoke(component, null);
        }

        private static void DestroySingleton<T>() where T : Component
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<T>();
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var field = typeof(T).GetField("<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }

        private static void DestroyNamedObject(string objectName)
        {
            var found = GameObject.Find(objectName);
            if (found != null)
            {
                UnityEngine.Object.DestroyImmediate(found);
            }
        }
    }
}
