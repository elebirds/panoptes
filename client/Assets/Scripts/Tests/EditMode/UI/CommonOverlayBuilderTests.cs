using System;
using System.Reflection;
using NUnit.Framework;
using Panoptes.Presentation.UI.Common;
using UnityEditor;
using UnityEngine;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class CommonOverlayBuilderTests
    {
        private const string BuilderTypeName = "Panoptes.Editor.CommonOverlayPrefabBuilder, Panoptes.Editor";
        private const string ErrorToastUiPath = "Assets/Prefabs/UI/ErrorToast.prefab";
        private const string ErrorToastRuntimePath = "Assets/Resources/Prefabs/UI/ErrorToast.prefab";
        private const string ConfirmDialogUiPath = "Assets/Prefabs/UI/ConfirmDialog.prefab";
        private const string ConfirmDialogRuntimePath = "Assets/Resources/Prefabs/UI/ConfirmDialog.prefab";

        [Test]
        public void ExistingOverlayPrefabs_ShouldContainRequiredRuntimeNodes()
        {
            AssertPrefab(ErrorToastUiPath, typeof(ErrorToast), "ToastRoot", "ToastRoot/Message");
            AssertPrefab(ErrorToastRuntimePath, typeof(ErrorToast), "ToastRoot", "ToastRoot/Message");
            AssertPrefab(ConfirmDialogUiPath, typeof(ConfirmDialog), "Mask", "PanelRoot", "PanelRoot/TitleText",
                "PanelRoot/MessageText", "PanelRoot/ConfirmButton", "PanelRoot/CancelButton");
            AssertPrefab(ConfirmDialogRuntimePath, typeof(ConfirmDialog), "Mask", "PanelRoot", "PanelRoot/TitleText",
                "PanelRoot/MessageText", "PanelRoot/ConfirmButton", "PanelRoot/CancelButton");
        }

        [Test]
        public void Builder_ShouldCreateOverlayRoots_WithoutWritingProjectAssets()
        {
            var toastRoot = InvokeBuilder<GameObject>("CreateErrorToastPrefabRoot");
            var dialogRoot = InvokeBuilder<GameObject>("CreateConfirmDialogPrefabRoot");

            try
            {
                AssertOverlayRoot(toastRoot, typeof(ErrorToast), "ToastRoot", "ToastRoot/Message");
                AssertOverlayRoot(dialogRoot, typeof(ConfirmDialog), "Mask", "PanelRoot", "PanelRoot/TitleText",
                    "PanelRoot/MessageText", "PanelRoot/ConfirmButton", "PanelRoot/CancelButton");
            }
            finally
            {
                if (toastRoot != null) UnityEngine.Object.DestroyImmediate(toastRoot);
                if (dialogRoot != null) UnityEngine.Object.DestroyImmediate(dialogRoot);
            }
        }

        private static void AssertPrefab(string assetPath, Type componentType, params string[] nodePaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.That(prefab, Is.Not.Null, $"{assetPath} 不存在。");
            AssertOverlayRoot(prefab, componentType, nodePaths);
        }

        private static void AssertOverlayRoot(GameObject root, Type componentType, params string[] nodePaths)
        {
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponent(componentType), Is.Not.Null,
                $"{root.name} 根节点必须挂载 {componentType.Name}。");
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one),
                $"{root.name} 根节点缩放必须保持为 (1,1,1)，否则运行时会整体不可见。");

            foreach (var nodePath in nodePaths)
            {
                Assert.That(root.transform.Find(nodePath), Is.Not.Null,
                    $"{root.name} 缺少关键节点 {nodePath}。");
            }
        }

        private static T InvokeBuilder<T>(string methodName, BindingFlags flags = BindingFlags.Static | BindingFlags.Public)
        {
            var builderType = Type.GetType(BuilderTypeName);
            Assert.That(builderType, Is.Not.Null, "CommonOverlayPrefabBuilder 类型不存在。");

            var method = builderType.GetMethod(methodName, flags);
            Assert.That(method, Is.Not.Null, $"CommonOverlayPrefabBuilder 缺少方法 {methodName}。");
            return (T)method.Invoke(null, null);
        }
    }
}
