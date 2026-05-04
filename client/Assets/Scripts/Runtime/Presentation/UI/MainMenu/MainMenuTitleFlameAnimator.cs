using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.MainMenu
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class MainMenuTitleFlameAnimator : MonoBehaviour
    {
        [SerializeField] private Color emberTop = new Color(1f, 0.96f, 0.62f, 1f);
        [SerializeField] private Color flameMid = new Color(1f, 0.42f, 0.08f, 1f);
        [SerializeField] private Color coalBottom = new Color(0.55f, 0.06f, 0.015f, 1f);
        [SerializeField] private float pulseSpeed = 2.4f;
        [SerializeField] private float jitterPixels = 1.8f;

        private TextMeshProUGUI _text;
        private Shadow _shadow;
        private Outline _outline;
        private Vector3 _baseScale;
        private Vector2 _shadowBaseDistance;

        private void Awake()
        {
            Cache();
            ApplyStaticStyle();
        }

        private void OnEnable()
        {
            Cache();
            ApplyStaticStyle();
        }

        private void Update()
        {
            if (_text == null)
            {
                return;
            }

            var time = Time.unscaledTime;
            var pulse = (Mathf.Sin(time * pulseSpeed) + 1f) * 0.5f;
            var lick = (Mathf.Sin(time * (pulseSpeed * 1.73f) + 0.8f) + 1f) * 0.5f;
            _text.colorGradient = new VertexGradient(
                Color.Lerp(emberTop, Color.white, pulse * 0.28f),
                Color.Lerp(emberTop, new Color(1f, 0.72f, 0.2f, 1f), lick * 0.35f),
                Color.Lerp(flameMid, coalBottom, 0.18f + pulse * 0.24f),
                Color.Lerp(coalBottom, flameMid, lick * 0.22f));
            _text.ForceMeshUpdate(false, false);

            var rect = transform as RectTransform;
            if (rect != null)
            {
                var x = Mathf.Sin(time * 4.1f) * jitterPixels;
                var y = Mathf.Cos(time * 5.3f + 0.4f) * jitterPixels * 0.55f;
                rect.localScale = _baseScale * (1f + pulse * 0.018f);
                if (_shadow != null)
                {
                    _shadow.effectDistance = _shadowBaseDistance + new Vector2(x * 0.55f, -2f - y);
                }
            }

            if (_outline != null)
            {
                _outline.effectColor = Color.Lerp(
                    new Color(0.45f, 0.06f, 0f, 0.92f),
                    new Color(1f, 0.36f, 0.02f, 0.96f),
                    pulse);
            }
        }

        private void Cache()
        {
            _text ??= GetComponent<TextMeshProUGUI>();
            _shadow ??= GetComponent<Shadow>() ?? gameObject.AddComponent<Shadow>();
            _outline ??= GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            if (_baseScale == Vector3.zero)
            {
                _baseScale = transform.localScale;
            }
            if (_shadowBaseDistance == Vector2.zero)
            {
                _shadowBaseDistance = new Vector2(0f, -4f);
            }
        }

        private void ApplyStaticStyle()
        {
            if (_text != null)
            {
                _text.enableVertexGradient = true;
                _text.fontStyle |= FontStyles.Bold;
                _text.faceColor = Color.white;
            }
            if (_shadow != null)
            {
                _shadow.effectColor = new Color(0.95f, 0.18f, 0.02f, 0.78f);
                _shadow.useGraphicAlpha = true;
            }
            if (_outline != null)
            {
                _outline.effectDistance = new Vector2(2.2f, -2.2f);
                _outline.useGraphicAlpha = true;
            }
        }
    }
}
