/*************************************************
 * Project: Panoptes
 * File: TopDownCameraController.cs
 * Author: Panoptes Team
 * Date: 2026-04-17
 * Description: Fixed strategic camera driven by map camera context.
 *************************************************/

using UnityEngine;
using UnityEngine.EventSystems;
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

        [Header("Input Gate")]
        [SerializeField] private bool blockCameraInputWhenPointerOverUI = true;
        [SerializeField] private bool blockZoomWhenPointerOverUI = true;
        [SerializeField] private bool blockDragWhenPointerOverUI = true;
        [SerializeField] private bool blockEdgePanWhenPointerOverUI = true;

        [Header("Pan")]
        [SerializeField] private bool enableDragPan = true;
        [SerializeField] private bool enableKeyboardPan = true;
        [SerializeField] private bool enableArrowKeyPan = true;
        [SerializeField] private bool enableEdgePan = false;
        [SerializeField] private float edgePanThreshold = 24f;
        [SerializeField] private float nearPanSpeed = 8f;
        [SerializeField] private float farPanSpeed = 28f;
        [SerializeField] private float moveSmoothTime = 0.08f;

        [Header("Zoom")]
        [SerializeField] private float nearDistance = 6.2f;
        [SerializeField] private float farDistance = 36f;
        [SerializeField] private float zoomSmoothTime = 0.06f;
        [SerializeField] private float scrollZoomStep = 0.12f;
        [SerializeField] private float inputSystemScrollScale = 0.01f;
        [SerializeField] private bool invertScrollDirection = false;
        [Range(0f, 1f)] [SerializeField] private float initialZoomNormalized = 0.22f;

        [Header("Fixed Rig")]
        [SerializeField] private float fixedYaw = 0f;
        [SerializeField] private float fixedPitch = 45f;
        [SerializeField] private float fixedFieldOfView = 50f;
        [SerializeField] private string anchorRootName = "AnchorRoot";
        [SerializeField] private string yawPivotName = "YawPivot";
        [SerializeField] private string pitchPivotName = "PitchPivot";

        private Camera _camera;
        private Transform _anchorRoot;
        private Transform _yawPivot;
        private Transform _pitchPivot;
        private MapRenderer _mapRenderer;
        private MapCameraContext _cameraContext;
        private bool _hasCameraContext;
        private bool _dragging;
        private Vector3 _dragGroundPoint;
        private bool _hasDragGroundPoint;
        private Vector2 _currentAnchorXZ;
        private Vector2 _targetAnchorXZ;
        private Vector2 _anchorVelocity;
        private float _currentZoomNormalized;
        private float _targetZoomNormalized;
        private float _zoomVelocity;
        private bool _hasSafeViewportOverride;
        private Rect _safeViewportOverride = new Rect(0f, 0f, 1f, 1f);

        private void Awake()
        {
            EnsureCameraReference();
            EnsureRigHierarchy();
            InitializeStateFromRig();
        }

        private void OnEnable()
        {
            EnsureCameraReference();
            EnsureRigHierarchy();
            InitializeStateFromRig();
            SubscribeToMapRenderer();
        }

        private void OnDisable()
        {
            UnsubscribeFromMapRenderer();
            _dragging = false;
            _hasDragGroundPoint = false;
        }

        private void Update()
        {
            EnsureCameraReference();
            EnsureRigHierarchy();
            SubscribeToMapRenderer();

            if (_camera == null)
            {
                return;
            }

            var dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            _camera.orthographic = false;
            _camera.fieldOfView = fixedFieldOfView;

            var pointerOverUI = IsPointerOverUI();
            HandleZoom(pointerOverUI);

            var panDelta = Vector2.zero;
            panDelta += GetKeyboardPanDelta(dt);
            panDelta += GetEdgePanDelta(dt, pointerOverUI);
            panDelta += GetDragPanDelta(pointerOverUI);
            _targetAnchorXZ += panDelta;

            var safeViewport = ResolveSafeViewportRect();
            _targetZoomNormalized = ClampZoomForContext(_targetZoomNormalized, safeViewport);
            _targetAnchorXZ = ClampAnchorForContext(_targetAnchorXZ, _targetZoomNormalized, safeViewport);

            var immediate = _dragging;
            if (immediate)
            {
                _currentAnchorXZ = _targetAnchorXZ;
                _currentZoomNormalized = _targetZoomNormalized;
                _anchorVelocity = Vector2.zero;
                _zoomVelocity = 0f;
            }
            else
            {
                _currentAnchorXZ = Vector2.SmoothDamp(
                    _currentAnchorXZ,
                    _targetAnchorXZ,
                    ref _anchorVelocity,
                    moveSmoothTime,
                    Mathf.Infinity,
                    dt);
                _currentZoomNormalized = Mathf.SmoothDamp(
                    _currentZoomNormalized,
                    _targetZoomNormalized,
                    ref _zoomVelocity,
                    zoomSmoothTime,
                    Mathf.Infinity,
                    dt);
            }

            SyncRigToState(_currentAnchorXZ, _currentZoomNormalized, safeViewport);
        }

        public void ApplyCameraContext(MapCameraContext context, bool snapInstantly = true)
        {
            _cameraContext = context;
            _hasCameraContext = context.IsValid;
            EnsureRigHierarchy();

            _targetZoomNormalized = ClampZoomForContext(Mathf.Clamp01(initialZoomNormalized), ResolveSafeViewportRect());
            _currentZoomNormalized = snapInstantly ? _targetZoomNormalized : _currentZoomNormalized;

            if (_hasCameraContext)
            {
                FocusWorldPoint(context.initialFocusPoint, snapInstantly);
                return;
            }

            if (snapInstantly)
            {
                SyncRigToState(_currentAnchorXZ, _currentZoomNormalized, ResolveSafeViewportRect());
            }
        }

        public void SnapTargetToCurrentPosition()
        {
            InitializeStateFromRig();
        }

        public void SetManualTargetPosition(Vector3 position, bool snapInstantly = false)
        {
            _targetAnchorXZ = new Vector2(position.x, position.z);
            _targetAnchorXZ = ClampAnchorForContext(_targetAnchorXZ, _targetZoomNormalized, ResolveSafeViewportRect());

            if (!snapInstantly)
            {
                return;
            }

            _currentAnchorXZ = _targetAnchorXZ;
            _anchorVelocity = Vector2.zero;
            SyncRigToState(_currentAnchorXZ, _currentZoomNormalized, ResolveSafeViewportRect());
        }

        public bool FocusWorldPoint(Vector3 worldPoint, bool snapInstantly = true)
        {
            EnsureCameraReference();
            if (_camera == null)
            {
                return false;
            }

            var focusPoint = _hasCameraContext ? _cameraContext.ClampGroundPoint(worldPoint) : new Vector3(worldPoint.x, worldPoint.y, worldPoint.z);
            focusPoint.y = GetGroundY();

            _targetAnchorXZ = ClampAnchorForContext(new Vector2(focusPoint.x, focusPoint.z), _targetZoomNormalized, ResolveSafeViewportRect());

            if (!snapInstantly)
            {
                return true;
            }

            _currentAnchorXZ = _targetAnchorXZ;
            _anchorVelocity = Vector2.zero;
            SyncRigToState(_currentAnchorXZ, _currentZoomNormalized, ResolveSafeViewportRect());
            return true;
        }

        public void SetZoomNormalized(float normalized)
        {
            _targetZoomNormalized = ClampZoomForContext(Mathf.Clamp01(normalized), ResolveSafeViewportRect());
        }

        public float GetZoomNormalized()
        {
            return _currentZoomNormalized;
        }

        public Vector3 GetAnchorWorldPoint()
        {
            return new Vector3(_currentAnchorXZ.x, GetGroundY(), _currentAnchorXZ.y);
        }

        public GroundBoundsResult TryGetCurrentVisibleGroundBounds()
        {
            if (TryGetVisibleGroundBounds(_currentAnchorXZ, _currentZoomNormalized, ResolveSafeViewportRect(), out var bounds))
            {
                return new GroundBoundsResult(true, bounds);
            }

            return new GroundBoundsResult(false, default);
        }

        public void UpdateImmediateForTests()
        {
            EnsureCameraReference();
            EnsureRigHierarchy();
            var safeViewport = ResolveSafeViewportRect();
            _targetZoomNormalized = ClampZoomForContext(_targetZoomNormalized, safeViewport);
            _targetAnchorXZ = ClampAnchorForContext(_targetAnchorXZ, _targetZoomNormalized, safeViewport);
            _currentAnchorXZ = _targetAnchorXZ;
            _currentZoomNormalized = _targetZoomNormalized;
            _anchorVelocity = Vector2.zero;
            _zoomVelocity = 0f;
            SyncRigToState(_currentAnchorXZ, _currentZoomNormalized, safeViewport);
        }

        public void SetSafeViewportOverride(Rect viewport)
        {
            _safeViewportOverride = viewport;
            _hasSafeViewportOverride = true;
        }

        public void ClearSafeViewportOverride()
        {
            _hasSafeViewportOverride = false;
        }

        private void HandleMapCameraContextReady(MapCameraContext context)
        {
            ApplyCameraContext(context, true);
        }

        private void SubscribeToMapRenderer()
        {
            var renderer = MapRenderer.Instance != null ? MapRenderer.Instance : FindAnyObjectByType<MapRenderer>();
            if (renderer == null || renderer == _mapRenderer)
            {
                if (renderer != null && renderer.TryGetCameraContext(out var existingContext) && (!_hasCameraContext || !SameContext(existingContext, _cameraContext)))
                {
                    HandleMapCameraContextReady(existingContext);
                }
                return;
            }

            UnsubscribeFromMapRenderer();
            _mapRenderer = renderer;
            _mapRenderer.CameraContextReady += HandleMapCameraContextReady;

            if (_mapRenderer.TryGetCameraContext(out var context))
            {
                HandleMapCameraContextReady(context);
            }
        }

        private void UnsubscribeFromMapRenderer()
        {
            if (_mapRenderer == null)
            {
                return;
            }

            _mapRenderer.CameraContextReady -= HandleMapCameraContextReady;
            _mapRenderer = null;
        }

        private void EnsureCameraReference()
        {
            if (_camera != null)
            {
                return;
            }

            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void EnsureRigHierarchy()
        {
            EnsureCameraReference();
            if (_camera == null)
            {
                return;
            }

            if (transform.parent != null
                && transform.parent.name == pitchPivotName
                && transform.parent.parent != null
                && transform.parent.parent.name == yawPivotName
                && transform.parent.parent.parent != null
                && transform.parent.parent.parent.name == anchorRootName)
            {
                _pitchPivot = transform.parent;
                _yawPivot = _pitchPivot.parent;
                _anchorRoot = _yawPivot.parent;
                return;
            }

            var cameraTransform = _camera.transform;
            var groundY = GetGroundY();
            var anchorPoint = TryProjectRayToGround(cameraTransform.position, cameraTransform.forward, groundY, out var projected)
                ? projected
                : new Vector3(cameraTransform.position.x, groundY, cameraTransform.position.z);

            var anchorRoot = new GameObject(anchorRootName).transform;
            var yawPivot = new GameObject(yawPivotName).transform;
            var pitchPivot = new GameObject(pitchPivotName).transform;

            anchorRoot.position = new Vector3(anchorPoint.x, groundY, anchorPoint.z);
            yawPivot.SetParent(anchorRoot, false);
            pitchPivot.SetParent(yawPivot, false);
            cameraTransform.SetParent(pitchPivot, true);

            _anchorRoot = anchorRoot;
            _yawPivot = yawPivot;
            _pitchPivot = pitchPivot;
        }

        private void InitializeStateFromRig()
        {
            if (_anchorRoot == null)
            {
                return;
            }

            _currentZoomNormalized = DistanceToZoomNormalized(GetCurrentDistance());
            _targetZoomNormalized = _currentZoomNormalized;

            var safeViewport = ResolveSafeViewportRect();
            if (TryGetGroundPointAtViewportFromCurrentCamera(safeViewport.center, out var logicalAnchor))
            {
                _currentAnchorXZ = new Vector2(logicalAnchor.x, logicalAnchor.z);
            }
            else
            {
                _currentAnchorXZ = new Vector2(_anchorRoot.position.x, _anchorRoot.position.z);
            }

            _targetAnchorXZ = _currentAnchorXZ;
            _anchorVelocity = Vector2.zero;
            _zoomVelocity = 0f;
            SyncRigToState(_currentAnchorXZ, _currentZoomNormalized, safeViewport);
        }

        private void SyncRigToState(Vector2 anchorXZ, float zoomNormalized, Rect safeViewport)
        {
            if (_anchorRoot == null || _yawPivot == null || _pitchPivot == null || _camera == null)
            {
                return;
            }

            _camera.orthographic = false;
            _camera.fieldOfView = fixedFieldOfView;

            var groundY = GetGroundY();
            var rigRoot = GetRigRootForLogicalAnchor(anchorXZ, zoomNormalized, safeViewport);
            _anchorRoot.position = new Vector3(rigRoot.x, groundY, rigRoot.y);
            _yawPivot.localPosition = Vector3.zero;
            _yawPivot.localRotation = Quaternion.Euler(0f, fixedYaw, 0f);
            _pitchPivot.localPosition = Vector3.zero;
            _pitchPivot.localRotation = Quaternion.Euler(fixedPitch, 0f, 0f);
            _camera.transform.localPosition = new Vector3(0f, 0f, -GetDistanceForZoomNormalized(zoomNormalized));
            _camera.transform.localRotation = Quaternion.identity;
        }

        private Vector2 GetKeyboardPanDelta(float dt)
        {
            if (!enableKeyboardPan || dt <= 0f)
            {
                return Vector2.zero;
            }

            var horizontal = GetHorizontalAxis();
            var vertical = GetVerticalAxis();
            var input = new Vector2(horizontal, vertical);
            if (input.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            return input * (GetPanSpeed() * dt);
        }

        private Vector2 GetEdgePanDelta(float dt, bool pointerOverUI)
        {
            if (!enableEdgePan || dt <= 0f || pointerOverUI && blockEdgePanWhenPointerOverUI || !HasMouse())
            {
                return Vector2.zero;
            }

            var mouse = GetMousePosition();
            if (mouse.x < 0f || mouse.y < 0f || mouse.x > Screen.width || mouse.y > Screen.height)
            {
                return Vector2.zero;
            }

            var left = Mathf.InverseLerp(edgePanThreshold, 0f, mouse.x);
            var right = Mathf.InverseLerp(Screen.width - edgePanThreshold, Screen.width, mouse.x);
            var bottom = Mathf.InverseLerp(edgePanThreshold, 0f, mouse.y);
            var top = Mathf.InverseLerp(Screen.height - edgePanThreshold, Screen.height, mouse.y);

            var delta = new Vector2(right - left, top - bottom);
            if (delta.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            return delta * (GetPanSpeed() * dt);
        }

        private Vector2 GetDragPanDelta(bool pointerOverUI)
        {
            if (!enableDragPan || !HasMouse())
            {
                return Vector2.zero;
            }

            if (GetPanMouseButtonDown())
            {
                if (pointerOverUI && blockDragWhenPointerOverUI)
                {
                    _dragging = false;
                    _hasDragGroundPoint = false;
                    return Vector2.zero;
                }

                _dragging = TryGetGroundPointUnderPointer(out _dragGroundPoint);
                _hasDragGroundPoint = _dragging;
                return Vector2.zero;
            }

            if (GetPanMouseButtonUp())
            {
                _dragging = false;
                _hasDragGroundPoint = false;
                return Vector2.zero;
            }

            if (!_dragging || !_hasDragGroundPoint)
            {
                return Vector2.zero;
            }

            if (!TryGetGroundPointUnderPointer(out var currentGround))
            {
                return Vector2.zero;
            }

            var delta = _dragGroundPoint - currentGround;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            return new Vector2(delta.x, delta.z);
        }

        private bool TryGetGroundPointUnderPointer(out Vector3 point)
        {
            point = Vector3.zero;
            if (_camera == null || !HasMouse())
            {
                return false;
            }

            var ray = _camera.ScreenPointToRay(GetMousePosition());
            return TryProjectRayToGround(ray.origin, ray.direction, GetGroundY(), out point);
        }

        private void HandleZoom(bool pointerOverUI)
        {
            if (_camera == null || pointerOverUI && blockZoomWhenPointerOverUI)
            {
                return;
            }

            var rawScroll = GetScrollDeltaY();
            if (Mathf.Abs(rawScroll) <= 0.0001f)
            {
                return;
            }

            var scrollDirection = invertScrollDirection ? -1f : 1f;
            var normalizedScroll = Mathf.Clamp(rawScroll, -1f, 1f);
            _targetZoomNormalized = Mathf.Clamp01(_targetZoomNormalized + normalizedScroll * scrollZoomStep * scrollDirection);
        }

        private float ClampZoomForContext(float desiredZoomNormalized, Rect safeViewport)
        {
            desiredZoomNormalized = Mathf.Clamp01(desiredZoomNormalized);
            if (!_hasCameraContext)
            {
                return desiredZoomNormalized;
            }

            var minimumZoomNormalized = FindMinimumZoomThatFitsContext(safeViewport);
            return Mathf.Clamp(desiredZoomNormalized, minimumZoomNormalized, 1f);
        }

        private float FindMinimumZoomThatFitsContext(Rect safeViewport)
        {
            if (!_hasCameraContext)
            {
                return 0f;
            }

            if (DoesZoomFitContext(0f, safeViewport))
            {
                return 0f;
            }

            var low = 0f;
            var high = 1f;
            for (var i = 0; i < 18; i++)
            {
                var mid = (low + high) * 0.5f;
                if (DoesZoomFitContext(mid, safeViewport))
                {
                    high = mid;
                }
                else
                {
                    low = mid;
                }
            }

            return high;
        }

        private bool DoesZoomFitContext(float zoomNormalized, Rect safeViewport)
        {
            if (!_hasCameraContext)
            {
                return true;
            }

            if (!TryGetLogicalRelativeGroundBounds(zoomNormalized, safeViewport, out var relativeBounds))
            {
                return true;
            }

            var halfWorldWidth = _cameraContext.worldRect.width * 0.5f + 0.001f;
            var halfWorldHeight = _cameraContext.worldRect.height * 0.5f + 0.001f;
            return Mathf.Abs(relativeBounds.xMin) <= halfWorldWidth
                && Mathf.Abs(relativeBounds.xMax) <= halfWorldWidth
                && Mathf.Abs(relativeBounds.yMin) <= halfWorldHeight
                && Mathf.Abs(relativeBounds.yMax) <= halfWorldHeight;
        }

        private Vector2 ClampAnchorForContext(Vector2 desiredAnchor, float zoomNormalized, Rect safeViewport)
        {
            if (!_hasCameraContext)
            {
                return desiredAnchor;
            }

            if (!TryGetLogicalRelativeGroundBounds(zoomNormalized, safeViewport, out var relativeBounds))
            {
                return desiredAnchor;
            }

            const float clampSafetyPadding = 0.002f;
            var minAllowedX = _cameraContext.worldRect.xMin - relativeBounds.xMin + clampSafetyPadding;
            var maxAllowedX = _cameraContext.worldRect.xMax - relativeBounds.xMax - clampSafetyPadding;
            var minAllowedZ = _cameraContext.worldRect.yMin - relativeBounds.yMin + clampSafetyPadding;
            var maxAllowedZ = _cameraContext.worldRect.yMax - relativeBounds.yMax - clampSafetyPadding;

            if (minAllowedX > maxAllowedX)
            {
                desiredAnchor.x = _cameraContext.worldRect.center.x;
            }
            else
            {
                desiredAnchor.x = Mathf.Clamp(desiredAnchor.x, minAllowedX, maxAllowedX);
            }

            if (minAllowedZ > maxAllowedZ)
            {
                desiredAnchor.y = _cameraContext.worldRect.center.y;
            }
            else
            {
                desiredAnchor.y = Mathf.Clamp(desiredAnchor.y, minAllowedZ, maxAllowedZ);
            }

            return desiredAnchor;
        }

        private bool TryGetCurrentVisibleGroundBoundsInternal(out Rect bounds)
        {
            return TryGetVisibleGroundBounds(_currentAnchorXZ, _currentZoomNormalized, ResolveSafeViewportRect(), out bounds);
        }

        private bool TryGetVisibleGroundBounds(Vector2 anchorXZ, float zoomNormalized, Rect safeViewport, out Rect bounds)
        {
            bounds = default;
            if (!TryGetLogicalRelativeGroundBounds(zoomNormalized, safeViewport, out var relativeBounds))
            {
                return false;
            }

            bounds = new Rect(
                anchorXZ.x + relativeBounds.xMin,
                anchorXZ.y + relativeBounds.yMin,
                relativeBounds.width,
                relativeBounds.height);
            return true;
        }

        private bool TryGetLogicalRelativeGroundBounds(float zoomNormalized, Rect safeViewport, out Rect bounds)
        {
            bounds = default;
            if (!TryGetRootRelativeGroundBounds(zoomNormalized, safeViewport, out var rootRelativeBounds))
            {
                return false;
            }

            if (!TryGetSafeCenterGroundOffset(zoomNormalized, safeViewport, out var safeCenterGround))
            {
                return false;
            }

            bounds = Rect.MinMaxRect(
                rootRelativeBounds.xMin - safeCenterGround.x,
                rootRelativeBounds.yMin - safeCenterGround.z,
                rootRelativeBounds.xMax - safeCenterGround.x,
                rootRelativeBounds.yMax - safeCenterGround.z);
            return true;
        }

        private bool TryGetRootRelativeGroundBounds(float zoomNormalized, Rect safeViewport, out Rect bounds)
        {
            bounds = default;
            if (_camera == null)
            {
                return false;
            }

            var candidateAnchor = Vector2.zero;
            if (!TryGetGroundPointAtViewport(new Vector2(safeViewport.xMin, safeViewport.yMin), candidateAnchor, zoomNormalized, out var bottomLeft)
                || !TryGetGroundPointAtViewport(new Vector2(safeViewport.xMax, safeViewport.yMin), candidateAnchor, zoomNormalized, out var bottomRight)
                || !TryGetGroundPointAtViewport(new Vector2(safeViewport.xMin, safeViewport.yMax), candidateAnchor, zoomNormalized, out var topLeft)
                || !TryGetGroundPointAtViewport(new Vector2(safeViewport.xMax, safeViewport.yMax), candidateAnchor, zoomNormalized, out var topRight))
            {
                return false;
            }

            var minX = Mathf.Min(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x);
            var maxX = Mathf.Max(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x);
            var minZ = Mathf.Min(bottomLeft.z, bottomRight.z, topLeft.z, topRight.z);
            var maxZ = Mathf.Max(bottomLeft.z, bottomRight.z, topLeft.z, topRight.z);
            bounds = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            return true;
        }

        private bool TryGetSafeCenterGroundOffset(float zoomNormalized, Rect safeViewport, out Vector3 point)
        {
            return TryGetGroundPointAtViewport(safeViewport.center, Vector2.zero, zoomNormalized, out point);
        }

        private bool TryGetGroundPointAtViewport(Vector2 viewportPoint, Vector2 anchorXZ, float zoomNormalized, out Vector3 point)
        {
            point = Vector3.zero;
            if (_camera == null)
            {
                return false;
            }

            var ray = _camera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
            var candidateCameraPosition = CalculateCameraWorldPosition(anchorXZ, zoomNormalized);
            var cameraOffset = candidateCameraPosition - _camera.transform.position;
            return TryProjectRayToGround(ray.origin + cameraOffset, ray.direction, GetGroundY(), out point);
        }

        private bool TryGetGroundPointAtViewportFromCurrentCamera(Vector2 viewportPoint, out Vector3 point)
        {
            point = Vector3.zero;
            if (_camera == null)
            {
                return false;
            }

            var ray = _camera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
            return TryProjectRayToGround(ray.origin, ray.direction, GetGroundY(), out point);
        }

        private Vector3 CalculateCameraWorldPosition(Vector2 anchorXZ, float zoomNormalized)
        {
            var anchor = new Vector3(anchorXZ.x, GetGroundY(), anchorXZ.y);
            var rotation = Quaternion.Euler(fixedPitch, fixedYaw, 0f);
            return anchor + rotation * new Vector3(0f, 0f, -GetDistanceForZoomNormalized(zoomNormalized));
        }

        private Vector2 GetRigRootForLogicalAnchor(Vector2 anchorXZ, float zoomNormalized, Rect safeViewport)
        {
            if (!TryGetSafeCenterGroundOffset(zoomNormalized, safeViewport, out var safeCenterGround))
            {
                return anchorXZ;
            }

            return new Vector2(anchorXZ.x - safeCenterGround.x, anchorXZ.y - safeCenterGround.z);
        }

        private float GetDistanceForZoomNormalized(float zoomNormalized)
        {
            return Mathf.Lerp(farDistance, nearDistance, Mathf.Clamp01(zoomNormalized));
        }

        private float DistanceToZoomNormalized(float distance)
        {
            if (Mathf.Abs(farDistance - nearDistance) < 0.001f)
            {
                return 1f;
            }

            return Mathf.Clamp01(Mathf.InverseLerp(farDistance, nearDistance, distance));
        }

        private float GetCurrentDistance()
        {
            return _camera != null ? Mathf.Max(nearDistance, Mathf.Abs(_camera.transform.localPosition.z)) : nearDistance;
        }

        private float GetPanSpeed()
        {
            return Mathf.Lerp(farPanSpeed, nearPanSpeed, Mathf.Clamp01(_currentZoomNormalized));
        }

        private Rect ResolveSafeViewportRect()
        {
            var rect = _hasSafeViewportOverride ? _safeViewportOverride : CameraSafeAreaRegistry.GetSafeViewportRect();
            rect.xMin = Mathf.Clamp01(rect.xMin);
            rect.yMin = Mathf.Clamp01(rect.yMin);
            rect.xMax = Mathf.Clamp01(rect.xMax);
            rect.yMax = Mathf.Clamp01(rect.yMax);

            if (rect.width <= 0.05f || rect.height <= 0.05f)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            return rect;
        }

        private float GetGroundY()
        {
            return _hasCameraContext ? _cameraContext.groundY : 0f;
        }

        private static bool SameContext(MapCameraContext lhs, MapCameraContext rhs)
        {
            return lhs.groundY == rhs.groundY
                && lhs.worldRect == rhs.worldRect
                && lhs.initialFocusPoint == rhs.initialFocusPoint;
        }

        private bool IsPointerOverUI()
        {
            if (!blockCameraInputWhenPointerOverUI)
            {
                return false;
            }

            var eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        private static bool TryProjectRayToGround(Vector3 origin, Vector3 direction, float groundY, out Vector3 point)
        {
            point = Vector3.zero;
            if (Mathf.Abs(direction.y) < 0.0001f)
            {
                return false;
            }

            var t = (groundY - origin.y) / direction.y;
            if (t < 0f)
            {
                return false;
            }

            point = origin + direction * t;
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
            return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
            return Input.mousePosition;
#endif
        }

        private float GetScrollDeltaY()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y * inputSystemScrollScale : 0f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

        private bool GetPanMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(2);
#endif
        }

        private bool GetPanMouseButtonUp()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.middleButton.wasReleasedThisFrame;
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

            var value = 0f;
            if (keyboard.aKey.isPressed)
            {
                value -= 1f;
            }
            if (keyboard.dKey.isPressed)
            {
                value += 1f;
            }
            if (enableArrowKeyPan)
            {
                if (keyboard.leftArrowKey.isPressed)
                {
                    value -= 1f;
                }
                if (keyboard.rightArrowKey.isPressed)
                {
                    value += 1f;
                }
            }

            return Mathf.Clamp(value, -1f, 1f);
#else
            return Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f);
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

            var value = 0f;
            if (keyboard.sKey.isPressed)
            {
                value -= 1f;
            }
            if (keyboard.wKey.isPressed)
            {
                value += 1f;
            }
            if (enableArrowKeyPan)
            {
                if (keyboard.downArrowKey.isPressed)
                {
                    value -= 1f;
                }
                if (keyboard.upArrowKey.isPressed)
                {
                    value += 1f;
                }
            }

            return Mathf.Clamp(value, -1f, 1f);
#else
            return Mathf.Clamp(Input.GetAxisRaw("Vertical"), -1f, 1f);
#endif
        }
    }
}
