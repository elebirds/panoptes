using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class StrategicCameraTests
    {
        private readonly string _mapRendererPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Map/MapRenderer.cs");
        private readonly string _animationQueuePath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Animation/AnimationQueue.cs");
        private readonly string _unitMoveAnimPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Animation/UnitMoveAnim.cs");

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
            Assert.That(content, Does.Not.Contain("CameraSafeAreaBootstrapper"),
                "MapRenderer 不应继续自动补挂 CameraSafeAreaBootstrapper。");
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
    }
}
