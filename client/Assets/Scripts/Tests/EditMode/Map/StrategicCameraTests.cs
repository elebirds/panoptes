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

        [TearDown]
        public void TearDown()
        {
            var names = new[]
            {
                "StrategicCameraTestCamera",
                "LeftSafeArea_Test",
                "RightSafeArea_Test",
                "AnchorRoot",
                "YawPivot",
                "PitchPivot"
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
        public void TopDownCameraController_ShouldExposeStrategicCameraRuntimeApi()
        {
            var contextType = ResolvePresentationType("Panoptes.Presentation.Map.MapCameraContext");
            Assert.That(contextType, Is.Not.Null, "应新增 MapCameraContext，作为地图相机上下文值对象。");

            var controllerType = ResolvePresentationType("Panoptes.Presentation.Map.TopDownCameraController");
            Assert.That(controllerType, Is.Not.Null, "TopDownCameraController 类型不存在。");
            Assert.That(controllerType!.GetMethod("ApplyCameraContext"), Is.Not.Null,
                "TopDownCameraController 应暴露运行时上下文初始化入口。");
            Assert.That(controllerType.GetMethod("GetAnchorWorldPoint"), Is.Not.Null,
                "TopDownCameraController 应暴露当前地面锚点读取接口。");
            Assert.That(controllerType.GetMethod("TryGetCurrentVisibleGroundBounds"), Is.Not.Null,
                "TopDownCameraController 应能导出当前安全视口投影到地面的可视范围。");
            Assert.That(controllerType.GetMethod("UpdateImmediateForTests"), Is.Not.Null,
                "TopDownCameraController 应提供同步求解入口，便于 EditMode 几何测试。");
        }

        [Test]
        public void TopDownCameraController_ShouldClampVisibleBounds_InsideLargeMapContext()
        {
            var contextType = ResolvePresentationType("Panoptes.Presentation.Map.MapCameraContext")
                              ?? throw new AssertionException("MapCameraContext 类型不存在。");
            var controllerType = ResolvePresentationType("Panoptes.Presentation.Map.TopDownCameraController")
                                 ?? throw new AssertionException("TopDownCameraController 类型不存在。");

            var go = new GameObject("StrategicCameraTestCamera", typeof(Camera));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.aspect = 16f / 9f;

            var controller = go.AddComponent(controllerType);
            Invoke(controller, "SetSafeViewportOverride", new Rect(0f, 0f, 1f, 1f));
            var context = Activator.CreateInstance(
                contextType,
                new object[]
                {
                    new Rect(-0.5f, -0.5f, 120f, 120f),
                    0f,
                    new Vector3(59.5f, 0f, 59.5f)
                });

            Invoke(controller, "ApplyCameraContext", context, true);
            Invoke(controller, "SetZoomNormalized", 0f);
            Invoke(controller, "SetManualTargetPosition", new Vector3(-200f, 0f, -200f), false);
            Invoke(controller, "UpdateImmediateForTests");

            var visible = Invoke(controller, "TryGetCurrentVisibleGroundBounds");
            Assert.That(visible, Is.Not.Null, "TryGetCurrentVisibleGroundBounds 应返回可视地面范围。");

            var resultType = visible!.GetType();
            Assert.That(GetField(resultType, "success", visible), Is.EqualTo(true),
                "大地图边界求解后，应该能拿到合法的可视范围。");

            var rect = (Rect)GetField(resultType, "bounds", visible);
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(-0.5015f));
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(-0.5015f));
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(119.5015f));
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(119.5015f));
        }

        [Test]
        public void TopDownCameraController_ShouldCenterSmallMap_WhenZoomedOut()
        {
            var contextType = ResolvePresentationType("Panoptes.Presentation.Map.MapCameraContext")
                              ?? throw new AssertionException("MapCameraContext 类型不存在。");
            var controllerType = ResolvePresentationType("Panoptes.Presentation.Map.TopDownCameraController")
                                 ?? throw new AssertionException("TopDownCameraController 类型不存在。");

            var go = new GameObject("StrategicCameraTestCamera", typeof(Camera));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.aspect = 16f / 9f;

            var controller = go.AddComponent(controllerType);
            Invoke(controller, "SetSafeViewportOverride", new Rect(0f, 0f, 1f, 1f));
            var context = Activator.CreateInstance(
                contextType,
                new object[]
                {
                    new Rect(-0.5f, -0.5f, 20f, 20f),
                    0f,
                    new Vector3(9.5f, 0f, 9.5f)
                });

            Invoke(controller, "ApplyCameraContext", context, true);
            Invoke(controller, "SetZoomNormalized", 0f);
            Invoke(controller, "UpdateImmediateForTests");

            var anchor = (Vector3)Invoke(controller, "GetAnchorWorldPoint");
            Assert.That(anchor.x, Is.EqualTo(9.5f).Within(0.25f));
            Assert.That(anchor.z, Is.EqualTo(9.5f).Within(0.25f));

            var visible = Invoke(controller, "TryGetCurrentVisibleGroundBounds");
            var resultType = visible!.GetType();
            Assert.That(GetField(resultType, "success", visible), Is.EqualTo(true));

            var rect = (Rect)GetField(resultType, "bounds", visible);
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(-0.5015f));
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(-0.5015f));
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(19.5015f));
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(19.5015f));
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
        public void MapRenderer_ShouldExposeCameraContext_AndStopDirectCameraMutation()
        {
            Assert.That(File.Exists(_mapRendererPath), Is.True, "MapRenderer.cs 不存在。");

            var content = File.ReadAllText(_mapRendererPath);
            StringAssert.Contains("CameraContextReady", content,
                "MapRenderer 应发布 CameraContextReady 事件。");
            StringAssert.Contains("TryGetCameraContext", content,
                "MapRenderer 应暴露当前地图相机上下文读取接口。");
            Assert.That(content, Does.Not.Contain("cam.transform.position ="),
                "MapRenderer 不应继续直接写 Camera.main.transform.position。");
            Assert.That(content, Does.Not.Contain("FocusCameraToCenter"),
                "MapRenderer 不应再保留直接操纵相机的聚焦流程。");
        }

        private static Type ResolvePresentationType(string fullName)
        {
            return Type.GetType($"{fullName}, Panoptes.Presentation");
        }

        private static object Invoke(Component component, string methodName, params object[] args)
        {
            var method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"未找到方法 {methodName}。");
            return method!.Invoke(component, args);
        }

        private static object GetField(Type type, string fieldName, object target)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"未找到字段 {fieldName}。");
            return field!.GetValue(target);
        }

        private static void SetField(Type type, string fieldName, object target, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"未找到字段 {fieldName}。");
            field!.SetValue(target, value);
        }
    }
}
