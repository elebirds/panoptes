/*************************************************
 * Project: Panoptes
 * File: CastleHPBar.cs
 * Author: Panoptes Team
 * Date: 2026-04-12
 * Description: World-space castle HP bar view (bar + name + faction plate).
 *************************************************/

using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class CastleHPBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private Image factionPlateImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image hpBarBackgroundImage;
        [SerializeField] private Image hpBarFillImage;

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(240f, 52f);
        [SerializeField] private Vector2 factionPlateSize = new Vector2(86f, 24f);
        [SerializeField] private Vector2 hpBarSize = new Vector2(138f, 16f);
        [SerializeField] private float spacing = 10f;

        [Header("Readability")]
        [SerializeField] private float canvasDynamicPixelsPerUnit = 120f;
        [SerializeField] private float canvasReferencePixelsPerUnit = 100f;
        [SerializeField] private float nameFontSize = 22f;
        [SerializeField] private FontStyles nameFontStyle = FontStyles.Bold;

        [Header("Style")]
        [SerializeField] private Color defaultFactionColor = new Color(0.22f, 0.5f, 0.95f, 0.95f);
        [SerializeField] private Color nameColor = Color.white;
        [SerializeField] private Color hpBarBackgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        [SerializeField] private Color hpBarFillColor = new Color(0.2f, 0.95f, 0.35f, 1f);
        [SerializeField] private string defaultDisplayName = "Castle";

        private static readonly Color TransparentWhite = new Color(1f, 1f, 1f, 0f);
        private static Sprite _defaultUiSprite;
#if UNITY_EDITOR
        private bool _editorRefreshQueued;
#endif

        private void Awake()
        {
            EnsureRuntimeUi();
            ApplyStyle();
        }

        public void EnsureRuntimeUi()
        {
            EnsureHierarchy();
            ApplyLayout();
        }

        public void EditorRebuildUiForPrefab()
        {
            EnsureHierarchy();
            ApplyLayout();
            ApplyStyle();
        }

        public void SetName(string displayName)
        {
            EnsureRuntimeUi();
            if (nameText != null)
            {
                nameText.text = string.IsNullOrWhiteSpace(displayName) ? defaultDisplayName : displayName;
            }
        }

        public void SetFactionColor(Color color)
        {
            EnsureRuntimeUi();
            if (factionPlateImage != null)
            {
                factionPlateImage.color = color;
            }
        }

        public void SetHpRatio(float ratio01)
        {
            EnsureRuntimeUi();
            if (hpBarFillImage != null)
            {
                hpBarFillImage.fillAmount = Mathf.Clamp01(ratio01);
            }
        }

        public void SetBarColors(Color background, Color fill)
        {
            EnsureRuntimeUi();
            if (hpBarBackgroundImage != null)
            {
                hpBarBackgroundImage.color = background;
            }

            if (hpBarFillImage != null)
            {
                hpBarFillImage.color = fill;
            }
        }

        private void ApplyStyle()
        {
            if (nameText != null)
            {
                nameText.text = string.IsNullOrWhiteSpace(defaultDisplayName) ? "Castle" : defaultDisplayName;
                nameText.color = nameColor;
                nameText.fontSize = Mathf.Max(10f, nameFontSize);
                nameText.fontStyle = nameFontStyle;
                nameText.enableAutoSizing = false;
                nameText.textWrappingMode = TextWrappingModes.NoWrap;
                nameText.extraPadding = true;
            }

            if (factionPlateImage != null)
            {
                factionPlateImage.color = defaultFactionColor;
            }

            if (hpBarBackgroundImage != null)
            {
                hpBarBackgroundImage.color = hpBarBackgroundColor;
            }

            if (hpBarFillImage != null)
            {
                hpBarFillImage.color = hpBarFillColor;
                hpBarFillImage.fillAmount = 1f;
            }
        }

        private void EnsureHierarchy()
        {
            if (rootRect == null)
            {
                rootRect = GetComponent<RectTransform>();
                if (rootRect == null)
                {
                    rootRect = gameObject.AddComponent<RectTransform>();
                }
            }

            if (worldCanvas == null)
            {
                worldCanvas = GetComponent<Canvas>();
                if (worldCanvas == null)
                {
                    worldCanvas = gameObject.AddComponent<Canvas>();
                }
            }

            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.overrideSorting = false;
            worldCanvas.pixelPerfect = true;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.dynamicPixelsPerUnit = Mathf.Max(1f, canvasDynamicPixelsPerUnit);
            scaler.referencePixelsPerUnit = Mathf.Max(1f, canvasReferencePixelsPerUnit);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            var factionPlate = EnsureChild("FactionPlate", rootRect);
            factionPlateImage = EnsureImage(factionPlate.gameObject, factionPlateImage);

            var name = EnsureChild("NameText", factionPlate);
            nameText = EnsureTmp(name.gameObject, nameText);

            var barBg = EnsureChild("HpBarBackground", rootRect);
            hpBarBackgroundImage = EnsureImage(barBg.gameObject, hpBarBackgroundImage);

            var fill = EnsureChild("HpBarFill", barBg);
            hpBarFillImage = EnsureImage(fill.gameObject, hpBarFillImage);
            hpBarFillImage.type = Image.Type.Filled;
            hpBarFillImage.fillMethod = Image.FillMethod.Horizontal;
            hpBarFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpBarFillImage.fillClockwise = true;
        }

        private void ApplyLayout()
        {
            if (rootRect == null)
            {
                return;
            }

            rootRect.sizeDelta = panelSize;

            var halfSpacing = Mathf.Max(0f, spacing) * 0.5f;
            var leftWidth = Mathf.Max(20f, factionPlateSize.x);
            var rightWidth = Mathf.Max(20f, hpBarSize.x);
            var panelHeight = Mathf.Max(factionPlateSize.y, hpBarSize.y);

            var factionRect = factionPlateImage != null ? factionPlateImage.rectTransform : null;
            if (factionRect != null)
            {
                factionRect.anchorMin = new Vector2(0f, 0.5f);
                factionRect.anchorMax = new Vector2(0f, 0.5f);
                factionRect.pivot = new Vector2(0f, 0.5f);
                factionRect.anchoredPosition = new Vector2(0f, 0f);
                factionRect.sizeDelta = new Vector2(leftWidth, Mathf.Max(12f, factionPlateSize.y));
            }

            var nameRect = nameText != null ? nameText.rectTransform : null;
            if (nameRect != null)
            {
                nameRect.anchorMin = Vector2.zero;
                nameRect.anchorMax = Vector2.one;
                nameRect.offsetMin = new Vector2(4f, 0f);
                nameRect.offsetMax = new Vector2(-4f, 0f);
            }

            var bgRect = hpBarBackgroundImage != null ? hpBarBackgroundImage.rectTransform : null;
            if (bgRect != null)
            {
                bgRect.anchorMin = new Vector2(0f, 0.5f);
                bgRect.anchorMax = new Vector2(0f, 0.5f);
                bgRect.pivot = new Vector2(0f, 0.5f);
                bgRect.anchoredPosition = new Vector2(leftWidth + halfSpacing + halfSpacing, 0f);
                bgRect.sizeDelta = new Vector2(rightWidth, Mathf.Max(8f, hpBarSize.y));
            }

            var fillRect = hpBarFillImage != null ? hpBarFillImage.rectTransform : null;
            if (fillRect != null)
            {
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }

            rootRect.sizeDelta = new Vector2(leftWidth + spacing + rightWidth, panelHeight);
        }

        private static RectTransform EnsureChild(string childName, Transform parent)
        {
            var child = parent.Find(childName) as RectTransform;
            if (child != null)
            {
                return child;
            }

            var go = new GameObject(childName, typeof(RectTransform));
            child = go.GetComponent<RectTransform>();
            child.SetParent(parent, false);
            child.localScale = Vector3.one;
            return child;
        }

        private static Image EnsureImage(GameObject go, Image existing)
        {
            if (existing != null && existing.gameObject == go)
            {
                return existing;
            }

            var image = go.GetComponent<Image>();
            if (image == null)
            {
                image = go.AddComponent<Image>();
            }

            if (_defaultUiSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _defaultUiSprite = tex != null
                    ? Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f))
                    : null;
            }

            image.sprite = _defaultUiSprite;
            image.color = TransparentWhite;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI EnsureTmp(GameObject go, TextMeshProUGUI existing)
        {
            if (existing != null && existing.gameObject == go)
            {
                return existing;
            }

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                tmp = go.AddComponent<TextMeshProUGUI>();
            }

            tmp.text = "Castle";
            tmp.fontSize = 16f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            canvasDynamicPixelsPerUnit = Mathf.Max(1f, canvasDynamicPixelsPerUnit);
            canvasReferencePixelsPerUnit = Mathf.Max(1f, canvasReferencePixelsPerUnit);
            nameFontSize = Mathf.Max(10f, nameFontSize);
            if (Application.isPlaying)
            {
                EnsureRuntimeUi();
                ApplyStyle();
                return;
            }

            QueueEditorRefresh();
        }

        private void QueueEditorRefresh()
        {
            if (_editorRefreshQueued)
            {
                return;
            }

            _editorRefreshQueued = true;
            EditorApplication.delayCall += PerformEditorRefresh;
        }

        private void PerformEditorRefresh()
        {
            if (this == null)
            {
                return;
            }

            _editorRefreshQueued = false;
            if (Application.isPlaying)
            {
                return;
            }

            EnsureHierarchy();
            ApplyLayout();
            ApplyStyle();
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
