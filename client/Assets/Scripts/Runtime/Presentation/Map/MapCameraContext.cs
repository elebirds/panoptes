using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public readonly struct MapCameraContext
    {
        public readonly Rect worldRect;
        public readonly float groundY;
        public readonly Vector3 initialFocusPoint;

        public MapCameraContext(Rect worldRect, float groundY, Vector3 initialFocusPoint)
        {
            this.worldRect = worldRect;
            this.groundY = groundY;
            this.initialFocusPoint = initialFocusPoint;
        }

        public bool IsValid => worldRect.width > 0f && worldRect.height > 0f;

        public Vector3 ClampGroundPoint(Vector3 point)
        {
            if (!IsValid)
            {
                return new Vector3(point.x, groundY, point.z);
            }

            return new Vector3(
                Mathf.Clamp(point.x, worldRect.xMin, worldRect.xMax),
                groundY,
                Mathf.Clamp(point.z, worldRect.yMin, worldRect.yMax));
        }

        public Vector3 GetWorldCenter()
        {
            return new Vector3(worldRect.center.x, groundY, worldRect.center.y);
        }
    }

    public struct GroundBoundsResult
    {
        public bool success;
        public Rect bounds;

        public GroundBoundsResult(bool success, Rect bounds)
        {
            this.success = success;
            this.bounds = bounds;
        }
    }
}
