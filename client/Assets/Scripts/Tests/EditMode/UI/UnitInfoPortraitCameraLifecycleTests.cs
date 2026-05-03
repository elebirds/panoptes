using NUnit.Framework;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class UnitInfoPortraitCameraLifecycleTests
    {
        private GameObject _unitRoot;
        private GameObject _uiRoot;

        [TearDown]
        public void TearDown()
        {
            if (_unitRoot != null)
            {
                Object.DestroyImmediate(_unitRoot);
            }

            if (_uiRoot != null)
            {
                Object.DestroyImmediate(_uiRoot);
            }
        }

        [Test]
        public void SetVisible_ShouldTogglePortraitAndFallbackIcon()
        {
            var lifecycle = new UnitInfoPortraitCameraLifecycle();
            var rawImage = CreateRawImage();
            var fallback = CreateFallbackIcon();

            lifecycle.SetVisible(
                rawImage,
                fallback,
                visible: true,
                CreateSettings(),
                ownerActiveAndEnabled: true,
                panelOpen: true,
                hasCurrentUnit: true);

            Assert.That(rawImage.enabled, Is.True);
            Assert.That(fallback.enabled, Is.False);

            lifecycle.SetVisible(
                rawImage,
                fallback,
                visible: false,
                CreateSettings(),
                ownerActiveAndEnabled: true,
                panelOpen: false,
                hasCurrentUnit: false);

            Assert.That(rawImage.enabled, Is.False);
            Assert.That(fallback.enabled, Is.True);
        }

        [Test]
        public void TryRefresh_ShouldBindRenderTextureAndReleaseIt()
        {
            var lifecycle = new UnitInfoPortraitCameraLifecycle();
            var unit = CreateUnitWithRenderer();
            var rawImage = CreateRawImage();

            var refreshed = lifecycle.TryRefresh(
                unit,
                rawImage,
                forceRender: false,
                CreateSettings(),
                ownerActiveAndEnabled: true,
                panelOpen: true);

            Assert.That(refreshed, Is.True);
            Assert.That(rawImage.texture, Is.TypeOf<RenderTexture>());

            lifecycle.Release(rawImage);

            Assert.That(rawImage.texture, Is.Null);
        }

        private UnitView CreateUnitWithRenderer()
        {
            _unitRoot = new GameObject("Unit", typeof(UnitView));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(_unitRoot.transform, false);
            return _unitRoot.GetComponent<UnitView>();
        }

        private RawImage CreateRawImage()
        {
            _uiRoot = new GameObject("RawImage", typeof(RectTransform), typeof(RawImage));
            return _uiRoot.GetComponent<RawImage>();
        }

        private Image CreateFallbackIcon()
        {
            var fallback = new GameObject("FallbackIcon", typeof(RectTransform), typeof(Image));
            fallback.transform.SetParent(_uiRoot.transform, false);
            return fallback.GetComponent<Image>();
        }

        private static UnitInfoPortraitCameraLifecycle.Settings CreateSettings()
        {
            return new UnitInfoPortraitCameraLifecycle.Settings(
                enabled: true,
                realtime: true,
                keepSceneBackground: false,
                textureSize: 128,
                fov: 30f,
                minDistance: 0.9f,
                distanceScale: 1.15f,
                distanceOffset: 0.5f,
                heightOffset: 0.2f,
                cameraVerticalOffsetScale: -0.1f,
                enableFillLight: true,
                fillLightColor: Color.white,
                fillLightIntensity: 1.8f,
                fillLightRange: 18f,
                fillLightSpotAngle: 80f,
                fillLightVerticalOffset: 0.06f,
                fillLightForwardOffset: -0.05f);
        }
    }
}
