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

        [SetUp]
        public void SetUp()
        {
            InvokeBuilder("RebuildPrefabs");
            AssetDatabase.Refresh();
        }

        [Test]
        public void Builder_ShouldCreateOverlayPrefabs_InUiAndResourcesPaths()
        {
            AssertPrefab(ErrorToastUiPath, typeof(ErrorToast), "ToastRoot", "ToastRoot/Message");
            AssertPrefab(ErrorToastRuntimePath, typeof(ErrorToast), "ToastRoot", "ToastRoot/Message");
            AssertPrefab(ConfirmDialogUiPath, typeof(ConfirmDialog), "Mask", "PanelRoot", "PanelRoot/TitleText",
                "PanelRoot/MessageText", "PanelRoot/ConfirmButton", "PanelRoot/CancelButton");
            AssertPrefab(ConfirmDialogRuntimePath, typeof(ConfirmDialog), "Mask", "PanelRoot", "PanelRoot/TitleText",
                "PanelRoot/MessageText", "PanelRoot/ConfirmButton", "PanelRoot/CancelButton");
        }

        private static void AssertPrefab(string assetPath, Type componentType, params string[] nodePaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.That(prefab, Is.Not.Null, $"{assetPath} 不存在。");
            Assert.That(prefab.GetComponent(componentType), Is.Not.Null,
                $"{assetPath} 根节点必须挂载 {componentType.Name}。");

            foreach (var nodePath in nodePaths)
            {
                Assert.That(prefab.transform.Find(nodePath), Is.Not.Null,
                    $"{assetPath} 缺少关键节点 {nodePath}。");
            }
        }

        private static void InvokeBuilder(string methodName, BindingFlags flags = BindingFlags.Static | BindingFlags.Public)
        {
            var builderType = Type.GetType(BuilderTypeName);
            Assert.That(builderType, Is.Not.Null, "CommonOverlayPrefabBuilder 类型不存在。");

            var method = builderType.GetMethod(methodName, flags);
            Assert.That(method, Is.Not.Null, $"CommonOverlayPrefabBuilder 缺少方法 {methodName}。");
            method.Invoke(null, null);
        }
    }
}
