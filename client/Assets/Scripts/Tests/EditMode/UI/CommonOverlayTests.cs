using System;
using System.Reflection;
using NUnit.Framework;
using Panoptes.Presentation.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class CommonOverlayTests
    {
        [TearDown]
        public void TearDown()
        {
            DestroySingleton<ErrorToast>();
            DestroySingleton<ConfirmDialog>();
        }

        [Test]
        public void ErrorToast_Show_ShouldCreateToastRootAndMessage_AndAssignDefaultTmpFont()
        {
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null, "TMP 默认字体未配置。");

            var host = new GameObject("ErrorToastHost");
            try
            {
                host.hideFlags = HideFlags.HideAndDontSave;
                var toast = host.AddComponent<ErrorToast>();

                InvokePublicMethod(toast, "Show", "资源不足", false);

                var canvasGroup = host.GetComponent<CanvasGroup>();
                Assert.That(canvasGroup, Is.Not.Null, "ErrorToast 必须自动挂载 CanvasGroup。");
                Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(canvasGroup.blocksRaycasts, Is.False);

                var toastRoot = host.transform.Find("ToastRoot");
                Assert.That(toastRoot, Is.Not.Null, "ErrorToast 缺少稳定节点 ToastRoot。");

                var message = toastRoot.Find("Message")?.GetComponent<TextMeshProUGUI>();
                Assert.That(message, Is.Not.Null, "ErrorToast 缺少稳定节点 Message。");
                Assert.That(message.text, Is.EqualTo("资源不足"));
                Assert.That(message.font, Is.EqualTo(TMP_Settings.defaultFontAsset),
                    "ErrorToast 运行时创建的 TMP 文本必须显式绑定 TMP 默认字体。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ErrorToast_Hide_ShouldHideCanvasGroup()
        {
            var host = new GameObject("ErrorToastHost");
            try
            {
                host.hideFlags = HideFlags.HideAndDontSave;
                var toast = host.AddComponent<ErrorToast>();

                InvokePublicMethod(toast, "Show", "需要隐藏", false);
                InvokePublicMethod(toast, "Hide");

                var canvasGroup = host.GetComponent<CanvasGroup>();
                Assert.That(canvasGroup, Is.Not.Null, "ErrorToast 必须自动挂载 CanvasGroup。");
                Assert.That(canvasGroup.alpha, Is.EqualTo(0f), "Hide 后应不可见。");
                Assert.That(canvasGroup.blocksRaycasts, Is.False, "Toast 不应阻挡射线。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ErrorToast_Show_ShouldPopulateDistinctVisualState_ForErrorAndSuccess()
        {
            var host = new GameObject("ErrorToastHost");
            try
            {
                host.hideFlags = HideFlags.HideAndDontSave;
                var toast = host.AddComponent<ErrorToast>();

                InvokePublicMethod(toast, "Show", "第一次提示", false);
                var toastRoot = host.transform.Find("ToastRoot");
                var background = toastRoot?.GetComponent<Image>();
                Assert.That(background, Is.Not.Null, "ToastRoot 必须带背景 Image。");
                var errorColor = background.color;
                Assert.That(errorColor.a, Is.GreaterThan(0f), "错误态背景不应为空视觉。");

                InvokePublicMethod(toast, "Show", "第二次提示", true);

                var message = toastRoot?.Find("Message")?.GetComponent<TextMeshProUGUI>();
                Assert.That(message, Is.Not.Null);
                Assert.That(message.text, Is.EqualTo("第二次提示"),
                    "重复 Show 时必须覆盖当前 toast 内容。");
                Assert.That(background.color, Is.Not.EqualTo(errorColor),
                    "success 视觉分支必须与错误分支不同。");
                Assert.That(background.color.a, Is.GreaterThan(0f), "成功态背景不应为空视觉。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ConfirmDialog_Show_ShouldCreateRequiredNodes_AndAssignDefaultTmpFonts()
        {
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null, "TMP 默认字体未配置。");

            var host = new GameObject("ConfirmDialogHost");
            try
            {
                host.hideFlags = HideFlags.HideAndDontSave;
                var dialog = host.AddComponent<ConfirmDialog>();

                InvokePublicMethod(dialog, "Show", "确认退出", "离开当前房间？", null, null);

                var canvasGroup = host.GetComponent<CanvasGroup>();
                Assert.That(canvasGroup, Is.Not.Null, "ConfirmDialog 必须自动挂载 CanvasGroup。");
                Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(canvasGroup.blocksRaycasts, Is.True);

                Assert.That(host.transform.Find("Mask"), Is.Not.Null, "ConfirmDialog 缺少稳定节点 Mask。");
                var panelRoot = host.transform.Find("PanelRoot");
                Assert.That(panelRoot, Is.Not.Null, "ConfirmDialog 缺少稳定节点 PanelRoot。");
                Assert.That(panelRoot.Find("ConfirmButton"), Is.Not.Null, "ConfirmDialog 缺少 ConfirmButton。");
                Assert.That(panelRoot.Find("CancelButton"), Is.Not.Null, "ConfirmDialog 缺少 CancelButton。");

                var title = panelRoot.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
                var message = panelRoot.Find("MessageText")?.GetComponent<TextMeshProUGUI>();
                Assert.That(title, Is.Not.Null, "ConfirmDialog 缺少 TitleText。");
                Assert.That(message, Is.Not.Null, "ConfirmDialog 缺少 MessageText。");
                Assert.That(title.text, Is.EqualTo("确认退出"));
                Assert.That(message.text, Is.EqualTo("离开当前房间？"));

                foreach (var text in host.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    Assert.That(text.font, Is.EqualTo(TMP_Settings.defaultFontAsset),
                        $"动态创建的 TMP 文本 {text.name} 必须显式绑定 TMP 默认字体。");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ConfirmDialog_Buttons_ShouldInvokeMatchingCallbacks_AndHideDialog()
        {
            var host = new GameObject("ConfirmDialogHost");
            try
            {
                host.hideFlags = HideFlags.HideAndDontSave;
                var dialog = host.AddComponent<ConfirmDialog>();
                var confirmCount = 0;
                var cancelCount = 0;

                InvokePublicMethod(dialog, "Show", "确认", "是否继续？",
                    new Action(() => confirmCount++),
                    new Action(() => cancelCount++));

                var panelRoot = host.transform.Find("PanelRoot");
                Assert.That(panelRoot, Is.Not.Null);

                panelRoot.Find("ConfirmButton")?.GetComponent<Button>().onClick.Invoke();
                Assert.That(confirmCount, Is.EqualTo(1));
                Assert.That(cancelCount, Is.EqualTo(0));
                AssertCanvasHidden(host, "点击确认后对话框必须隐藏。");

                InvokePublicMethod(dialog, "Show", "确认", "是否继续？",
                    new Action(() => confirmCount += 10),
                    new Action(() => cancelCount += 10));
                panelRoot.Find("CancelButton")?.GetComponent<Button>().onClick.Invoke();
                Assert.That(confirmCount, Is.EqualTo(1), "取消不应触发确认回调。");
                Assert.That(cancelCount, Is.EqualTo(10), "Show 之后必须覆盖为最新回调。");
                AssertCanvasHidden(host, "点击取消后对话框必须隐藏。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void AssertCanvasHidden(GameObject host, string message)
        {
            var canvasGroup = host.GetComponent<CanvasGroup>();
            Assert.That(canvasGroup, Is.Not.Null, message);
            Assert.That(canvasGroup.alpha, Is.EqualTo(0f), message);
            Assert.That(canvasGroup.blocksRaycasts, Is.False, message);
        }

        private static object InvokePublicMethod(Component component, string methodName, params object[] args)
        {
            var method = ResolveMethod(component.GetType(), methodName, args);
            Assert.That(method, Is.Not.Null,
                $"{component.GetType().Name} 必须公开方法 {FormatMethodSignature(methodName, args)}。");

            return method.Invoke(component, args);
        }

        private static MethodInfo ResolveMethod(Type type, string methodName, object[] args)
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != methodName)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                var matched = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameterType = parameters[i].ParameterType;
                    var argument = args[i];
                    if (argument == null)
                    {
                        if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                        {
                            matched = false;
                            break;
                        }

                        continue;
                    }

                    if (!parameterType.IsInstanceOfType(argument) && argument.GetType() != parameterType)
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return method;
                }
            }

            return null;
        }

        private static string FormatMethodSignature(string methodName, object[] args)
        {
            var formattedArgs = Array.ConvertAll(args, arg => arg?.GetType().Name ?? "null");
            return $"{methodName}({string.Join(", ", formattedArgs)})";
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
    }
}
