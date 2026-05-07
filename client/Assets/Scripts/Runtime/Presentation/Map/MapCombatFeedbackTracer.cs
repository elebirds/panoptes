using UnityEngine;

namespace Panoptes.Presentation.Map
{
    internal sealed class MapCombatFeedbackTracer : MonoBehaviour
    {
        private LineRenderer _lineRenderer;
        private Material _material;
        private Color _startColor;
        private Color _endColor;
        private float _duration;
        private float _elapsed;

        public void Configure(Vector3 from, Vector3 to, Color color, float width, float duration, float arcHeight)
        {
            _duration = Mathf.Max(0.05f, duration);
            _elapsed = 0f;
            _startColor = color;
            _endColor = new Color(color.r, color.g, color.b, 0f);

            _lineRenderer = gameObject.AddComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = 3;
            _lineRenderer.startWidth = Mathf.Max(0.01f, width);
            _lineRenderer.endWidth = Mathf.Max(0.01f, width * 0.35f);
            _lineRenderer.numCapVertices = 4;
            _lineRenderer.numCornerVertices = 2;
            _lineRenderer.startColor = _startColor;
            _lineRenderer.endColor = _startColor;
            _lineRenderer.SetPosition(0, from);
            _lineRenderer.SetPosition(1, Vector3.Lerp(from, to, 0.5f) + Vector3.up * Mathf.Max(0f, arcHeight));
            _lineRenderer.SetPosition(2, to);

            _material = CreateMaterial(color);
            _lineRenderer.sharedMaterial = _material;
        }

        private void Update()
        {
            if (_lineRenderer == null)
            {
                Destroy(gameObject);
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_elapsed / Mathf.Max(0.05f, _duration));
            var color = Color.Lerp(_startColor, _endColor, t);
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_material);
            }
            else
            {
                DestroyImmediate(_material);
            }
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                name = "CombatFeedbackTracer_Runtime"
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            return material;
        }
    }
}
