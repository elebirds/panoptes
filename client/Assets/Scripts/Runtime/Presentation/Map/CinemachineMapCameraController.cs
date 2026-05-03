/*************************************************
 * Project: Panoptes
 * File: CinemachineMapCameraController.cs
 * Author: Panoptes Team
 * Date: 2026-04-17
 * Description: Minimal map camera input driver for Cinemachine.
 *************************************************/

using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Panoptes.Presentation.Map
{
    [DisallowMultipleComponent]
    public sealed class CinemachineMapCameraController : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] private CinemachineCamera targetCamera;
        [SerializeField] private Camera renderCamera;

        [Header("Time")]
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Input Gate")]
        [SerializeField] private bool blockZoomWhenPointerOverUI = true;
        [SerializeField] private bool blockDragWhenPointerOverUI = true;

        [Header("Pan")]
        [SerializeField] private bool enableDragPan = true;
        [SerializeField] private bool enableKeyboardPan = true;
        [SerializeField] private float nearPanSpeed = 8f;
        [SerializeField] private float farPanSpeed = 28f;
        [SerializeField] private float moveSmoothTime = 0.08f;

        [Header("Zoom")]
        [SerializeField] private float nearDistance = 6.2f;
        [SerializeField] private float farDistance = 36f;
        [SerializeField] private float distanceSmoothTime = 0.06f;
        [SerializeField] private float scrollDistanceStep = 3.5f;
        [SerializeField] private float inputSystemScrollScale = 0.01f;
        [SerializeField] private bool invertScrollDirection = false;

        private CinemachineFollow _followComponent;
        private MapRenderer _mapRenderer;
        private MapCameraContext _cameraContext;
        private bool _hasCameraContext;
        private bool _subscribedToMapRenderer;
        private bool _dragging;
        private Vector3 _dragGroundPoint;
        private Vector2 _currentAnchorXZ;
        private Vector2 _targetAnchorXZ;
        private Vector2 _anchorVelocity;
        private float _currentDistance;
        private float _targetDistance;
        private float _distanceVelocity;

        private void Awake()
        {
            ResolveRig();
            InitializeStateFromRig();
        }

        [Inject]
        private void Construct(MapRenderer mapRenderer)
        {
            ConfigureMapRenderer(mapRenderer);
        }

        private void OnEnable()
        {
            ResolveRig();
            InitializeStateFromRig();
            SubscribeToMapRenderer();
        }

        private void OnDisable()
        {
            UnsubscribeFromMapRenderer();
            _dragging = false;
        }

        private void Update()
        {
            ResolveRig();
            if (_followComponent == null)
            {
                return;
            }

            var dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            var pointerOverUI = IsPointerOverUI();
            HandleZoom(pointerOverUI);

            var panDelta = Vector2.zero;
            panDelta += GetKeyboardPanDelta(dt);
            panDelta += GetDragPanDelta(pointerOverUI);
            _targetAnchorXZ += panDelta;

            _targetDistance = ClampDistance(_targetDistance);
            _targetAnchorXZ = ClampAnchorToContext(_targetAnchorXZ);

            if (_dragging)
            {
                _currentAnchorXZ = _targetAnchorXZ;
                _currentDistance = _targetDistance;
                _anchorVelocity = Vector2.zero;
                _distanceVelocity = 0f;
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
                _currentDistance = Mathf.SmoothDamp(
                    _currentDistance,
                    _targetDistance,
                    ref _distanceVelocity,
                    distanceSmoothTime,
                    Mathf.Infinity,
                    dt);
            }

            ApplyRigState(_currentAnchorXZ, _currentDistance);
        }

        public void ApplyCameraContext(MapCameraContext context, bool snapInstantly = true)
        {
            _cameraContext = context;
            _hasCameraContext = context.IsValid;

            if (!_hasCameraContext)
            {
                return;
            }

            _targetAnchorXZ = ClampAnchorToContext(new Vector2(context.initialFocusPoint.x, context.initialFocusPoint.z));
            if (!snapInstantly)
            {
                return;
            }

            _currentAnchorXZ = _targetAnchorXZ;
            _anchorVelocity = Vector2.zero;
            ApplyRigState(_currentAnchorXZ, _currentDistance > 0f ? _currentDistance : ClampDistance(farDistance));
        }

        private void HandleMapCameraContextReady(MapCameraContext context)
        {
            ApplyCameraContext(context, true);
        }

        private void ConfigureMapRenderer(MapRenderer mapRenderer)
        {
            if (_mapRenderer == mapRenderer)
            {
                if (isActiveAndEnabled)
                {
                    SyncCameraContextFromMapRenderer();
                }
                return;
            }

            UnsubscribeFromMapRenderer();
            _mapRenderer = mapRenderer;
            if (isActiveAndEnabled)
            {
                SubscribeToMapRenderer();
            }
        }

        private void SubscribeToMapRenderer()
        {
            if (_mapRenderer == null)
            {
                return;
            }

            if (!_subscribedToMapRenderer)
            {
                _mapRenderer.CameraContextReady += HandleMapCameraContextReady;
                _subscribedToMapRenderer = true;
            }

            SyncCameraContextFromMapRenderer();
        }

        private void UnsubscribeFromMapRenderer()
        {
            if (_mapRenderer == null || !_subscribedToMapRenderer)
            {
                return;
            }

            _mapRenderer.CameraContextReady -= HandleMapCameraContextReady;
            _subscribedToMapRenderer = false;
        }

        private void SyncCameraContextFromMapRenderer()
        {
            if (_mapRenderer != null &&
                _mapRenderer.TryGetCameraContext(out var existingContext) &&
                (!_hasCameraContext || !SameContext(existingContext, _cameraContext)))
            {
                HandleMapCameraContextReady(existingContext);
            }
        }

        private void ResolveRig()
        {
            if (renderCamera == null)
            {
                renderCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                targetCamera = FindAnyObjectByType<CinemachineCamera>();
            }

            if (targetCamera == null)
            {
                _followComponent = null;
                return;
            }

            if (targetCamera.Follow != transform)
            {
                targetCamera.Follow = transform;
            }

            if (targetCamera.LookAt != transform)
            {
                targetCamera.LookAt = transform;
            }

            if (_followComponent == null || _followComponent.gameObject != targetCamera.gameObject)
            {
                _followComponent = targetCamera.GetComponent<CinemachineFollow>();
            }
        }

        private void InitializeStateFromRig()
        {
            _currentAnchorXZ = new Vector2(transform.position.x, transform.position.z);
            _targetAnchorXZ = _currentAnchorXZ;

            var initialDistance = ExtractDistanceFromRig();
            _currentDistance = ClampDistance(initialDistance);
            _targetDistance = _currentDistance;
            _anchorVelocity = Vector2.zero;
            _distanceVelocity = 0f;

            ApplyRigState(_currentAnchorXZ, _currentDistance);
        }

        private float ExtractDistanceFromRig()
        {
            if (_followComponent == null)
            {
                return farDistance;
            }

            var offset = _followComponent.FollowOffset;
            return Mathf.Max(nearDistance, Mathf.Max(Mathf.Abs(offset.y), Mathf.Abs(offset.z)));
        }

        private void ApplyRigState(Vector2 anchorXZ, float distance)
        {
            var groundY = _hasCameraContext ? _cameraContext.groundY : transform.position.y;
            transform.position = new Vector3(anchorXZ.x, groundY, anchorXZ.y);
            if (_followComponent != null)
            {
                _followComponent.FollowOffset = new Vector3(0f, distance, -distance);
            }
        }

        private void HandleZoom(bool pointerOverUI)
        {
            if (pointerOverUI && blockZoomWhenPointerOverUI)
            {
                return;
            }

            var rawScroll = GetScrollDeltaY();
            if (Mathf.Abs(rawScroll) <= 0.0001f)
            {
                return;
            }

            var normalizedScroll = Mathf.Clamp(rawScroll, -1f, 1f);
            var scrollDirection = invertScrollDirection ? 1f : -1f;
            _targetDistance = ClampDistance(_targetDistance + normalizedScroll * scrollDistanceStep * scrollDirection);
        }

        private Vector2 GetKeyboardPanDelta(float dt)
        {
            if (!enableKeyboardPan || dt <= 0f)
            {
                return Vector2.zero;
            }

            var input = new Vector2(GetHorizontalAxis(), GetVerticalAxis());
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

        private Vector2 GetDragPanDelta(bool pointerOverUI)
        {
            if (!enableDragPan || !HasMouse() || renderCamera == null)
            {
                return Vector2.zero;
            }

            if (GetPanMouseButtonDown())
            {
                if (pointerOverUI && blockDragWhenPointerOverUI)
                {
                    _dragging = false;
                    return Vector2.zero;
                }

                _dragging = TryGetGroundPointUnderPointer(out _dragGroundPoint);
                return Vector2.zero;
            }

            if (GetPanMouseButtonUp())
            {
                _dragging = false;
                return Vector2.zero;
            }

            if (!_dragging || !TryGetGroundPointUnderPointer(out var currentGround))
            {
                return Vector2.zero;
            }

            var delta = _dragGroundPoint - currentGround;
            _dragGroundPoint = currentGround;
            delta.y = 0f;
            return new Vector2(delta.x, delta.z);
        }

        private bool TryGetGroundPointUnderPointer(out Vector3 point)
        {
            point = Vector3.zero;
            if (renderCamera == null)
            {
                return false;
            }

            var ray = renderCamera.ScreenPointToRay(GetMousePosition());
            return TryProjectRayToGround(ray.origin, ray.direction, _hasCameraContext ? _cameraContext.groundY : transform.position.y, out point);
        }

        private Vector2 ClampAnchorToContext(Vector2 anchorXZ)
        {
            if (!_hasCameraContext)
            {
                return anchorXZ;
            }

            anchorXZ.x = Mathf.Clamp(anchorXZ.x, _cameraContext.worldRect.xMin, _cameraContext.worldRect.xMax);
            anchorXZ.y = Mathf.Clamp(anchorXZ.y, _cameraContext.worldRect.yMin, _cameraContext.worldRect.yMax);
            return anchorXZ;
        }

        private float ClampDistance(float distance)
        {
            return Mathf.Clamp(distance, nearDistance, farDistance);
        }

        private float GetPanSpeed()
        {
            var normalized = Mathf.InverseLerp(farDistance, nearDistance, _currentDistance);
            return Mathf.Lerp(farPanSpeed, nearPanSpeed, normalized);
        }

        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            if (EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && EventSystem.current.IsPointerOverGameObject(Mouse.current.deviceId))
            {
                return true;
            }
#endif

            return false;
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
            return mouse != null ? mouse.scroll.ReadValue().y * inputSystemScrollScale : 0f;
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

            var value = 0f;
            if (keyboard.aKey.isPressed)
            {
                value -= 1f;
            }
            if (keyboard.dKey.isPressed)
            {
                value += 1f;
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

            return Mathf.Clamp(value, -1f, 1f);
#else
            return Mathf.Clamp(Input.GetAxisRaw("Vertical"), -1f, 1f);
#endif
        }

        private static bool SameContext(MapCameraContext lhs, MapCameraContext rhs)
        {
            return lhs.groundY == rhs.groundY
                && lhs.worldRect == rhs.worldRect
                && lhs.initialFocusPoint == rhs.initialFocusPoint;
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
    }
}
