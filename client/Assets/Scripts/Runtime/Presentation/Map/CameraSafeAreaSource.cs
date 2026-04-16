using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public enum CameraSafeAreaEdge
    {
        Auto = 0,
        Left = 1,
        Right = 2,
        Top = 3,
        Bottom = 4
    }

    public enum CameraSafeAreaMeasureMode
    {
        RectTransform = 0,
        ManualNormalized = 1
    }

    [DisallowMultipleComponent]
    public sealed class CameraSafeAreaSource : MonoBehaviour
    {
        [SerializeField] private CameraSafeAreaEdge edge = CameraSafeAreaEdge.Auto;
        [SerializeField] private CameraSafeAreaMeasureMode measureMode = CameraSafeAreaMeasureMode.RectTransform;
        [SerializeField] private RectTransform targetRect;
        [SerializeField] [Range(0f, 1f)] private float manualNormalizedSize = 0.1f;
        [SerializeField] private bool ignoreWhenInactive = true;

        private readonly Vector3[] _corners = new Vector3[4];

        private void OnEnable()
        {
            CameraSafeAreaRegistry.Register(this);
        }

        private void OnDisable()
        {
            CameraSafeAreaRegistry.Unregister(this);
        }

        public bool TryGetContribution(out CameraSafeAreaEdge resolvedEdge, out float normalizedSize)
        {
            resolvedEdge = CameraSafeAreaEdge.Auto;
            normalizedSize = 0f;

            if (ignoreWhenInactive && (!isActiveAndEnabled || !gameObject.activeInHierarchy))
            {
                return false;
            }

            resolvedEdge = edge;
            if (resolvedEdge == CameraSafeAreaEdge.Auto && !TryResolveAutoEdge(out resolvedEdge))
            {
                return false;
            }

            normalizedSize = measureMode == CameraSafeAreaMeasureMode.ManualNormalized
                ? Mathf.Clamp01(manualNormalizedSize)
                : Mathf.Clamp01(MeasureRectTransformThickness(resolvedEdge));
            return normalizedSize > 0.0001f;
        }

        private bool TryResolveAutoEdge(out CameraSafeAreaEdge resolvedEdge)
        {
            resolvedEdge = CameraSafeAreaEdge.Auto;
            if (!TryGetScreenRect(out var rect))
            {
                return false;
            }

            var left = Mathf.Abs(rect.xMin);
            var right = Mathf.Abs(Screen.width - rect.xMax);
            var bottom = Mathf.Abs(rect.yMin);
            var top = Mathf.Abs(Screen.height - rect.yMax);

            var minDistance = left;
            resolvedEdge = CameraSafeAreaEdge.Left;

            if (right < minDistance)
            {
                minDistance = right;
                resolvedEdge = CameraSafeAreaEdge.Right;
            }

            if (top < minDistance)
            {
                minDistance = top;
                resolvedEdge = CameraSafeAreaEdge.Top;
            }

            if (bottom < minDistance)
            {
                resolvedEdge = CameraSafeAreaEdge.Bottom;
            }

            return true;
        }

        private float MeasureRectTransformThickness(CameraSafeAreaEdge resolvedEdge)
        {
            if (!TryGetScreenRect(out var rect))
            {
                return 0f;
            }

            switch (resolvedEdge)
            {
                case CameraSafeAreaEdge.Left:
                case CameraSafeAreaEdge.Right:
                    return Screen.width > 0 ? rect.width / Screen.width : 0f;
                case CameraSafeAreaEdge.Top:
                case CameraSafeAreaEdge.Bottom:
                    return Screen.height > 0 ? rect.height / Screen.height : 0f;
                default:
                    return 0f;
            }
        }

        private bool TryGetScreenRect(out Rect rect)
        {
            rect = default;
            var rectTransform = targetRect != null ? targetRect : transform as RectTransform;
            if (rectTransform == null)
            {
                return false;
            }

            rectTransform.GetWorldCorners(_corners);
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;

            for (var i = 0; i < _corners.Length; i++)
            {
                var corner = RectTransformUtility.WorldToScreenPoint(null, _corners[i]);
                if (corner.x < minX) minX = corner.x;
                if (corner.x > maxX) maxX = corner.x;
                if (corner.y < minY) minY = corner.y;
                if (corner.y > maxY) maxY = corner.y;
            }

            if (!float.IsFinite(minX) || !float.IsFinite(maxX) || !float.IsFinite(minY) || !float.IsFinite(maxY))
            {
                return false;
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return rect.width > 0.1f && rect.height > 0.1f;
        }
    }
}
