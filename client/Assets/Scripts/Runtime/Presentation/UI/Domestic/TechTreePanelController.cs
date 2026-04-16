/*************************************************
 * Project: Panoptes
 * File: TechTreePanelController.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Initializes technology tree panel from server static catalog snapshot.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class TechTreePanelController : MonoBehaviour
    {
        [Header("Binding")]
        [SerializeField] private RectTransform nodesRoot;
        [SerializeField] private RectTransform lineRoot;
        [SerializeField] private RectTransform panelRoot;

        [Header("Line Style")]
        [SerializeField] private Color lineColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private float defaultLineThickness = 3f;
        [SerializeField] private TMP_FontAsset arrowFont;
        [SerializeField] private float arrowFontSize = 24f;
        [SerializeField] private Color arrowColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private bool logWarnings = true;
        [SerializeField] private bool waitForServerSnapshot = true;
        [SerializeField] private bool allowLocalBundleFallback = false;
        [SerializeField] private Vector2 layoutOffset = new Vector2(0f, 160f);

        [Header("Close Button")]
        [SerializeField] private Button closeButton;
        [SerializeField] private bool autoCreateCloseButton = false;
        [SerializeField] private string closeButtonText = "Close";
        [SerializeField] private TMP_FontAsset closeButtonFont;

        private readonly Dictionary<string, RectTransform> _nodeById = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<RectTransform> _fallbackOrderedNodes = new();
        private readonly List<GameObject> _generatedLineObjects = new();
        private StaticCatalogCache _catalogCache;
        private bool _localFallbackAttempted;

        private void Awake()
        {
            EnsureRoots();
            EnsureCloseButton();
        }

        private void OnEnable()
        {
            _catalogCache = StaticCatalogCache.EnsureInstance();
            if (_catalogCache != null)
            {
                _catalogCache.CatalogChanged -= OnCatalogChanged;
                _catalogCache.CatalogChanged += OnCatalogChanged;
            }

            RefreshFromServerCatalog();
        }

        private void OnDisable()
        {
            if (_catalogCache != null)
            {
                _catalogCache.CatalogChanged -= OnCatalogChanged;
                _catalogCache = null;
            }

            ClearGeneratedLines();
            _localFallbackAttempted = false;
        }

        private void OnCatalogChanged()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            RefreshFromServerCatalog();
        }

        private void RefreshFromServerCatalog()
        {
            EnsureRoots();
            var cache = _catalogCache != null ? _catalogCache : StaticCatalogCache.Instance;
            if (cache == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[TechTreePanel] Missing technology tree layout from server snapshot.");
                }
                return;
            }

            if (waitForServerSnapshot && !cache.HasServerSnapshot)
            {
                return;
            }

            if (!cache.TryGetTechnologyTree(out var tree))
            {
                if (allowLocalBundleFallback && !_localFallbackAttempted)
                {
                    _localFallbackAttempted = true;
                    cache.LoadLocalCatalog();
                }

                if (!cache.TryGetTechnologyTree(out tree))
                {
                    if (logWarnings)
                    {
                        Debug.LogWarning("[TechTreePanel] Missing technology tree layout from server snapshot.");
                    }
                    return;
                }
            }

            BuildNodeLookup();
            ApplyNodeConfigs(tree.nodes, cache);
            RenderEdges(tree.edges);
        }

        private void EnsureRoots()
        {
            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            if (nodesRoot == null)
            {
                nodesRoot = panelRoot != null ? panelRoot : (transform as RectTransform);
            }

            if (lineRoot == null)
            {
                var existing = transform.Find("TechTreeLines");
                if (existing != null)
                {
                    lineRoot = existing as RectTransform;
                }
                else
                {
                    var go = new GameObject("TechTreeLines", typeof(RectTransform));
                    var rect = go.GetComponent<RectTransform>();
                    rect.SetParent(transform, false);
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    lineRoot = rect;
                }
            }
        }

        private void BuildNodeLookup()
        {
            _nodeById.Clear();
            _fallbackOrderedNodes.Clear();
            if (nodesRoot == null)
            {
                return;
            }

            var queue = new Queue<Transform>();
            queue.Enqueue(nodesRoot);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (var i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    queue.Enqueue(child);
                }

                if (current == nodesRoot || current is not RectTransform rect)
                {
                    continue;
                }

                var key = NormalizeKey(current.name);
                if (!string.IsNullOrEmpty(key) && !_nodeById.ContainsKey(key))
                {
                    _nodeById.Add(key, rect);
                }

                var normalizedName = NormalizeKey(rect.name);
                if (normalizedName.Contains("btnclose"))
                {
                    continue;
                }

                if (rect.GetComponentInChildren<TMP_Text>(true) != null)
                {
                    _fallbackOrderedNodes.Add(rect);
                }
            }
        }

        private void ApplyNodeConfigs(StaticCatalogCache.TechnologyTreeNodeJson[] nodes, StaticCatalogCache cache)
        {
            if (nodes == null || nodes.Length == 0)
            {
                return;
            }

            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.id))
                {
                    continue;
                }

                if (!TryFindNode(node.id, out var nodeRect))
                {
                    if (i >= 0 && i < _fallbackOrderedNodes.Count)
                    {
                        nodeRect = _fallbackOrderedNodes[i];
                    }
                    else
                    {
                        if (logWarnings)
                        {
                            Debug.LogWarning($"[TechTreePanel] Node '{node.id}' not found in panel.");
                        }
                        continue;
                    }
                }

                nodeRect.anchoredPosition = new Vector2(node.x, node.y) + layoutOffset;
                nodeRect.sizeDelta = new Vector2(Mathf.Max(1f, node.width), Mathf.Max(1f, node.height));
                nodeRect.gameObject.SetActive(node.visible);

                var title = node.title ?? string.Empty;
                var description = node.description ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(node.technology_id) &&
                    cache.TryGetTechnology(node.technology_id, out var technology) &&
                    technology != null)
                {
                    if (!string.IsNullOrWhiteSpace(technology.name))
                    {
                        title = technology.name;
                    }
                    if (!string.IsNullOrWhiteSpace(technology.description))
                    {
                        description = technology.description;
                    }
                }

                AssignNodeTexts(nodeRect, title, description);
            }
        }

        private void AssignNodeTexts(RectTransform nodeRect, string title, string description)
        {
            var texts = nodeRect.GetComponentsInChildren<TMP_Text>(true);
            if (texts == null || texts.Length == 0)
            {
                return;
            }

            TMP_Text titleText = null;
            TMP_Text descText = null;
            for (var i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                var key = NormalizeKey(t.gameObject.name);
                if (titleText == null && (key.Contains("name") || key.Contains("title") || key == "label"))
                {
                    titleText = t;
                }

                if (descText == null && (key.Contains("desc") || key.Contains("description") || key.Contains("detail")))
                {
                    descText = t;
                }
            }

            if (titleText == null || descText == null)
            {
                Array.Sort(texts, (a, b) =>
                {
                    var ay = (a.transform as RectTransform)?.anchoredPosition.y ?? 0f;
                    var by = (b.transform as RectTransform)?.anchoredPosition.y ?? 0f;
                    return by.CompareTo(ay);
                });

                if (titleText == null)
                {
                    titleText = texts[0];
                }
                if (descText == null && texts.Length > 1)
                {
                    descText = texts[texts.Length - 1];
                }
            }

            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
            }

            if (descText != null && !ReferenceEquals(descText, titleText))
            {
                descText.text = description ?? string.Empty;
            }
            else if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(description)
                    ? (title ?? string.Empty)
                    : $"{title}\n{description}";
            }
        }

        private void RenderEdges(StaticCatalogCache.TechnologyTreeEdgeJson[] edges)
        {
            ClearGeneratedLines();
            if (lineRoot == null || edges == null || edges.Length == 0)
            {
                return;
            }

            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (edge?.points == null || edge.points.Length < 2)
                {
                    continue;
                }

                var thickness = edge.thickness > 0f ? edge.thickness : defaultLineThickness;

                for (var p = 0; p < edge.points.Length - 1; p++)
                {
                    var a = new Vector2(edge.points[p].x, edge.points[p].y) + layoutOffset;
                    var b = new Vector2(edge.points[p + 1].x, edge.points[p + 1].y) + layoutOffset;
                    CreateLineSegment(a, b, thickness);
                }

                if (edge.show_arrow)
                {
                    var prev = new Vector2(edge.points[edge.points.Length - 2].x, edge.points[edge.points.Length - 2].y) + layoutOffset;
                    var end = new Vector2(edge.points[edge.points.Length - 1].x, edge.points[edge.points.Length - 1].y) + layoutOffset;
                    CreateArrow(edge.arrow, prev, end);
                }
            }
        }

        private void EnsureCloseButton()
        {
            if (closeButton == null)
            {
                var existing = transform.Find("BtnCloseTechTree");
                if (existing != null)
                {
                    closeButton = existing.GetComponent<Button>();
                }
            }

            if (closeButton == null && autoCreateCloseButton && panelRoot != null &&
                string.Equals(gameObject.name, "TechTreePanel", StringComparison.OrdinalIgnoreCase))
            {
                var go = new GameObject("BtnCloseTechTree", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                var rect = go.GetComponent<RectTransform>();
                rect.SetParent(panelRoot, false);
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-20f, -20f);
                rect.sizeDelta = new Vector2(120f, 44f);

                var image = go.GetComponent<Image>();
                image.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

                closeButton = go.GetComponent<Button>();

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                var labelRect = labelGo.GetComponent<RectTransform>();
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(6f, 4f);
                labelRect.offsetMax = new Vector2(-6f, -4f);

                var label = labelGo.GetComponent<TextMeshProUGUI>();
                label.text = string.IsNullOrWhiteSpace(closeButtonText) ? "Close" : closeButtonText;
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 20f;
                label.color = Color.white;
                label.enableWordWrapping = false;
                if (closeButtonFont == null)
                {
                    closeButtonFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Panoptes CJK Fallback");
                }
                if (closeButtonFont != null)
                {
                    label.font = closeButtonFont;
                }
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HidePanel);
                closeButton.onClick.AddListener(HidePanel);
            }
        }

        private void HidePanel()
        {
            gameObject.SetActive(false);
        }

        private void CreateLineSegment(Vector2 a, Vector2 b, float thickness)
        {
            var go = new GameObject("EdgeSegment", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(lineRoot, false);

            var rect = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();
            img.color = lineColor;
            img.raycastTarget = false;

            var delta = b - a;
            var length = delta.magnitude;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(length, Mathf.Max(1f, thickness));
            rect.anchoredPosition = (a + b) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            _generatedLineObjects.Add(go);
        }

        private void CreateArrow(string mode, Vector2 prev, Vector2 end)
        {
            var go = new GameObject("EdgeArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(lineRoot, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(24f, 24f);
            rect.anchoredPosition = end;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = ResolveArrowGlyph(mode, prev, end);
            tmp.fontSize = arrowFontSize;
            tmp.color = arrowColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            if (arrowFont == null)
            {
                arrowFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Panoptes CJK Fallback");
            }

            if (arrowFont != null)
            {
                tmp.font = arrowFont;
            }

            _generatedLineObjects.Add(go);
        }

        private static string ResolveArrowGlyph(string mode, Vector2 prev, Vector2 end)
        {
            var token = NormalizeKey(mode);
            if (token == "up") return "▲";
            if (token == "down") return "▼";
            if (token == "left") return "◀";
            if (token == "right") return "▶";

            var dir = (end - prev).normalized;
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            {
                return dir.x >= 0f ? "▶" : "◀";
            }

            return dir.y >= 0f ? "▲" : "▼";
        }

        private void ClearGeneratedLines()
        {
            for (var i = 0; i < _generatedLineObjects.Count; i++)
            {
                var go = _generatedLineObjects[i];
                if (go == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(go);
                }
                else
                {
                    DestroyImmediate(go);
                }
            }

            _generatedLineObjects.Clear();
        }

        private bool TryFindNode(string nodeId, out RectTransform rect)
        {
            rect = null;
            var key = NormalizeKey(nodeId);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (_nodeById.TryGetValue(key, out rect))
            {
                return rect != null;
            }

            var transformNode = transform.Find(nodeId);
            if (transformNode is RectTransform direct)
            {
                rect = direct;
                _nodeById[key] = direct;
                return true;
            }

            return false;
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
