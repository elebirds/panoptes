using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.MainMenu
{
    public sealed class MainMenuFireAnimator : MonoBehaviour
    {
        [SerializeField] private RawImage[] fireLayers;
        [SerializeField] private float uvAmplitudeX = 0.008f;
        [SerializeField] private float uvAmplitudeY = 0.014f;
        [SerializeField] private float positionAmplitude = 14f;
        [SerializeField] private float scaleAmplitude = 0.012f;
        [SerializeField] private float alphaMin = 0.04f;
        [SerializeField] private float alphaMax = 0.16f;
        [SerializeField] private float pulseSpeed = 1.35f;

        private Rect[] _baseUvs;
        private Vector2[] _basePositions;
        private Vector3[] _baseScales;
        private Color[] _baseColors;

        private void Awake()
        {
            CacheBaseValues();
        }

        private void OnValidate()
        {
            alphaMin = Mathf.Clamp01(alphaMin);
            alphaMax = Mathf.Clamp01(Mathf.Max(alphaMin, alphaMax));
            uvAmplitudeX = Mathf.Max(0f, uvAmplitudeX);
            uvAmplitudeY = Mathf.Max(0f, uvAmplitudeY);
            positionAmplitude = Mathf.Max(0f, positionAmplitude);
            scaleAmplitude = Mathf.Max(0f, scaleAmplitude);
            pulseSpeed = Mathf.Max(0.01f, pulseSpeed);
        }

        private void Update()
        {
            if (fireLayers == null || fireLayers.Length == 0)
            {
                return;
            }

            if (_baseUvs == null || _baseUvs.Length != fireLayers.Length)
            {
                CacheBaseValues();
            }

            var time = Time.unscaledTime;
            for (var i = 0; i < fireLayers.Length; i++)
            {
                var layer = fireLayers[i];
                if (layer == null)
                {
                    continue;
                }

                var phase = i * 1.73f;
                var uv = _baseUvs[i];
                uv.x += Mathf.Sin(time * (0.19f + i * 0.03f) + phase) * uvAmplitudeX;
                uv.y += Mathf.Cos(time * (0.27f + i * 0.05f) + phase) * uvAmplitudeY;
                layer.uvRect = uv;

                if (layer.transform is RectTransform rect)
                {
                    var x = Mathf.Sin(time * (0.9f + i * 0.17f) + phase) * positionAmplitude;
                    var y = Mathf.Cos(time * (1.1f + i * 0.11f) + phase) * positionAmplitude;
                    rect.anchoredPosition = _basePositions[i] + new Vector2(x, y);

                    var scale = 1f + Mathf.Sin(time * (0.8f + i * 0.13f) + phase) * scaleAmplitude;
                    rect.localScale = _baseScales[i] * scale;
                }

                var pulse = (Mathf.Sin(time * pulseSpeed + phase) + 1f) * 0.5f;
                var color = _baseColors[i];
                color.a = Mathf.Lerp(alphaMin, alphaMax, pulse);
                layer.color = color;
            }
        }

        private void CacheBaseValues()
        {
            if (fireLayers == null)
            {
                _baseUvs = null;
                _basePositions = null;
                _baseScales = null;
                _baseColors = null;
                return;
            }

            _baseUvs = new Rect[fireLayers.Length];
            _basePositions = new Vector2[fireLayers.Length];
            _baseScales = new Vector3[fireLayers.Length];
            _baseColors = new Color[fireLayers.Length];

            for (var i = 0; i < fireLayers.Length; i++)
            {
                var layer = fireLayers[i];
                if (layer == null)
                {
                    _baseUvs[i] = new Rect(0f, 0f, 1f, 1f);
                    _basePositions[i] = Vector2.zero;
                    _baseScales[i] = Vector3.one;
                    _baseColors[i] = Color.white;
                    continue;
                }

                _baseUvs[i] = layer.uvRect;
                _basePositions[i] = layer.transform is RectTransform rect ? rect.anchoredPosition : Vector2.zero;
                _baseScales[i] = layer.transform.localScale;
                _baseColors[i] = layer.color;
            }
        }
    }
}
