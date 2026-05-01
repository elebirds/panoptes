using Panoptes.Presentation.Map;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class UnitInfoPortraitCameraLifecycle
    {
        public readonly struct Settings
        {
            public Settings(
                bool enabled,
                bool realtime,
                bool keepSceneBackground,
                int textureSize,
                float fov,
                float minDistance,
                float distanceScale,
                float distanceOffset,
                float heightOffset,
                float cameraVerticalOffsetScale,
                bool enableFillLight,
                Color fillLightColor,
                float fillLightIntensity,
                float fillLightRange,
                float fillLightSpotAngle,
                float fillLightVerticalOffset,
                float fillLightForwardOffset)
            {
                Enabled = enabled;
                Realtime = realtime;
                KeepSceneBackground = keepSceneBackground;
                TextureSize = textureSize;
                Fov = fov;
                MinDistance = minDistance;
                DistanceScale = distanceScale;
                DistanceOffset = distanceOffset;
                HeightOffset = heightOffset;
                CameraVerticalOffsetScale = cameraVerticalOffsetScale;
                EnableFillLight = enableFillLight;
                FillLightColor = fillLightColor;
                FillLightIntensity = fillLightIntensity;
                FillLightRange = fillLightRange;
                FillLightSpotAngle = fillLightSpotAngle;
                FillLightVerticalOffset = fillLightVerticalOffset;
                FillLightForwardOffset = fillLightForwardOffset;
            }

            public bool Enabled { get; }
            public bool Realtime { get; }
            public bool KeepSceneBackground { get; }
            public int TextureSize { get; }
            public float Fov { get; }
            public float MinDistance { get; }
            public float DistanceScale { get; }
            public float DistanceOffset { get; }
            public float HeightOffset { get; }
            public float CameraVerticalOffsetScale { get; }
            public bool EnableFillLight { get; }
            public Color FillLightColor { get; }
            public float FillLightIntensity { get; }
            public float FillLightRange { get; }
            public float FillLightSpotAngle { get; }
            public float FillLightVerticalOffset { get; }
            public float FillLightForwardOffset { get; }
        }

        private Camera _portraitCamera;
        private RenderTexture _portraitRenderTexture;
        private Light _portraitFillLight;

        public bool TryRefresh(
            UnitView unit,
            RawImage rawImage,
            bool forceRender,
            Settings settings,
            bool ownerActiveAndEnabled,
            bool panelOpen)
        {
            if (!settings.Enabled || unit == null || rawImage == null)
            {
                Disable();
                return false;
            }

            if (!EnsureCameraAndTexture(rawImage, settings))
            {
                Disable();
                return false;
            }

            if (!UpdateCameraPose(unit, settings))
            {
                Disable();
                return false;
            }

            UpdateEnabledState(rawImage, settings, ownerActiveAndEnabled, panelOpen, unit != null);

            if (_portraitCamera != null &&
                _portraitCamera.targetTexture != null &&
                (!settings.Realtime || forceRender))
            {
                var usePortraitFillLight = _portraitFillLight != null && settings.EnableFillLight;
                if (usePortraitFillLight)
                {
                    _portraitFillLight.enabled = true;
                }

                try
                {
                    _portraitCamera.Render();
                }
                finally
                {
                    if (usePortraitFillLight)
                    {
                        _portraitFillLight.enabled = false;
                    }
                }
            }

            return true;
        }

        public void SetVisible(
            RawImage rawImage,
            Image fallbackIcon,
            bool visible,
            Settings settings,
            bool ownerActiveAndEnabled,
            bool panelOpen,
            bool hasCurrentUnit)
        {
            if (rawImage != null)
            {
                rawImage.enabled = visible;
            }

            if (fallbackIcon != null)
            {
                fallbackIcon.enabled = !visible;
            }

            UpdateEnabledState(rawImage, settings, ownerActiveAndEnabled, panelOpen, hasCurrentUnit);
        }

        public void Disable()
        {
            if (_portraitCamera != null)
            {
                _portraitCamera.enabled = false;
            }

            if (_portraitFillLight != null)
            {
                _portraitFillLight.enabled = false;
            }
        }

        public void Release(RawImage rawImage)
        {
            Disable();

            if (_portraitCamera != null && _portraitCamera.targetTexture == _portraitRenderTexture)
            {
                _portraitCamera.targetTexture = null;
            }

            if (_portraitFillLight != null)
            {
                _portraitFillLight.enabled = false;
                _portraitFillLight = null;
            }

            if (rawImage != null && rawImage.texture == _portraitRenderTexture)
            {
                rawImage.texture = null;
            }

            if (_portraitCamera != null)
            {
                DestroyRuntimeObject(_portraitCamera.gameObject);
                _portraitCamera = null;
            }

            if (_portraitRenderTexture != null)
            {
                _portraitRenderTexture.Release();
                DestroyRuntimeObject(_portraitRenderTexture);
                _portraitRenderTexture = null;
            }
        }

        public void BindRawImageTexture(RawImage rawImage)
        {
            if (rawImage != null)
            {
                rawImage.texture = _portraitRenderTexture;
            }
        }

        private bool EnsureCameraAndTexture(RawImage rawImage, Settings settings)
        {
            if (!settings.Enabled)
            {
                return false;
            }

            var textureSize = Mathf.Clamp(settings.TextureSize, 64, 1024);
            if (_portraitRenderTexture == null ||
                _portraitRenderTexture.width != textureSize ||
                _portraitRenderTexture.height != textureSize)
            {
                if (_portraitCamera != null && _portraitCamera.targetTexture == _portraitRenderTexture)
                {
                    _portraitCamera.targetTexture = null;
                }

                if (_portraitRenderTexture != null)
                {
                    _portraitRenderTexture.Release();
                    DestroyRuntimeObject(_portraitRenderTexture);
                }

                _portraitRenderTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
                {
                    name = "UnitPortraitRT_Runtime",
                    hideFlags = HideFlags.DontSave,
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _portraitRenderTexture.Create();
            }

            if (_portraitCamera == null)
            {
                var cameraGo = new GameObject("UnitPortraitCamera_Runtime", typeof(Camera));
                cameraGo.hideFlags = HideFlags.DontSave;
                _portraitCamera = cameraGo.GetComponent<Camera>();
            }

            if (_portraitCamera == null || _portraitRenderTexture == null)
            {
                return false;
            }

            _portraitCamera.enabled = false;
            _portraitCamera.orthographic = false;
            _portraitCamera.fieldOfView = Mathf.Clamp(settings.Fov, 10f, 80f);
            _portraitCamera.nearClipPlane = 0.03f;
            _portraitCamera.farClipPlane = 500f;
            _portraitCamera.cullingMask = ~0;
            _portraitCamera.targetTexture = _portraitRenderTexture;
            ConfigureCameraClearFlags(settings);
            EnsureFillLight();
            ConfigureFillLight(settings);

            if (rawImage != null)
            {
                rawImage.texture = _portraitRenderTexture;
            }

            return true;
        }

        private void ConfigureCameraClearFlags(Settings settings)
        {
            if (_portraitCamera == null)
            {
                return;
            }

            if (!settings.KeepSceneBackground)
            {
                _portraitCamera.clearFlags = CameraClearFlags.SolidColor;
                _portraitCamera.backgroundColor = Color.clear;
                return;
            }

            if (RenderSettings.skybox != null)
            {
                _portraitCamera.clearFlags = CameraClearFlags.Skybox;
                return;
            }

            _portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            _portraitCamera.backgroundColor = Color.black;
        }

        private void EnsureFillLight()
        {
            if (_portraitCamera == null)
            {
                _portraitFillLight = null;
                return;
            }

            if (_portraitFillLight != null)
            {
                return;
            }

            var fillLightGo = new GameObject("UnitPortraitFillLight_Runtime", typeof(Light));
            fillLightGo.hideFlags = HideFlags.DontSave;
            fillLightGo.transform.SetParent(_portraitCamera.transform, false);
            _portraitFillLight = fillLightGo.GetComponent<Light>();
        }

        private void ConfigureFillLight(Settings settings)
        {
            if (_portraitFillLight == null)
            {
                return;
            }

            _portraitFillLight.enabled = false;
            _portraitFillLight.type = LightType.Spot;
            _portraitFillLight.shadows = LightShadows.None;
            _portraitFillLight.renderMode = LightRenderMode.ForcePixel;
            _portraitFillLight.cullingMask = _portraitCamera != null ? _portraitCamera.cullingMask : ~0;
            _portraitFillLight.color = settings.FillLightColor;
            _portraitFillLight.intensity = Mathf.Max(0f, settings.FillLightIntensity);
            _portraitFillLight.range = Mathf.Max(1f, settings.FillLightRange);
            _portraitFillLight.spotAngle = Mathf.Clamp(settings.FillLightSpotAngle, 15f, 150f);
            _portraitFillLight.innerSpotAngle = Mathf.Clamp(
                _portraitFillLight.spotAngle * 0.65f,
                1f,
                _portraitFillLight.spotAngle - 0.1f);
        }

        private bool UpdateCameraPose(UnitView unit, Settings settings)
        {
            if (_portraitCamera == null || unit == null)
            {
                return false;
            }

            if (!UnitInfoPortraitPresenter.TryComputeUnitBounds(unit, out var bounds))
            {
                return false;
            }

            var visualRoot = unit.VisualRoot != null ? unit.VisualRoot : unit.transform;
            var forward = visualRoot != null ? visualRoot.forward : unit.transform.forward;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = unit.transform.forward;
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            var lookAt = bounds.center + Vector3.up * (bounds.size.y * 0.15f + settings.HeightOffset);
            var distance = Mathf.Max(
                Mathf.Max(0.01f, settings.MinDistance),
                bounds.extents.magnitude * Mathf.Max(0.01f, settings.DistanceScale))
                + Mathf.Max(0f, settings.DistanceOffset);
            var camPos = lookAt - forward * distance +
                         Vector3.up * (bounds.size.y * settings.CameraVerticalOffsetScale);
            var lookDir = lookAt - camPos;
            if (lookDir.sqrMagnitude <= 0.0001f)
            {
                lookDir = forward;
            }

            _portraitCamera.transform.SetPositionAndRotation(
                camPos,
                Quaternion.LookRotation(lookDir.normalized, Vector3.up));
            UpdateFillLightPose(lookAt, settings);
            return true;
        }

        private void UpdateFillLightPose(Vector3 lookAt, Settings settings)
        {
            if (_portraitFillLight == null || _portraitCamera == null)
            {
                return;
            }

            var cameraTransform = _portraitCamera.transform;
            var fillPosition = cameraTransform.position +
                               cameraTransform.up * settings.FillLightVerticalOffset +
                               cameraTransform.forward * settings.FillLightForwardOffset;
            var lightDirection = lookAt - fillPosition;
            if (lightDirection.sqrMagnitude <= 0.0001f)
            {
                lightDirection = cameraTransform.forward;
            }

            _portraitFillLight.transform.SetPositionAndRotation(
                fillPosition,
                Quaternion.LookRotation(lightDirection.normalized, Vector3.up));
        }

        private void UpdateEnabledState(
            RawImage rawImage,
            Settings settings,
            bool ownerActiveAndEnabled,
            bool panelOpen,
            bool hasCurrentUnit)
        {
            if (_portraitCamera == null)
            {
                return;
            }

            var shouldEnable = settings.Enabled &&
                               settings.Realtime &&
                               ownerActiveAndEnabled &&
                               panelOpen &&
                               hasCurrentUnit &&
                               rawImage != null &&
                               rawImage.enabled &&
                               _portraitRenderTexture != null;
            _portraitCamera.enabled = shouldEnable;

            if (_portraitFillLight != null)
            {
                _portraitFillLight.enabled = false;
            }
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
                return;
            }

            Object.DestroyImmediate(target);
        }
    }
}
