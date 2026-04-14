/*************************************************
 * Project: Panoptes
 * File: TechTreePanelController.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Applies tech-tree node config and renders connection lines.
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
        [Serializable]
        private sealed class TechTreeConfigRoot
        {
            public string config_version;
            public TechTreeNodeConfig[] nodes;
            public TechTreeEdgeConfig[] edges;
        }

        [Serializable]
        private sealed class TechTreeNodeConfig
        {
            public string id;
            public string title;
            public string description;
            public string icon_key;
            public float x;
            public float y;
            public float width = 320f;
            public float height = 96f;
            public bool visible = true;
        }

        [Serializable]
        private sealed class TechTreeEdgeConfig
        {
            public string id;
            public string from;
            public string to;
            public string arrow = "auto";
            public bool show_arrow = true;
            public float thickness = 3f;
            public TechTreePoint[] points;
        }

        [Serializable]
        private sealed class TechTreePoint
        {
            public float x;
            public float y;
        }

        [Header("Config Source")]
        [SerializeField] private bool preferServerPushedConfig = true;
        [SerializeField] private bool listenServerConfigUpdates = true;
        [SerializeField] private string serverConfigKey = "techtreeconfig";
        [SerializeField] private TextAsset localConfigJson;
        [SerializeField] private string localConfigResourcesPath = "Config/techtreeconfig";
        [SerializeField] private bool logWarnings = true;

        [Header("Binding")]
        [SerializeField] private RectTransform nodesRoot;
        [SerializeField] private RectTransform lineRoot;

        [Header("Line Style")]
        [SerializeField] private Color lineColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private float defaultLineThickness = 3f;
        [SerializeField] private TMP_FontAsset arrowFont;
        [SerializeField] private float arrowFontSize = 24f;
        [SerializeField] private Color arrowColor = new Color(0.08f, 0.08f, 0.08f, 1f);

        private readonly Dictionary<string, RectTransform> _nodeById = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<RectTransform> _fallbackOrderedNodes = new();
        private readonly List<GameObject> _generatedLineObjects = new();
        private ConfigCache _configCache;
        private string _normalizedServerKey;

        private void Awake()
        {
            _normalizedServerKey = NormalizeKey(serverConfigKey);
            EnsureRoots();
        }

        private void OnEnable()
        {
            SubscribeServerConfig();
            RefreshFromConfig();
        }

        private void OnDisable()
        {
            UnsubscribeServerConfig();
            ClearGeneratedLines();
        }

        private void SubscribeServerConfig()
        {
            if (!listenServerConfigUpdates)
            {
                return;
            }

            _configCache = ConfigCache.EnsureInstance();
            if (_configCache == null)
            {
                return;
            }

            _configCache.ConfigUpdated -= OnServerConfigUpdated;
            _configCache.ConfigUpdated += OnServerConfigUpdated;
        }

        private void UnsubscribeServerConfig()
        {
            if (_configCache == null)
            {
                return;
            }

            _configCache.ConfigUpdated -= OnServerConfigUpdated;
            _configCache = null;
        }

        private void OnServerConfigUpdated(string key)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (NormalizeKey(key) != _normalizedServerKey)
            {
                return;
            }

            RefreshFromConfig();
        }

        private void RefreshFromConfig()
        {
            EnsureRoots();
            var json = TryLoadConfigJson();
            if (string.IsNullOrWhiteSpace(json))
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[TechTreePanel] Missing tech-tree config JSON.");
                }
                return;
            }

            TechTreeConfigRoot config;
            try
            {
                config = JsonUtility.FromJson<TechTreeConfigRoot>(json);
            }
            catch (Exception ex)
            {
                if (logWarnings)
                {
                    Debug.LogWarning($"[TechTreePanel] Failed to parse tech-tree config: {ex.Message}");
                }
                return;
            }

            if (config == null || config.nodes == null || config.nodes.Length == 0)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[TechTreePanel] Config has no nodes.");
                }
                return;
            }

            BuildNodeLookup();
            ApplyNodeConfigs(config.nodes);
            RenderEdges(config.edges);
        }

        private void EnsureRoots()
        {
            if (nodesRoot == null)
            {
                nodesRoot = transform as RectTransform;
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

        private string TryLoadConfigJson()
        {
            if (preferServerPushedConfig && ConfigCache.Instance != null && ConfigCache.Instance.TryGetJson(_normalizedServerKey, out var serverJson))
            {
                return serverJson;
            }

            if (localConfigJson != null && !string.IsNullOrWhiteSpace(localConfigJson.text))
            {
                return localConfigJson.text;
            }

            if (!string.IsNullOrWhiteSpace(localConfigResourcesPath))
            {
                var asset = Resources.Load<TextAsset>(localConfigResourcesPath.Trim());
                if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
                {
                    return asset.text;
                }
            }

            return string.Empty;
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

                if (current == nodesRoot)
                {
                    continue;
                }

                if (current is not RectTransform rect)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(current.name))
                {
                    continue;
                }

                var key = NormalizeKey(current.name);
                if (!_nodeById.ContainsKey(key))
                {
                    _nodeById.Add(key, rect);
                }

                if (rect.GetComponentInChildren<TMP_Text>(true) != null)
                {
                    _fallbackOrderedNodes.Add(rect);
                }
            }
        }

        private void ApplyNodeConfigs(TechTreeNodeConfig[] nodes)
        {
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

                nodeRect.anchoredPosition = new Vector2(node.x, node.y);
                nodeRect.sizeDelta = new Vector2(Mathf.Max(1f, node.width), Mathf.Max(1f, node.height));
                nodeRect.gameObject.SetActive(node.visible);

                AssignNodeTexts(nodeRect, node.title, node.description);
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
                if (titleText == null && (key.Contains("name") || key.Contains("title")))
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

        private void RenderEdges(TechTreeEdgeConfig[] edges)
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
                    var a = new Vector2(edge.points[p].x, edge.points[p].y);
                    var b = new Vector2(edge.points[p + 1].x, edge.points[p + 1].y);
                    CreateLineSegment(a, b, thickness);
                }

                if (edge.show_arrow)
                {
                    var prev = new Vector2(edge.points[edge.points.Length - 2].x, edge.points[edge.points.Length - 2].y);
                    var end = new Vector2(edge.points[edge.points.Length - 1].x, edge.points[edge.points.Length - 1].y);
                    CreateArrow(edge.arrow, prev, end);
                }
            }
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
