/*************************************************
 * Project: Panoptes
 * File: CityCoreHpBarOverlayController.cs
 * Author: Panoptes Team
 * Date: 2026-04-12
 * Description: Screen-space city core HP bar overlay manager.
 *************************************************/

using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class CityCoreHpBarOverlayController : MonoBehaviour
    {
        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateOverlayCanvas = true;
        [SerializeField] private string canvasName = "CityCoreHpOverlayCanvas";

        [Header("Tracking")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float screenYOffset = 32f;
        [SerializeField] private bool hideWhenBehindCamera = true;
        [SerializeField] private bool hideWhenOffScreen = true;
        [SerializeField] private bool disableWorldSpaceCityCoreHpBar = true;

        [Header("Entry Layout")]
        [SerializeField] private Vector2 entrySize = new Vector2(228f, 34f);
        [SerializeField] private Vector2 plateSize = new Vector2(88f, 24f);
        [SerializeField] private Vector2 barSize = new Vector2(126f, 14f);
        [SerializeField] private float spacing = 8f;
        [SerializeField] private int fontSize = 18;

        [Header("Colors")]
        [SerializeField] private Color neutralOwnerColor = Color.white;
        [SerializeField] private Color friendlyOwnerColor = new Color(0.26f, 0.78f, 1f, 1f);
        [SerializeField] private Color enemyOwnerColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color hpBarBgColor = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        [SerializeField] private Color hpBarFillColor = new Color(0.2f, 0.95f, 0.35f, 1f);

        private sealed class Entry
        {
            public string NodeId;
            public BuildingView Building;
            public RectTransform Root;
            public Image Plate;
            public TextMeshProUGUI Name;
            public Image Fill;
            public float WorldHeightOffset;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private GameStateCache _cache;
        private Sprite _defaultUiSprite;

        private void Awake()
        {
            _cache = GameStateCache.Instance;
            ResolveCamera();
            EnsureCanvas();
        }

        private void LateUpdate()
        {
            ResolveCamera();
            if (_canvasRect == null || targetCamera == null)
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null || map.TileViews == null || map.TileViews.Count == 0)
            {
                HideAll();
                return;
            }

            SyncEntriesFromMap(map);
            UpdateEntryTransforms();
        }

        private void ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void EnsureCanvas()
        {
            if (_canvas != null && _canvasRect != null)
            {
                return;
            }

            if (!autoCreateOverlayCanvas)
            {
                _canvas = GetComponentInParent<Canvas>();
                _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;
                return;
            }

            var found = GameObject.Find(canvasName);
            if (found != null)
            {
                _canvas = found.GetComponent<Canvas>();
                _canvasRect = found.transform as RectTransform;
            }

            if (_canvas != null && _canvasRect != null)
            {
                return;
            }

            var root = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasRect = root.transform as RectTransform;
            _canvas = root.GetComponent<Canvas>();
            if (_canvas != null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 400;
                _canvas.pixelPerfect = true;
            }

            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        private void SyncEntriesFromMap(MapRenderer map)
        {
            var alive = new HashSet<string>();
            foreach (var pair in map.TileViews)
            {
                var node = pair.Value;
                if (node == null)
                {
                    continue;
                }

                var building = node.BuildingInstance;
                if (building == null || !building.IsCityCore || building.IsGhost)
                {
                    continue;
                }

                var nodeId = node.NodeId;
                if (string.IsNullOrEmpty(nodeId))
                {
                    continue;
                }

                alive.Add(nodeId);
                if (!_entries.TryGetValue(nodeId, out var entry) || entry == null)
                {
                    entry = CreateEntry(nodeId, building);
                    _entries[nodeId] = entry;
                }
                else
                {
                    entry.Building = building;
                }

                if (disableWorldSpaceCityCoreHpBar && building != null && building.IsCityCoreHpBarEnabled)
                {
                    building.SetCityCoreHpBarEnabled(false);
                }

                RefreshEntryVisual(entry);
            }

            var stale = new List<string>();
            foreach (var pair in _entries)
            {
                if (!alive.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                RemoveEntry(stale[i]);
            }
        }

        private Entry CreateEntry(string nodeId, BuildingView building)
        {
            var go = new GameObject($"CityCoreHp_{nodeId}", typeof(RectTransform));
            var root = go.transform as RectTransform;
            root.SetParent(_canvasRect, false);
            root.sizeDelta = entrySize;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            var plateGo = new GameObject("OwnerPlate", typeof(RectTransform), typeof(Image));
            var plateRect = plateGo.transform as RectTransform;
            plateRect.SetParent(root, false);
            plateRect.anchorMin = new Vector2(0f, 0.5f);
            plateRect.anchorMax = new Vector2(0f, 0.5f);
            plateRect.pivot = new Vector2(0f, 0.5f);
            plateRect.sizeDelta = plateSize;
            plateRect.anchoredPosition = new Vector2(0f, 0f);
            var plateImage = plateGo.GetComponent<Image>();
            plateImage.sprite = GetDefaultSprite();

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            var nameRect = nameGo.transform as RectTransform;
            nameRect.SetParent(plateRect, false);
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(4f, 0f);
            nameRect.offsetMax = new Vector2(-4f, 0f);
            var name = nameGo.GetComponent<TextMeshProUGUI>();
            name.fontSize = fontSize;
            name.fontStyle = FontStyles.Bold;
            name.alignment = TextAlignmentOptions.Center;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = false;
            name.extraPadding = true;
            name.raycastTarget = false;

            var barBgGo = new GameObject("HpBarBg", typeof(RectTransform), typeof(Image));
            var barBgRect = barBgGo.transform as RectTransform;
            barBgRect.SetParent(root, false);
            barBgRect.anchorMin = new Vector2(0f, 0.5f);
            barBgRect.anchorMax = new Vector2(0f, 0.5f);
            barBgRect.pivot = new Vector2(0f, 0.5f);
            barBgRect.sizeDelta = barSize;
            barBgRect.anchoredPosition = new Vector2(plateSize.x + spacing, 0f);
            var barBg = barBgGo.GetComponent<Image>();
            barBg.sprite = GetDefaultSprite();
            barBg.color = hpBarBgColor;

            var fillGo = new GameObject("HpBarFill", typeof(RectTransform), typeof(Image));
            var fillRect = fillGo.transform as RectTransform;
            fillRect.SetParent(barBgRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = GetDefaultSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.color = hpBarFillColor;

            root.sizeDelta = new Vector2(plateSize.x + spacing + barSize.x, Mathf.Max(plateSize.y, barSize.y));

            return new Entry
            {
                NodeId = nodeId,
                Building = building,
                Root = root,
                Plate = plateImage,
                Name = name,
                Fill = fill,
                WorldHeightOffset = ComputeHeightOffset(building)
            };
        }

        private void RefreshEntryVisual(Entry entry)
        {
            if (entry == null || entry.Building == null)
            {
                return;
            }

            var ownerColor = ResolveOwnerColor(entry.Building.OwnerId);
            if (entry.Plate != null)
            {
                entry.Plate.color = ownerColor;
            }

            if (entry.Name != null)
            {
                entry.Name.text = "City Core";
                entry.Name.color = Color.white;
            }

            if (entry.Fill != null)
            {
                var maxHp = Mathf.Max(1, entry.Building.MaxHitPoints);
                var ratio = Mathf.Clamp01(entry.Building.HitPoints / (float)maxHp);
                entry.Fill.fillAmount = ratio;
            }
        }

        private void UpdateEntryTransforms()
        {
            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                if (entry == null || entry.Root == null || entry.Building == null)
                {
                    continue;
                }

                var world = entry.Building.transform.position + Vector3.up * Mathf.Max(0.1f, entry.WorldHeightOffset);
                var screen = targetCamera.WorldToScreenPoint(world);

                var visible = true;
                if (hideWhenBehindCamera && screen.z <= 0f)
                {
                    visible = false;
                }

                if (hideWhenOffScreen &&
                    (screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height))
                {
                    visible = false;
                }

                if (!visible)
                {
                    entry.Root.gameObject.SetActive(false);
                    continue;
                }

                entry.Root.gameObject.SetActive(true);
                var sp = new Vector2(screen.x, screen.y + screenYOffset);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, sp, null, out var localPos);
                entry.Root.anchoredPosition = localPos;
            }
        }

        private float ComputeHeightOffset(BuildingView building)
        {
            if (building == null)
            {
                return 1.2f;
            }

            var renderers = building.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return 1.2f;
            }

            var hasBounds = false;
            var bounds = default(Bounds);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (!hasBounds)
            {
                return 1.2f;
            }

            return Mathf.Max(0.3f, bounds.max.y - building.transform.position.y + 0.25f);
        }

        private Color ResolveOwnerColor(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return neutralOwnerColor;
            }

            var myPlayerId = _cache != null ? _cache.MyPlayerID : string.Empty;
            if (!string.IsNullOrWhiteSpace(myPlayerId))
            {
                return string.Equals(ownerId, myPlayerId, System.StringComparison.OrdinalIgnoreCase)
                    ? friendlyOwnerColor
                    : enemyOwnerColor;
            }

            return enemyOwnerColor;
        }

        private Sprite GetDefaultSprite()
        {
            if (_defaultUiSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _defaultUiSprite = tex != null
                    ? Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f))
                    : null;
            }

            return _defaultUiSprite;
        }

        private void HideAll()
        {
            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                if (entry?.Root != null)
                {
                    entry.Root.gameObject.SetActive(false);
                }
            }
        }

        private void RemoveEntry(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            if (_entries.TryGetValue(nodeId, out var entry) && entry != null && entry.Root != null)
            {
                Destroy(entry.Root.gameObject);
            }

            _entries.Remove(nodeId);
        }
    }
}
