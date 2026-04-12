/*************************************************
 * Project: Panoptes
 * File: TopDownCameraController.cs
 * Author: Panoptes Team
 * Date: 2026-04-05
 * Description: RTS-style top-down camera controls.
 *************************************************/

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Panoptes.Presentation.Map
{
    [DisallowMultipleComponent]
    public sealed class TopDownCameraController : MonoBehaviour
    {
        [Header("Time")]
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Pan - Drag")]
        [SerializeField] private bool enableDragPan = true;
        [SerializeField] private bool invertDrag = true;
        [SerializeField] private float dragPanSpeed = 0.02f; // world units per pixel

        [Header("Pan - Keyboard")]
        [SerializeField] private bool enableKeyboardPan = true;
        [SerializeField] private bool enableArrowKeyPan = true;
        [SerializeField] private float keyboardPanSpeed = 14f; // world units / second

        [Header("Pan - Edge Scroll")]
        [SerializeField] private bool enableEdgeScroll = false;
        [SerializeField] private bool edgeScrollWhileDragging = false;
        [SerializeField] private float edgeThreshold = 24f; // px
        [SerializeField] private float edgePanSpeed = 14f;  // world units / second

        [Header("Movement Smoothing")]
        [SerializeField] private float moveSmoothTime = 0.08f;

        [Header("Zoom")]
        [SerializeField] private float scrollZoomSpeed = 4f;
        [SerializeField] private float inputSystemScrollScale = 0.01f;
        [SerializeField] private float scrollZoomMultiplier = 3f;
        [SerializeField] private float scrollSensitivity = 2f;
        [SerializeField] private float maxZoomStepPerFrame = 1.2f;
        [SerializeField] private bool invertScrollDirection = false;
        [SerializeField] private bool perspectiveZoomByFov = true;
        [SerializeField] private bool alsoDollyWhenPerspectiveZoomByFov = true;
        [SerializeField] private float perspectiveDollySpeed = 2.2f;
        [SerializeField] private float minOrthoSize = 5f;
        [SerializeField] private float maxOrthoSize = 28f;
        [SerializeField] private float minFov = 25f;
        [SerializeField] private float maxFov = 60f;
        [SerializeField] private float minHeight = 1.5f;
        [SerializeField] private float maxHeight = 60f;

        [Header("Zoom - Perspective Tilt")]
        [SerializeField] private bool enableZoomTiltInPerspective = true;
        [SerializeField] private bool keepGroundFocusWhenTilt = true;
        [SerializeField] private float farZoomPitch = 58f;
        [SerializeField] private float nearZoomPitch = 24f;
        [SerializeField] private float tiltSmoothTime = 0.12f;
        
        [Header("Start Pose Guard")]
        [SerializeField] private bool autoFixInvalidStartPose = true;
        [SerializeField] private float fallbackStartHeight = 14f;
        [SerializeField] private float fallbackStartPitch = 50f;

        [Header("Bounds")]
        [SerializeField] private bool clampToBounds = true;
        [SerializeField] private Vector2 xBounds = new Vector2(0f, 19f);
        [SerializeField] private Vector2 zBounds = new Vector2(0f, 19f);
        [SerializeField] private float boundsPadding = 0f;
        [SerializeField] private float boundsLeftPadding = 0f;
        [SerializeField] private float boundsRightPadding = 0f;
        [SerializeField] private float boundsBottomPadding = 0f;
        [SerializeField] private float boundsTopPadding = 0f;
        [SerializeField] private float boundaryDamping = 14f;
        [SerializeField] private bool useViewportGroundBounds = true;
        [SerializeField] private float boundsGroundY = 0f;

        private Camera _camera;
        private Vector3 _targetPosition;
        private Vector3 _moveVelocity;
        private float _tiltPitchVelocity;
        private bool _dragging;
        private Vector3 _lastMousePosition;
        private bool _hasMouse;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void OnEnable()
        {
            EnsureZoomRanges();
            ApplyStartPoseGuard();
            _targetPosition = transform.position;
            ClampCurrentZoomToRange();
        }

        private void Update()
        {
            if (_camera == null)
            {
                return;
            }

            _hasMouse = HasMouse();
            if (!_hasMouse)
            {
                return;
            }

            var dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            EnsureZoomRanges();
            ClampCurrentZoomToRange();
            HandleZoom();
            HandlePerspectiveZoomTilt(dt);

            var dragDelta = GetDragPanDelta();
            var keyboardDelta = GetKeyboardPanDelta(dt);
            var edgeDelta = GetEdgePanDelta(dt);
            var panDelta = dragDelta + keyboardDelta + edgeDelta;
            _targetPosition += panDelta;

            if (clampToBounds)
            {
                ApplyBounds(dt, panDelta.sqrMagnitude > 0.000001f);
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                _targetPosition,
                ref _moveVelocity,
                moveSmoothTime,
                Mathf.Infinity,
                dt);
        }

        public void SetWorldBounds(float minX, float maxX, float minZ, float maxZ, float padding = 0f)
        {
            xBounds = new Vector2(minX, maxX);
            zBounds = new Vector2(minZ, maxZ);
            boundsPadding = padding;
        }

        public void SetBoundsGroundY(float y)
        {
            boundsGroundY = y;
        }

        public void SnapTargetToCurrentPosition()
        {
            _targetPosition = transform.position;
            _moveVelocity = Vector3.zero;
        }

        public void SetManualTargetPosition(Vector3 position, bool snapInstantly = false)
        {
            _targetPosition = position;
            if (snapInstantly)
            {
                transform.position = position;
                _moveVelocity = Vector3.zero;
            }
        }

        // ===== Zoom Runtime API (for UI panel bindings) =====
        // Suggested slider convention: 0 = far, 1 = near.
        public void SetZoomNormalized(float normalized)
        {
            EnsureZoomRanges();
            var t = Mathf.Clamp01(normalized);

            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
                if (_camera == null)
                {
                    _camera = Camera.main;
                }
            }

            if (_camera == null)
            {
                return;
            }

            if (_camera.orthographic)
            {
                _camera.orthographicSize = Mathf.Lerp(maxOrthoSize, minOrthoSize, t);
            }
            else if (perspectiveZoomByFov)
            {
                _camera.fieldOfView = Mathf.Lerp(maxFov, minFov, t);
            }
            else
            {
                _targetPosition = new Vector3(
                    _targetPosition.x,
                    Mathf.Lerp(maxHeight, minHeight, t),
                    _targetPosition.z);
            }
        }

        public float GetZoomNormalized()
        {
            EnsureZoomRanges();

            if (_camera == null)
            {
                return 0f;
            }

            if (_camera.orthographic)
            {
                var denom = Mathf.Max(0.0001f, maxOrthoSize - minOrthoSize);
                return Mathf.Clamp01((maxOrthoSize - _camera.orthographicSize) / denom);
            }

            if (perspectiveZoomByFov)
            {
                var denom = Mathf.Max(0.0001f, maxFov - minFov);
                return Mathf.Clamp01((maxFov - _camera.fieldOfView) / denom);
            }

            var heightDenom = Mathf.Max(0.0001f, maxHeight - minHeight);
            return Mathf.Clamp01((maxHeight - _targetPosition.y) / heightDenom);
        }

        public void SetScrollZoomSpeed(float value)
        {
            scrollZoomSpeed = Mathf.Max(0f, value);
        }

        public void SetInputSystemScrollScale(float value)
        {
            inputSystemScrollScale = Mathf.Max(0f, value);
        }

        public void SetScrollZoomMultiplier(float value)
        {
            scrollZoomMultiplier = Mathf.Max(0f, value);
        }

        public void SetInvertScrollDirection(bool enabled)
        {
            invertScrollDirection = enabled;
        }

        public void SetPerspectiveZoomByFov(bool enabled)
        {
            perspectiveZoomByFov = enabled;
            ClampCurrentZoomToRange();
        }

        public void SetAlsoDollyWhenPerspectiveZoomByFov(bool enabled)
        {
            alsoDollyWhenPerspectiveZoomByFov = enabled;
        }

        public void SetPerspectiveDollySpeed(float value)
        {
            perspectiveDollySpeed = Mathf.Max(0f, value);
        }

        public void SetZoomTiltEnabled(bool enabled)
        {
            enableZoomTiltInPerspective = enabled;
        }

        public void SetZoomTiltPitchRange(float farPitchValue, float nearPitchValue)
        {
            farZoomPitch = Mathf.Clamp(farPitchValue, -89f, 89f);
            nearZoomPitch = Mathf.Clamp(nearPitchValue, -89f, 89f);
        }

        public void SetZoomTiltSmoothTime(float value)
        {
            tiltSmoothTime = Mathf.Max(0.01f, value);
        }

        public void SetKeepGroundFocusWhenTilt(bool enabled)
        {
            keepGroundFocusWhenTilt = enabled;
        }

        public void SetMinOrthoSize(float value)
        {
            minOrthoSize = value;
            EnsureZoomRanges();
            ClampCurrentZoomToRange();
        }

        public void SetMaxOrthoSize(float value)
        {
            maxOrthoSize = value;
            EnsureZoomRanges();
            ClampCurrentZoomToRange();
        }

        public void SetMinFov(float value)
        {
            minFov = value;
            EnsureZoomRanges();
            ClampCurrentZoomToRange();
        }

        public void SetMaxFov(float value)
        {
            maxFov = value;
            EnsureZoomRanges();
            ClampCurrentZoomToRange();
        }

        public void SetMinHeight(float value)
        {
            minHeight = value;
            EnsureZoomRanges();
            ClampCurrentZoomToRange();
        }

        public void SetMaxHeight(float value)
        {
            maxHeight = value;
            EnsureZoomRanges();
            ClampCurrentZoomToRange();
        }

        private void HandleZoom()
        {
            var rawScroll = GetScrollDeltaY();
            if (Mathf.Abs(rawScroll) <= 0.0001f)
            {
                return;
            }

            // Normalize wheel burst values (Windows often reports +/-120) into stable per-frame input.
            var normalizedScroll = Mathf.Clamp(rawScroll, -1f, 1f);
            var zoomDelta = normalizedScroll * scrollZoomMultiplier * Mathf.Max(0f, scrollSensitivity) * (invertScrollDirection ? -1f : 1f);
            zoomDelta = Mathf.Clamp(zoomDelta, -Mathf.Max(0.01f, maxZoomStepPerFrame), Mathf.Max(0.01f, maxZoomStepPerFrame));
            if (Mathf.Abs(zoomDelta) <= 0.0001f)
            {
                return;
            }

            if (_camera.orthographic)
            {
                var current = _camera.orthographicSize;
                var next = Mathf.Clamp(current - zoomDelta * scrollZoomSpeed, minOrthoSize, maxOrthoSize);
                if (Mathf.Abs(next - current) <= 0.0001f)
                {
                    return;
                }

                _camera.orthographicSize = next;
                return;
            }

            if (perspectiveZoomByFov)
            {
                var currentFov = _camera.fieldOfView;
                var nextFov = Mathf.Clamp(currentFov - zoomDelta * scrollZoomSpeed, minFov, maxFov);
                var appliedFovDelta = currentFov - nextFov;
                var hasFovChange = Mathf.Abs(appliedFovDelta) > 0.0001f;
                if (hasFovChange)
                {
                    _camera.fieldOfView = nextFov;
                }

                if (alsoDollyWhenPerspectiveZoomByFov && perspectiveDollySpeed > 0f)
                {
                    // If FOV is clamped, still allow a controlled dolly so forward scroll doesn't "die".
                    var appliedZoomDelta = hasFovChange
                        ? (appliedFovDelta / Mathf.Max(0.0001f, scrollZoomSpeed))
                        : zoomDelta;
                    var desiredMove = transform.forward * (appliedZoomDelta * perspectiveDollySpeed);

                    var currentY = _targetPosition.y;
                    var desiredY = currentY + desiredMove.y;
                    var clampedY = Mathf.Clamp(desiredY, minHeight, maxHeight);
                    var yDenom = desiredY - currentY;
                    var yRatio = Mathf.Abs(yDenom) > 0.0001f
                        ? Mathf.Clamp01((clampedY - currentY) / yDenom)
                        : 1f;

                    _targetPosition += desiredMove * yRatio;
                    _targetPosition = new Vector3(_targetPosition.x, clampedY, _targetPosition.z);
                }

                if (!hasFovChange)
                {
                    // FOV reached boundary and no dolly effect available: hard-stop.
                    return;
                }

                return;
            }

            // Alternate perspective zoom: move camera along Y.
            var targetY = _targetPosition.y;
            var desiredTargetY = targetY + (-zoomDelta * scrollZoomSpeed);
            var nextTargetY = Mathf.Clamp(desiredTargetY, minHeight, maxHeight);
            if (Mathf.Abs(nextTargetY - targetY) <= 0.0001f)
            {
                // At zoom limit: block.
                return;
            }

            _targetPosition = new Vector3(_targetPosition.x, nextTargetY, _targetPosition.z);
        }

        private void HandlePerspectiveZoomTilt(float dt)
        {
            if (_camera == null || _camera.orthographic || !enableZoomTiltInPerspective)
            {
                return;
            }

            var currentEuler = transform.eulerAngles;
            var currentPitch = NormalizeAngle180(currentEuler.x);
            var targetPitch = Mathf.Lerp(farZoomPitch, nearZoomPitch, GetZoomNormalized());
            var nextPitch = Mathf.SmoothDampAngle(
                currentPitch,
                targetPitch,
                ref _tiltPitchVelocity,
                Mathf.Max(0.01f, tiltSmoothTime),
                Mathf.Infinity,
                dt);

            if (Mathf.Abs(Mathf.DeltaAngle(currentPitch, nextPitch)) < 0.01f)
            {
                return;
            }

            var yaw = currentEuler.y;
            var previousRotation = transform.rotation;
            var nextRotation = Quaternion.Euler(nextPitch, yaw, 0f);

            Vector3 focusBefore = default;
            var hasFocus = false;
            if (keepGroundFocusWhenTilt)
            {
                hasFocus = TryProjectCameraForwardToGround(transform.position, previousRotation, out focusBefore);
            }

            transform.rotation = nextRotation;

            if (hasFocus && TryProjectCameraForwardToGround(transform.position, nextRotation, out var focusAfter))
            {
                var correction = focusBefore - focusAfter;
                correction.y = 0f;
                transform.position += correction;
                _targetPosition += correction;
            }
        }

        private void EnsureZoomRanges()
        {
            if (minOrthoSize > maxOrthoSize)
            {
                (minOrthoSize, maxOrthoSize) = (maxOrthoSize, minOrthoSize);
            }

            if (minFov > maxFov)
            {
                (minFov, maxFov) = (maxFov, minFov);
            }

            if (minHeight > maxHeight)
            {
                (minHeight, maxHeight) = (maxHeight, minHeight);
            }
        }

        private void ClampCurrentZoomToRange()
        {
            if (_camera == null)
            {
                return;
            }

            if (_camera.orthographic)
            {
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize, minOrthoSize, maxOrthoSize);
                return;
            }

            if (perspectiveZoomByFov)
            {
                _camera.fieldOfView = Mathf.Clamp(_camera.fieldOfView, minFov, maxFov);
            }
            else
            {
                _targetPosition = new Vector3(
                    _targetPosition.x,
                    Mathf.Clamp(_targetPosition.y, minHeight, maxHeight),
                    _targetPosition.z);
            }
        }

        private void ApplyStartPoseGuard()
        {
            if (!autoFixInvalidStartPose || _camera == null || _camera.orthographic)
            {
                return;
            }

            var pos = transform.position;
            var euler = transform.eulerAngles;
            var changed = false;

            if (pos.y <= 0.01f)
            {
                pos.y = Mathf.Clamp(fallbackStartHeight, minHeight, maxHeight);
                changed = true;
            }

            // Scene/default camera rotation (pitch almost 0) causes horizon/black view in top-down map.
            if (Mathf.Abs(transform.forward.y) < 0.05f)
            {
                euler.x = fallbackStartPitch;
                euler.z = 0f;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
        }

        private Vector3 GetDragPanDelta()
        {
            if (!enableDragPan)
            {
                return Vector3.zero;
            }

            if (GetPanMouseButtonDown())
            {
                _dragging = true;
                _lastMousePosition = GetMousePosition();
            }
            else if (GetPanMouseButtonUp())
            {
                _dragging = false;
            }

            if (!_dragging)
            {
                return Vector3.zero;
            }

            var current = GetMousePosition();
            var delta = current - _lastMousePosition;
            _lastMousePosition = current;

            if (delta.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            var right = transform.right;
            right.y = 0f;
            right.Normalize();

            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            var directionSign = invertDrag ? -1f : 1f;
            // Reduce drag pan speed to half for finer control.
            return (right * delta.x + forward * delta.y) * directionSign * dragPanSpeed * 0.5f;
        }

        private Vector3 GetKeyboardPanDelta(float dt)
        {
            if (!enableKeyboardPan || dt <= 0f)
            {
                return Vector3.zero;
            }

            var horizontal = GetHorizontalAxis();
            var vertical = GetVerticalAxis();
            var input = new Vector2(horizontal, vertical);
            if (input.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            var right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }
            right.Normalize();

            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            return (right * input.x + forward * input.y) * (keyboardPanSpeed * dt);
        }

        private Vector3 GetEdgePanDelta(float dt)
        {
            if (!enableEdgeScroll)
            {
                return Vector3.zero;
            }

            if (_dragging && !edgeScrollWhileDragging)
            {
                return Vector3.zero;
            }

            var mouse = GetMousePosition();
            if (mouse.x < 0f || mouse.y < 0f || mouse.x > Screen.width || mouse.y > Screen.height)
            {
                return Vector3.zero;
            }

            var left = Mathf.InverseLerp(edgeThreshold, 0f, mouse.x);
            var rightEdge = Mathf.InverseLerp(Screen.width - edgeThreshold, Screen.width, mouse.x);
            var bottom = Mathf.InverseLerp(edgeThreshold, 0f, mouse.y);
            var top = Mathf.InverseLerp(Screen.height - edgeThreshold, Screen.height, mouse.y);

            var horizontal = rightEdge - left;
            var vertical = top - bottom;
            if (Mathf.Abs(horizontal) < 0.0001f && Mathf.Abs(vertical) < 0.0001f)
            {
                return Vector3.zero;
            }

            var right = transform.right;
            right.y = 0f;
            right.Normalize();

            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            return (right * horizontal + forward * vertical) * (edgePanSpeed * dt);
        }

        private void ApplyBounds(float dt, bool hasPanInput)
        {
            var minX = Mathf.Min(xBounds.x, xBounds.y) + boundsPadding - boundsLeftPadding;
            var maxX = Mathf.Max(xBounds.x, xBounds.y) - boundsPadding + boundsRightPadding;
            var minZ = Mathf.Min(zBounds.x, zBounds.y) + boundsPadding - boundsBottomPadding;
            var maxZ = Mathf.Max(zBounds.x, zBounds.y) - boundsPadding + boundsTopPadding;

            if (minX > maxX)
            {
                var mid = (minX + maxX) * 0.5f;
                minX = mid;
                maxX = mid;
            }

            if (minZ > maxZ)
            {
                var mid = (minZ + maxZ) * 0.5f;
                minZ = mid;
                maxZ = mid;
            }

            var clamped = _targetPosition;
            if (useViewportGroundBounds && TryGetViewportGroundExtents(_targetPosition, out var viewMinX, out var viewMaxX, out var viewMinZ, out var viewMaxZ))
            {
                var allowedWidth = maxX - minX;
                var viewWidth = viewMaxX - viewMinX;
                if (viewWidth >= allowedWidth)
                {
                    clamped.x = (minX + maxX) * 0.5f;
                }
                else
                {
                    if (viewMinX < minX)
                    {
                        clamped.x += minX - viewMinX;
                    }
                    else if (viewMaxX > maxX)
                    {
                        clamped.x -= viewMaxX - maxX;
                    }
                }

                var allowedHeight = maxZ - minZ;
                var viewHeight = viewMaxZ - viewMinZ;
                if (viewHeight >= allowedHeight)
                {
                    clamped.z = (minZ + maxZ) * 0.5f;
                }
                else
                {
                    if (viewMinZ < minZ)
                    {
                        clamped.z += minZ - viewMinZ;
                    }
                    else if (viewMaxZ > maxZ)
                    {
                        clamped.z -= viewMaxZ - maxZ;
                    }
                }
            }
            else
            {
                clamped.x = Mathf.Clamp(clamped.x, minX, maxX);
                clamped.z = Mathf.Clamp(clamped.z, minZ, maxZ);
            }

            if (hasPanInput)
            {
                // While actively panning, use hard clamp to avoid "push-back" feel at edges.
                _targetPosition = clamped;
                return;
            }

            if (boundaryDamping <= 0f)
            {
                _targetPosition = clamped;
                return;
            }

            // Soft boundary feel instead of instant hard-stop.
            var t = 1f - Mathf.Exp(-boundaryDamping * dt);
            _targetPosition = Vector3.Lerp(_targetPosition, clamped, t);
        }

        private bool TryGetViewportGroundExtents(Vector3 candidatePosition, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            minZ = float.MaxValue;
            maxZ = float.MinValue;

            var currentPosition = _camera.transform.position;
            var offset = candidatePosition - currentPosition;
            if (!TryProjectViewportPointToGround(new Vector2(0f, 0f), offset, out var p0) ||
                !TryProjectViewportPointToGround(new Vector2(1f, 0f), offset, out var p1) ||
                !TryProjectViewportPointToGround(new Vector2(0f, 1f), offset, out var p2) ||
                !TryProjectViewportPointToGround(new Vector2(1f, 1f), offset, out var p3))
            {
                return false;
            }

            minX = Mathf.Min(p0.x, p1.x, p2.x, p3.x);
            maxX = Mathf.Max(p0.x, p1.x, p2.x, p3.x);
            minZ = Mathf.Min(p0.z, p1.z, p2.z, p3.z);
            maxZ = Mathf.Max(p0.z, p1.z, p2.z, p3.z);
            return true;
        }

        private bool TryProjectViewportPointToGround(Vector2 viewport, Vector3 cameraOffset, out Vector3 point)
        {
            var ray = _camera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            ray.origin += cameraOffset;

            var dy = ray.direction.y;
            if (Mathf.Abs(dy) < 0.00001f)
            {
                point = Vector3.zero;
                return false;
            }

            var t = (boundsGroundY - ray.origin.y) / dy;
            if (t < 0f)
            {
                point = Vector3.zero;
                return false;
            }

            point = ray.origin + ray.direction * t;
            return true;
        }

        private bool TryProjectCameraForwardToGround(Vector3 origin, Quaternion rotation, out Vector3 point)
        {
            var direction = rotation * Vector3.forward;
            if (Mathf.Abs(direction.y) < 0.00001f)
            {
                point = Vector3.zero;
                return false;
            }

            var t = (boundsGroundY - origin.y) / direction.y;
            if (t < 0f)
            {
                point = Vector3.zero;
                return false;
            }

            point = origin + direction * t;
            return true;
        }

        private static float NormalizeAngle180(float angle)
        {
            var normalized = angle % 360f;
            if (normalized > 180f)
            {
                normalized -= 360f;
            }
            if (normalized < -180f)
            {
                normalized += 360f;
            }
            return normalized;
        }

        private bool HasMouse()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null;
#else
            return true;
#endif
        }

        private Vector3 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null ? (Vector3)mouse.position.ReadValue() : Vector3.zero;
#else
            return Input.mousePosition;
#endif
        }

        private float GetScrollDeltaY()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return 0f;
            }

            var raw = mouse.scroll.ReadValue().y;
            return raw * inputSystemScrollScale;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

        private bool GetPanMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null && mouse.middleButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(2);
#endif
        }

        private bool GetPanMouseButtonUp()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null && mouse.middleButton.wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(2);
#endif
        }

        private float GetHorizontalAxis()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return 0f;
            }

            float value = 0f;
            if (keyboard.aKey.isPressed || (enableArrowKeyPan && keyboard.leftArrowKey.isPressed))
            {
                value -= 1f;
            }

            if (keyboard.dKey.isPressed || (enableArrowKeyPan && keyboard.rightArrowKey.isPressed))
            {
                value += 1f;
            }

            return Mathf.Clamp(value, -1f, 1f);
#else
            float value = 0f;
            if (Input.GetKey(KeyCode.A) || (enableArrowKeyPan && Input.GetKey(KeyCode.LeftArrow)))
            {
                value -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || (enableArrowKeyPan && Input.GetKey(KeyCode.RightArrow)))
            {
                value += 1f;
            }

            return Mathf.Clamp(value, -1f, 1f);
#endif
        }

        private float GetVerticalAxis()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return 0f;
            }

            float value = 0f;
            if (keyboard.sKey.isPressed || (enableArrowKeyPan && keyboard.downArrowKey.isPressed))
            {
                value -= 1f;
            }

            if (keyboard.wKey.isPressed || (enableArrowKeyPan && keyboard.upArrowKey.isPressed))
            {
                value += 1f;
            }

            return Mathf.Clamp(value, -1f, 1f);
#else
            float value = 0f;
            if (Input.GetKey(KeyCode.S) || (enableArrowKeyPan && Input.GetKey(KeyCode.DownArrow)))
            {
                value -= 1f;
            }

            if (Input.GetKey(KeyCode.W) || (enableArrowKeyPan && Input.GetKey(KeyCode.UpArrow)))
            {
                value += 1f;
            }

            return Mathf.Clamp(value, -1f, 1f);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureZoomRanges();
            scrollZoomMultiplier = Mathf.Max(0f, scrollZoomMultiplier);
            scrollSensitivity = Mathf.Max(0f, scrollSensitivity);
            maxZoomStepPerFrame = Mathf.Max(0.01f, maxZoomStepPerFrame);
            perspectiveDollySpeed = Mathf.Max(0f, perspectiveDollySpeed);
            tiltSmoothTime = Mathf.Max(0.01f, tiltSmoothTime);
            farZoomPitch = Mathf.Clamp(farZoomPitch, -89f, 89f);
            nearZoomPitch = Mathf.Clamp(nearZoomPitch, -89f, 89f);
            fallbackStartHeight = Mathf.Max(0.1f, fallbackStartHeight);
        }

        private void OnDrawGizmosSelected()
        {
            if (!clampToBounds)
            {
                return;
            }

            var minX = Mathf.Min(xBounds.x, xBounds.y) + boundsPadding - boundsLeftPadding;
            var maxX = Mathf.Max(xBounds.x, xBounds.y) - boundsPadding + boundsRightPadding;
            var minZ = Mathf.Min(zBounds.x, zBounds.y) + boundsPadding - boundsBottomPadding;
            var maxZ = Mathf.Max(zBounds.x, zBounds.y) - boundsPadding + boundsTopPadding;

            Gizmos.color = new Color(0.15f, 0.9f, 1f, 0.9f);
            var a = new Vector3(minX, transform.position.y, minZ);
            var b = new Vector3(maxX, transform.position.y, minZ);
            var c = new Vector3(maxX, transform.position.y, maxZ);
            var d = new Vector3(minX, transform.position.y, maxZ);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
#endif
    }
}
