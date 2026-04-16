using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class TechTreePanelController : MonoBehaviour
    {
        private sealed class NodeData
        {
            public string Id;
            public string Name;
            public string Desc;
            public string Icon;
            public string Branch;
            public int Tier;
            public int Sort;
            public int Column = -1;
            public int Row = -1;
            public Vector2 Pos;
            public RectTransform Rect;
            public readonly HashSet<string> Pre = new(StringComparer.OrdinalIgnoreCase);
        }

        [Header("Binding")]
        [SerializeField] private RectTransform nodesRoot;
        [SerializeField] private RectTransform lineRoot;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private RectTransform nodeTemplate;

        [Header("Line Style")]
        [SerializeField] private Color lineColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private float defaultLineThickness = 3f;
        [SerializeField] private TMP_FontAsset arrowFont;
        [SerializeField] private int arrowFontSize = 24;
        [SerializeField] private Color arrowColor = new Color(0.08f, 0.08f, 0.08f, 1f);

        [Header("Layout")]
        [SerializeField] private Vector2 layoutOffset = new Vector2(0f, 160f);
        [SerializeField] private float columnSpacing = 300f;
        [SerializeField] private float rowSpacing = 150f;
        [SerializeField] private bool clearLegacyNodesOnRefresh = true;

        [Header("Source")]
        [SerializeField] private bool logWarnings = true;
        [SerializeField] private string iconResourcesRoot = "Icons/Tech";

        [Header("Close")]
        [SerializeField] private Button closeButton;
        [SerializeField] private bool autoCreateCloseButton = false;
        [SerializeField] private string closeButtonText = "Close";
        [SerializeField] private TMP_FontAsset closeButtonFont;

        private readonly List<GameObject> _runtimeNodes = new();
        private readonly List<GameObject> _runtimeLines = new();
        private readonly Dictionary<string, NodeData> _dict = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _cols = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _visiting = new(StringComparer.OrdinalIgnoreCase);
        private StaticCatalogCache _catalog;
        private bool _loggedMissingConfigThisEnable;

        private void Awake()
        {
            EnsureRoots();
            EnsureCloseButton();
            ResolveTemplate();
        }

        private void OnEnable()
        {
            _loggedMissingConfigThisEnable = false;
            _catalog = StaticCatalogCache.EnsureInstance();
            if (_catalog != null) { _catalog.CatalogChanged -= Refresh; _catalog.CatalogChanged += Refresh; }
            Refresh();
        }

        private void OnDisable()
        {
            if (_catalog != null) _catalog.CatalogChanged -= Refresh;
            _loggedMissingConfigThisEnable = false;
        }

        private void Refresh()
        {
            EnsureRoots();
            ResolveTemplate();
            if (_catalog == null) _catalog = StaticCatalogCache.Instance;
            if (_catalog == null)
            {
                WarnMissingConfigOnce("[TechTreePanel] StaticCatalogCache missing.");
                return;
            }

            var nodes = LoadFromCatalog();
            if (nodes.Count == 0) { WarnMissingConfigOnce("[TechTreePanel] No technologies found in config/catalog."); return; }

            Layout(nodes);
            Render(nodes);
        }

        private void WarnMissingConfigOnce(string message)
        {
            if (!logWarnings || _loggedMissingConfigThisEnable)
            {
                return;
            }

            Debug.LogWarning(message);
            _loggedMissingConfigThisEnable = true;
        }

        private List<NodeData> LoadFromCatalog()
        {
            var list = new List<NodeData>();
            foreach (var pair in _catalog.Technologies)
            {
                var t = pair.Value; if (t == null || string.IsNullOrWhiteSpace(t.id)) continue;
                var d = new NodeData { Id = t.id.Trim(), Name = string.IsNullOrWhiteSpace(t.name) ? t.id : t.name, Desc = t.description ?? string.Empty, Icon = t.icon_key ?? string.Empty, Branch = t.branch ?? string.Empty, Tier = t.tier, Sort = t.sort_order };
                if (t.prerequisites != null)
                {
                    for (var i = 0; i < t.prerequisites.Length; i++)
                    {
                        var p = t.prerequisites[i];
                        if (p != null && string.Equals((p.type ?? string.Empty).Trim(), "technology_unlocked", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.target_id))
                            d.Pre.Add(p.target_id.Trim());
                    }
                }
                list.Add(d);
            }
            return list;
        }

        private void Layout(List<NodeData> nodes)
        {
            _dict.Clear(); _cols.Clear(); _visiting.Clear();
            for (var i = 0; i < nodes.Count; i++) { var n = nodes[i]; _dict[n.Id] = n; if (n.Column >= 0) _cols[n.Id] = n.Column; }
            foreach (var n in nodes) ResolveCol(n.Id);

            var groups = new Dictionary<int, List<NodeData>>();
            foreach (var n in nodes)
            {
                var c = _cols.TryGetValue(n.Id, out var v) ? v : 0;
                if (!groups.TryGetValue(c, out var g)) { g = new List<NodeData>(); groups[c] = g; }
                g.Add(n);
            }

            foreach (var pair in groups)
            {
                var col = pair.Key; var g = pair.Value;
                g.Sort((a, b) => a.Sort != b.Sort ? a.Sort.CompareTo(b.Sort) : (a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)));
                var startY = layoutOffset.y + (g.Count - 1) * rowSpacing * 0.5f;
                for (var i = 0; i < g.Count; i++)
                {
                    var row = g[i].Row >= 0 ? g[i].Row : i;
                    var y = g[i].Row >= 0 ? layoutOffset.y - row * rowSpacing : startY - i * rowSpacing;
                    g[i].Pos = new Vector2(layoutOffset.x + col * columnSpacing, y);
                }
            }
        }

        private int ResolveCol(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return 0;
            if (_cols.TryGetValue(id, out var c)) return Mathf.Max(0, c);
            if (!_dict.TryGetValue(id, out var n) || n == null) return 0;
            if (!_visiting.Add(id)) return 0;
            var m = -1;
            foreach (var pre in n.Pre)
            {
                if (_dict.ContainsKey(pre))
                {
                    m = Mathf.Max(m, ResolveCol(pre));
                }
            }

            // Service-aligned fallback: when no explicit prerequisites are provided,
            // keep a layered look by using tier as the implicit column.
            var fallbackCol = n.Tier > 0 ? n.Tier - 1 : 0;
            _visiting.Remove(id);
            _cols[id] = Mathf.Max(fallbackCol, m + 1);
            return _cols[id];
        }

        private void Render(List<NodeData> nodes)
        {
            for (var i = 0; i < _runtimeNodes.Count; i++) if (_runtimeNodes[i] != null) Destroy(_runtimeNodes[i]);
            for (var i = 0; i < _runtimeLines.Count; i++) if (_runtimeLines[i] != null) Destroy(_runtimeLines[i]);
            _runtimeNodes.Clear(); _runtimeLines.Clear();
            if (clearLegacyNodesOnRefresh) HideLegacyNodes();

            for (var i = 0; i < nodes.Count; i++) nodes[i].Rect = CreateNode(nodes[i]);
            for (var i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                foreach (var pre in n.Pre)
                {
                    if (!_dict.TryGetValue(pre, out var p) || p.Rect == null || n.Rect == null) continue;
                    CreateLine(ToLinePoint(p.Rect, new Vector3(p.Rect.rect.xMax, p.Rect.rect.center.y, 0f)), ToLinePoint(n.Rect, new Vector3(n.Rect.rect.xMin, n.Rect.rect.center.y, 0f)));
                }
            }
        }

        private RectTransform CreateNode(NodeData n)
        {
            RectTransform rect;
            if (nodeTemplate != null) { rect = Instantiate(nodeTemplate, nodesRoot, false); rect.gameObject.SetActive(true); }
            else { var go = new GameObject("TechNode", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); rect = go.transform as RectTransform; rect.SetParent(nodesRoot, false); rect.sizeDelta = new Vector2(360f, 104f); }
            rect.name = $"TechNode_{n.Id}";
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = n.Pos;

            var texts = rect.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text title = null, desc = null;
            for (var i = 0; i < texts.Length; i++)
            {
                var k = Key(texts[i].gameObject.name);
                if (title == null && (k.Contains("name") || k.Contains("title") || k == "label")) title = texts[i];
                else if (desc == null && (k.Contains("desc") || k.Contains("detail"))) desc = texts[i];
            }
            if (title == null && texts.Length > 0) title = texts[0];
            if (desc == null && texts.Length > 1) desc = texts[texts.Length - 1];
            if (title != null) title.text = n.Name;
            if (desc != null && !ReferenceEquals(desc, title)) desc.text = n.Desc;

            var imgs = rect.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < imgs.Length; i++)
            {
                if (!Key(imgs[i].gameObject.name).Contains("icon")) continue;
                var sp = LoadIcon(n.Icon); if (sp != null) { imgs[i].sprite = sp; imgs[i].color = Color.white; imgs[i].preserveAspect = true; } else imgs[i].color = new Color(1f, 1f, 1f, 0.75f);
                break;
            }

            _runtimeNodes.Add(rect.gameObject);
            return rect;
        }

        private void CreateLine(Vector2 start, Vector2 end)
        {
            var root = lineRoot != null ? lineRoot : nodesRoot; if (root == null) return;
            var go = new GameObject("TechEdge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.transform as RectTransform; rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var dir = end - start; var len = Mathf.Max(1f, dir.magnitude);
            rect.sizeDelta = new Vector2(len, Mathf.Max(1f, defaultLineThickness));
            rect.anchoredPosition = (start + end) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            var img = go.GetComponent<Image>(); img.color = lineColor; img.raycastTarget = false;
            _runtimeLines.Add(go);
        }

        private Vector2 ToLinePoint(RectTransform source, Vector3 local)
        {
            var root = lineRoot != null ? lineRoot : nodesRoot;
            return root.InverseTransformPoint(source.TransformPoint(local));
        }

        private void EnsureRoots()
        {
            if (panelRoot == null) panelRoot = transform as RectTransform;
            if (nodesRoot == null) nodesRoot = panelRoot != null ? panelRoot : transform as RectTransform;
            if (lineRoot == null) lineRoot = nodesRoot;
        }

        private void ResolveTemplate()
        {
            if (nodesRoot == null) return;
            if (nodeTemplate != null) { nodeTemplate.gameObject.SetActive(false); return; }
            for (var i = 0; i < nodesRoot.childCount; i++)
            {
                var c = nodesRoot.GetChild(i) as RectTransform; if (c == null) continue;
                if (Key(c.name).Contains("technodeitem")) { nodeTemplate = c; nodeTemplate.gameObject.SetActive(false); break; }
            }
        }

        private void HideLegacyNodes()
        {
            if (nodesRoot == null) return;
            for (var i = 0; i < nodesRoot.childCount; i++)
            {
                var c = nodesRoot.GetChild(i) as RectTransform; if (c == null) continue;
                var k = Key(c.name);
                if (k.Contains("btnclose")) continue;
                if (nodeTemplate != null && ReferenceEquals(c, nodeTemplate)) { c.gameObject.SetActive(false); continue; }
                if (_runtimeNodes.Contains(c.gameObject) || _runtimeLines.Contains(c.gameObject)) continue;
                c.gameObject.SetActive(false);
            }
        }

        private void EnsureCloseButton()
        {
            if (closeButton == null)
            {
                var existing = transform.Find("BtnCloseTechTree");
                if (existing != null) closeButton = existing.GetComponent<Button>();
            }

            if (closeButton == null && autoCreateCloseButton && panelRoot != null)
            {
                var go = new GameObject("BtnCloseTechTree", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                var rect = go.GetComponent<RectTransform>();
                rect.SetParent(panelRoot, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-20f, -20f); rect.sizeDelta = new Vector2(120f, 44f);
                go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
                closeButton = go.GetComponent<Button>();
                var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                var lr = label.rectTransform; lr.SetParent(rect, false); lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(6f, 4f); lr.offsetMax = new Vector2(-6f, -4f);
                label.text = string.IsNullOrWhiteSpace(closeButtonText) ? "Close" : closeButtonText; label.alignment = TextAlignmentOptions.Center; label.fontSize = 20f; label.color = Color.white; label.enableWordWrapping = false;
                if (closeButtonFont == null) closeButtonFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Panoptes CJK Fallback");
                if (closeButtonFont != null) label.font = closeButtonFont;
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HidePanel);
                closeButton.onClick.AddListener(HidePanel);
            }
        }

        private void HidePanel() => gameObject.SetActive(false);

        private Sprite LoadIcon(string iconKey)
        {
            iconKey = (iconKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(iconKey)) return null;
            var root = (iconResourcesRoot ?? string.Empty).Trim().Trim('/');
            if (!string.IsNullOrEmpty(root))
            {
                var sp = Resources.Load<Sprite>($"{root}/{iconKey}");
                if (sp != null) return sp;
            }
            return Resources.Load<Sprite>(iconKey);
        }

        private static string Key(string s) => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim().ToLowerInvariant();
    }
}
