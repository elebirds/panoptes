using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class StrategicCameraTests
    {
        private readonly string _mapRendererPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/MapRenderer.cs");
        private readonly string _animationQueuePath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Animation/AnimationQueue.cs");
        private readonly string _unitMoveAnimPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Animation/UnitMoveAnim.cs");

        [TearDown]
        public void TearDown()
        {
            var names = new[]
            {
                "LeftSafeArea_Test",
                "RightSafeArea_Test"
            };

            for (var i = 0; i < names.Length; i++)
            {
                var target = GameObject.Find(names[i]);
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        [Test]
        public void CinemachineMapCameraController_ShouldExposeStrategicCameraRuntimeApi()
        {
            var contextType = ResolvePresentationType("Panoptes.Presentation.Map.MapCameraContext");
            Assert.That(contextType, Is.Not.Null, "应新增 MapCameraContext，作为地图相机上下文值对象。");

            var controllerType = ResolvePresentationType("Panoptes.Presentation.Map.CinemachineMapCameraController");
            Assert.That(controllerType, Is.Not.Null, "CinemachineMapCameraController 类型不存在。");
            Assert.That(controllerType!.GetMethod("ApplyCameraContext"), Is.Not.Null,
                "CinemachineMapCameraController 应暴露地图上下文初始化入口。");
        }

        [Test]
        public void CameraSafeAreaRegistry_ShouldCombineRegisteredManualMargins()
        {
            var sourceType = ResolvePresentationType("Panoptes.Presentation.Map.CameraSafeAreaSource")
                             ?? throw new AssertionException("CameraSafeAreaSource 类型不存在。");
            var registryType = ResolvePresentationType("Panoptes.Presentation.Map.CameraSafeAreaRegistry")
                               ?? throw new AssertionException("CameraSafeAreaRegistry 类型不存在。");
            var edgeType = ResolvePresentationType("Panoptes.Presentation.Map.CameraSafeAreaEdge")
                           ?? throw new AssertionException("CameraSafeAreaEdge 类型不存在。");
            var modeType = ResolvePresentationType("Panoptes.Presentation.Map.CameraSafeAreaMeasureMode")
                           ?? throw new AssertionException("CameraSafeAreaMeasureMode 类型不存在。");

            var leftGo = new GameObject("LeftSafeArea_Test");
            var leftSource = leftGo.AddComponent(sourceType);
            SetField(sourceType, "edge", leftSource, Enum.Parse(edgeType, "Left"));
            SetField(sourceType, "measureMode", leftSource, Enum.Parse(modeType, "ManualNormalized"));
            SetField(sourceType, "manualNormalizedSize", leftSource, 0.2f);

            var rightGo = new GameObject("RightSafeArea_Test");
            var rightSource = rightGo.AddComponent(sourceType);
            SetField(sourceType, "edge", rightSource, Enum.Parse(edgeType, "Right"));
            SetField(sourceType, "measureMode", rightSource, Enum.Parse(modeType, "ManualNormalized"));
            SetField(sourceType, "manualNormalizedSize", rightSource, 0.1f);

            var viewport = (Rect)registryType.GetMethod("GetSafeViewportRect", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, null);

            Assert.That(viewport.xMin, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(viewport.xMax, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(viewport.yMin, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(viewport.yMax, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void MapRenderer_ShouldExposeCameraContext_AndStopAutoCreatingTopDownController()
        {
            Assert.That(File.Exists(_mapRendererPath), Is.True, "MapRenderer.cs 不存在。");

            var content = File.ReadAllText(_mapRendererPath);
            StringAssert.Contains("CameraContextReady", content,
                "MapRenderer 应发布 CameraContextReady 事件。");
            StringAssert.Contains("TryGetCameraContext", content,
                "MapRenderer 应暴露当前地图相机上下文读取接口。");
            Assert.That(content, Does.Not.Contain("TopDownCameraController"),
                "MapRenderer 不应继续自动补挂 TopDownCameraController。");
        }

        [Test]
        public void AnimationQueue_ShouldStopReferencingTopDownCameraController()
        {
            Assert.That(File.Exists(_animationQueuePath), Is.True, "AnimationQueue.cs 不存在。");

            var content = File.ReadAllText(_animationQueuePath);
            Assert.That(content, Does.Not.Contain("TopDownCameraController"),
                "AnimationQueue 不应继续查找或启停 TopDownCameraController。");
        }

        [Test]
        public void UnitMoveAnim_ShouldStopMutatingMainCameraTransform()
        {
            Assert.That(File.Exists(_unitMoveAnimPath), Is.True, "UnitMoveAnim.cs 不存在。");

            var content = File.ReadAllText(_unitMoveAnimPath);
            Assert.That(content, Does.Not.Contain("followCamera.transform.position ="),
                "UnitMoveAnim 不应继续直接写相机 Transform。");
        }

        private static Type ResolvePresentationType(string fullName)
        {
            return Type.GetType($"{fullName}, Panoptes.Presentation");
        }

        private static void SetField(Type type, string fieldName, object target, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"未找到字段 {fieldName}。");
            field!.SetValue(target, value);
        }
    }
}
