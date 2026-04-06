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

namespace Panoptes.Runtime.Map
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

        [Header("Pan - Edge Scroll")]
        [SerializeField] private bool enableEdgeScroll = true;
        [SerializeField] private bool edgeScrollWhileDragging = false;
        [SerializeField] private float edgeThreshold = 24f; // px
        [SerializeField] private float edgePanSpeed = 14f;  // world units / second

        [Header("Movement Smoothing")]
        [SerializeField] private float moveSmoothTime = 0.08f;

        [Header("Zoom")]
        [SerializeField] private float scrollZoomSpeed = 4f;
        [SerializeField] private float inputSystemScrollScale = 0.01f;
        [SerializeField] private bool perspectiveZoomByFov = true;
        [SerializeField] private float minOrthoSize = 5f;
        [SerializeField] private float maxOrthoSize = 28f;
        [SerializeField] private float minFov = 25f;
        [SerializeField] private float maxFov = 60f;
        [SerializeField] private float minHeight = 8f;
        [SerializeField] private float maxHeight = 40f;

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

            var dragDelta = GetDragPanDelta();
            var edgeDelta = GetEdgePanDelta(dt);
            var panDelta = dragDelta + edgeDelta;
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

        public void SetPerspectiveZoomByFov(bool enabled)
        {
            perspectiveZoomByFov = enabled;
            ClampCurrentZoomToRange();
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
            var scroll = GetScrollDeltaY();
            if (Mathf.Abs(scroll) <= 0.0001f)
            {
                return;
            }

            if (_camera.orthographic)
            {
                _camera.orthographicSize = Mathf.Clamp(
                    _camera.orthographicSize - scroll * scrollZoomSpeed,
                    minOrthoSize,
                    maxOrthoSize);
                return;
            }

            if (perspectiveZoomByFov)
            {
                _camera.fieldOfView = Mathf.Clamp(
                    _camera.fieldOfView - scroll * scrollZoomSpeed,
                    minFov,
                    maxFov);
            }
            else
            {
                // Alternate perspective zoom: move camera along Y.
                _targetPosition += Vector3.up * (-scroll * scrollZoomSpeed);
                _targetPosition = new Vector3(
                    _targetPosition.x,
                    Mathf.Clamp(_targetPosition.y, minHeight, maxHeight),
                    _targetPosition.z);
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

        private Vector3 GetDragPanDelta()
        {
            if (!enableDragPan)
            {
                return Vector3.zero;
            }

            if (GetLeftMouseButtonDown())
            {
                _dragging = true;
                _lastMousePosition = GetMousePosition();
            }
            else if (GetLeftMouseButtonUp())
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

        private bool GetLeftMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private bool GetLeftMouseButtonUp()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(0);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureZoomRanges();
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
