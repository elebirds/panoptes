/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.Pointer.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Pointer, UI hit-test, and map raycast helpers for MapInputHandler.
 *************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Panoptes.Presentation.Map
{
    public sealed partial class MapInputHandler
    {
        private readonly List<RaycastResult> _uiRaycastResults = new();

        private sealed class MoveGhostTag : MonoBehaviour
        {
        }

        private bool TryRaycastNode(out NodeView nodeView)
        {
            nodeView = null;
            if (!TryRaycast(out var hit))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            return map != null && map.TryGetNodeViewByWorld(hit.point, out nodeView) && nodeView != null;
        }

        private bool TryRaycastUnit(out UnitView unitView)
        {
            unitView = null;
            if (!TryRaycast(out var hit))
            {
                return false;
            }

            if (hit.collider.GetComponentInParent<MoveGhostTag>() != null)
            {
                return false;
            }

            unitView = hit.collider.GetComponentInParent<UnitView>();
            return unitView != null;
        }

        private bool TryRaycast(out RaycastHit hit)
        {
            hit = default;

            var ray = inputCamera.ScreenPointToRay(GetMousePosition());
            return Physics.Raycast(ray, out hit, raycastDistance, raycastMask, QueryTriggerInteraction.Ignore);
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

            _uiRaycastResults.Clear();
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = GetMousePosition()
            };
            EventSystem.current.RaycastAll(eventData, _uiRaycastResults);
            return _uiRaycastResults.Count > 0;
        }

        private static bool HasMouse()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null;
#else
            return true;
#endif
        }

        private static Vector3 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null ? (Vector3)mouse.position.ReadValue() : Vector3.zero;
#else
            return Input.mousePosition;
#endif
        }

        private static bool GetLeftMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private static bool GetRightMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(1);
#endif
        }
    }
}
