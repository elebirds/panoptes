using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Composition;
using Panoptes.Presentation.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class TechTreePanelController : MonoBehaviour
    {
        private sealed class NodeData
        {
            public string Id;
            public string Name;
            public string Description;
            public string IconKey;
            public int Tier;
            public int SortOrder;
            public Vector2 Position;
            public Vector2 Size = new(420f, 132f);
            public bool HasExplicitLayout;
            public RectTransform Rect;
            public readonly HashSet<string> Prerequisites = new(StringComparer.OrdinalIgnoreCase);
        }

        [Header("Binding")]
        [SerializeField] private RectTransform nodesRoot;
        [SerializeField] private RectTransform lineRoot;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private GameObject nodePrefab;
        [SerializeField] private RectTransform nodeTemplate;

        [Header("Line Style")]
        [SerializeField] private Color lineColor = new(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private float defaultLineThickness = 3f;

        [Header("Layout")]
        [SerializeField] private Vector2 layoutOffset = new(0f, 160f);
        [SerializeField] private float columnSpacing = 300f;
        [SerializeField] private float rowSpacing = 190f;
        [SerializeField] private bool clearLegacyNodesOnRefresh = true;

        [Header("Source")]
        [SerializeField] private bool logWarnings = true;
        [SerializeField] private string iconResourcesRoot = "Icons/Tech";

        [Header("Close")]
        [SerializeField] private Button closeButton;
        [SerializeField] private bool autoCreateCloseButton = false;
        [SerializeField] private string closeButtonText = "Close";
        [SerializeField] private TMP_FontAsset closeButtonFont;

        private readonly TechTreePanelStateBuilder _stateBuilder = new();
        private readonly List<GameObject> _runtimeNodes = new();
        private readonly List<GameObject> _runtimeLines = new();
        private readonly Dictionary<string, NodeData> _nodesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _columnsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _visiting = new(StringComparer.OrdinalIgnoreCase);

        private StaticCatalogCache _catalog;
        private GameStateCache _gameState;
        private PlanningDraftCache _planningDraft;
        private GameIntentService _gameIntentService;
        private bool _loggedMissingConfigThisEnable;
        private bool _cityCoreOverlaySuppressed;

        [Inject]
        private void Construct(GameIntentService gameIntentService)
        {
            _gameIntentService = gameIntentService;
        }

        private void Awake()
        {
            SceneCommandServiceInjector.InjectIfAvailable(this);
            EnsureRoots();
            EnsureCloseButton();
            ResolveTemplateFallback();
        }

        private void OnEnable()
        {
            _loggedMissingConfigThisEnable = false;
            EnsureRoots();
            EnsureCloseButton();
            RefreshSubscriptions();
            Refresh();
            ApplyCityCoreOverlaySuppression(true);
        }

        private void OnDisable()
        {
            Unsubscribe();
            _loggedMissingConfigThisEnable = false;
            ApplyCityCoreOverlaySuppression(false);
        }

        private void OnDestroy()
        {
            ApplyCityCoreOverlaySuppression(false);
        }

        private void ApplyCityCoreOverlaySuppression(bool suppress)
        {
            if (suppress)
            {
                if (_cityCoreOverlaySuppressed)
                {
                    return;
                }

                CityCoreHpBarOverlayController.PushUiSuppression();
                _cityCoreOverlaySuppressed = true;
                return;
            }

            if (!_cityCoreOverlaySuppressed)
            {
                return;
            }

            CityCoreHpBarOverlayController.PopUiSuppression();
            _cityCoreOverlaySuppressed = false;
        }

        private void OnActionLockChanged(bool _)
        {
            Refresh();
        }

        private void OnPlanningDraftChanged()
        {
            Refresh();
        }

        private void RefreshSubscriptions()
        {
            var nextCatalog = StaticCatalogCache.EnsureInstance();
            var nextGameState = GameStateCache.Instance;
            var nextPlanningDraft = PlanningDraftCache.Instance ?? PlanningDraftCache.EnsureInstance();

            var changed = !ReferenceEquals(_catalog, nextCatalog) ||
                          !ReferenceEquals(_gameState, nextGameState) ||
                          !ReferenceEquals(_planningDraft, nextPlanningDraft);
            if (!changed)
            {
                return;
            }

            Unsubscribe();

            _catalog = nextCatalog;
            _gameState = nextGameState;
            _planningDraft = nextPlanningDraft;

            if (_catalog != null)
            {
                _catalog.CatalogChanged -= Refresh;
                _catalog.CatalogChanged += Refresh;
            }

            if (_gameState != null)
            {
                _gameState.OnStateChanged -= Refresh;
                _gameState.OnStateChanged += Refresh;
            }

            if (_planningDraft != null)
            {
                _planningDraft.OrdersChanged -= OnPlanningDraftChanged;
                _planningDraft.OrdersChanged += OnPlanningDraftChanged;
            }

            ActionLock.OnChanged -= OnActionLockChanged;
            ActionLock.OnChanged += OnActionLockChanged;
        }

        private void Unsubscribe()
        {
            if (_catalog != null)
            {
                _catalog.CatalogChanged -= Refresh;
            }

            if (_gameState != null)
            {
                _gameState.OnStateChanged -= Refresh;
            }

            if (_planningDraft != null)
            {
                _planningDraft.OrdersChanged -= OnPlanningDraftChanged;
            }

            ActionLock.OnChanged -= OnActionLockChanged;
        }

        private void Refresh()
        {
            EnsureRoots();
            EnsureCloseButton();
            ResolveTemplateFallback();
            RefreshSubscriptions();

            if (_catalog == null)
            {
                WarnMissingConfigOnce("[TechTreePanel] StaticCatalogCache missing.");
                return;
            }

            var nodes = LoadFromCatalog();
            if (nodes.Count == 0)
            {
                WarnMissingConfigOnce("[TechTreePanel] No technologies found in config/catalog.");
                return;
            }

            Layout(nodes);
            var runtimeStates = _stateBuilder.Build(new TechTreePanelStateBuilder.BuildInput
            {
                Technologies = _catalog != null
                    ? new List<StaticCatalogCache.TechnologyEntryJson>(_catalog.Technologies.Values)
                    : Array.Empty<StaticCatalogCache.TechnologyEntryJson>(),
                ResearchState = GetCurrentResearchState(),
                PlannedResearchTargetTechnologyId = _planningDraft != null ? _planningDraft.PlannedResearchTargetTechnologyId : string.Empty,
                Phase = _gameState != null ? _gameState.Phase : string.Empty,
                IsActionLocked = ActionLock.IsLocked
            });
            Render(nodes, runtimeStates);
        }

        private TechnologyDto GetCurrentResearchState()
        {
            return _gameState != null ? _gameState.GetCurrentResearchState() : new TechnologyDto();
        }

        private List<NodeData> LoadFromCatalog()
        {
            var result = new List<NodeData>();
            var layoutByTechnology = new Dictionary<string, StaticCatalogCache.TechTreeLayoutNodeJson>(StringComparer.OrdinalIgnoreCase);
            var layout = _catalog.TechTreeLayout;
            if (layout?.nodes != null)
            {
                for (var i = 0; i < layout.nodes.Length; i++)
                {
                    var node = layout.nodes[i];
                    if (node == null || string.IsNullOrWhiteSpace(node.technology_id) || !node.visible)
                    {
                        continue;
                    }

                    layoutByTechnology[Normalize(node.technology_id)] = node;
                }
            }

            foreach (var pair in _catalog.Technologies)
            {
                var technology = pair.Value;
                if (technology == null || string.IsNullOrWhiteSpace(technology.id))
                {
                    continue;
                }

                var node = new NodeData
                {
                    Id = Normalize(technology.id),
                    Name = string.IsNullOrWhiteSpace(technology.name) ? technology.id.Trim() : technology.name.Trim(),
                    Description = technology.description ?? string.Empty,
                    IconKey = technology.icon_key ?? string.Empty,
                    Tier = technology.tier,
                    SortOrder = technology.sort_order
                };

                if (layoutByTechnology.TryGetValue(node.Id, out var layoutNode) && layoutNode != null)
                {
                    if (!string.IsNullOrWhiteSpace(layoutNode.title))
                    {
                        node.Name = layoutNode.title;
                    }

                    if (!string.IsNullOrWhiteSpace(layoutNode.description))
                    {
                        node.Description = layoutNode.description;
                    }

                    node.Position = new Vector2(layoutOffset.x + layoutNode.x, layoutOffset.y - layoutNode.y);
                    node.Size = new Vector2(Mathf.Max(1f, layoutNode.width), Mathf.Max(1f, layoutNode.height));
                    node.HasExplicitLayout = true;
                }

                if (technology.prerequisites != null)
                {
                    for (var i = 0; i < technology.prerequisites.Length; i++)
                    {
                        var prerequisite = technology.prerequisites[i];
                        if (prerequisite == null)
                        {
                            continue;
                        }

                        if (!string.Equals(Normalize(prerequisite.type), "technology_unlocked", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var targetId = Normalize(prerequisite.target_id);
                        if (!string.IsNullOrWhiteSpace(targetId))
                        {
                            node.Prerequisites.Add(targetId);
                        }
                    }
                }

                result.Add(node);
            }

            return result;
        }

        private void Layout(List<NodeData> nodes)
        {
            _nodesById.Clear();
            _columnsById.Clear();
            _visiting.Clear();

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                _nodesById[node.Id] = node;
            }

            foreach (var node in nodes)
            {
                ResolveColumn(node.Id);
            }

            var groups = new Dictionary<int, List<NodeData>>();
            foreach (var node in nodes)
            {
                var column = _columnsById.TryGetValue(node.Id, out var value) ? value : 0;
                if (!groups.TryGetValue(column, out var group))
                {
                    group = new List<NodeData>();
                    groups[column] = group;
                }

                group.Add(node);
            }

            foreach (var pair in groups)
            {
                var column = pair.Key;
                var group = pair.Value;
                group.Sort((left, right) =>
                {
                    if (left.SortOrder != right.SortOrder)
                    {
                        return left.SortOrder.CompareTo(right.SortOrder);
                    }

                    if (left.Tier != right.Tier)
                    {
                        return left.Tier.CompareTo(right.Tier);
                    }

                    return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                });

                var startY = layoutOffset.y + (group.Count - 1) * rowSpacing * 0.5f;
                for (var i = 0; i < group.Count; i++)
                {
                    if (group[i].HasExplicitLayout)
                    {
                        continue;
                    }

                    group[i].Position = new Vector2(layoutOffset.x + column * columnSpacing, startY - i * rowSpacing);
                }
            }
        }

        private int ResolveColumn(string technologyId)
        {
            if (string.IsNullOrWhiteSpace(technologyId))
            {
                return 0;
            }

            if (_columnsById.TryGetValue(technologyId, out var cached))
            {
                return cached;
            }

            if (!_nodesById.TryGetValue(technologyId, out var node) || node == null)
            {
                return 0;
            }

            if (!_visiting.Add(technologyId))
            {
                return 0;
            }

            var maxDependencyColumn = -1;
            foreach (var prerequisite in node.Prerequisites)
            {
                if (_nodesById.ContainsKey(prerequisite))
                {
                    maxDependencyColumn = Mathf.Max(maxDependencyColumn, ResolveColumn(prerequisite));
                }
            }

            _visiting.Remove(technologyId);
            var fallbackColumn = node.Tier > 0 ? node.Tier - 1 : 0;
            _columnsById[technologyId] = Mathf.Max(fallbackColumn, maxDependencyColumn + 1);
            return _columnsById[technologyId];
        }

        private void Render(IReadOnlyList<NodeData> nodes, IReadOnlyDictionary<string, TechTreeNodeRuntimeState> runtimeStates)
        {
            for (var i = 0; i < _runtimeNodes.Count; i++)
            {
                if (_runtimeNodes[i] != null)
                {
                    Destroy(_runtimeNodes[i]);
                }
            }

            for (var i = 0; i < _runtimeLines.Count; i++)
            {
                if (_runtimeLines[i] != null)
                {
                    Destroy(_runtimeLines[i]);
                }
            }

            _runtimeNodes.Clear();
            _runtimeLines.Clear();

            if (clearLegacyNodesOnRefresh)
            {
                HideLegacyNodes();
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                nodes[i].Rect = CreateNode(nodes[i], runtimeStates);
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                foreach (var prerequisite in node.Prerequisites)
                {
                    if (!_nodesById.TryGetValue(prerequisite, out var prerequisiteNode) ||
                        prerequisiteNode.Rect == null ||
                        node.Rect == null)
                    {
                        continue;
                    }

                    CreateLine(
                        ToLinePoint(prerequisiteNode.Rect, new Vector3(prerequisiteNode.Rect.rect.xMax, prerequisiteNode.Rect.rect.center.y, 0f)),
                        ToLinePoint(node.Rect, new Vector3(node.Rect.rect.xMin, node.Rect.rect.center.y, 0f)));
                }
            }
        }

        private RectTransform CreateNode(NodeData node, IReadOnlyDictionary<string, TechTreeNodeRuntimeState> runtimeStates)
        {
            var view = CreateNodeViewInstance();
            var rect = view.transform as RectTransform;
            rect.name = $"TechNode_{node.Id}";
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = node.Position;
            rect.sizeDelta = node.Size;

            runtimeStates.TryGetValue(node.Id, out var runtimeState);
            runtimeState ??= new TechTreeNodeRuntimeState
            {
                TechnologyId = node.Id,
                Status = TechTreeNodeStatus.Locked,
                CurrentProgress = 0,
                RequiredProgress = 0,
                IsInteractable = false
            };

            var model = new TechTreeNodeRenderModel
            {
                TechnologyId = node.Id,
                Title = node.Name,
                Description = node.Description,
                IconKey = node.IconKey,
                Status = runtimeState.Status,
                StatusLabel = TechTreePanelStateBuilder.GetStatusLabel(runtimeState.Status),
                CurrentProgress = runtimeState.CurrentProgress,
                RequiredProgress = runtimeState.RequiredProgress,
                IsInteractable = runtimeState.IsInteractable
            };

            view.Bind(model, LoadIcon(node.IconKey), OnTechnologyClicked);
            _runtimeNodes.Add(rect.gameObject);
            return rect;
        }

        private TechTreeNodeView CreateNodeViewInstance()
        {
            GameObject instance = null;
            if (nodePrefab != null)
            {
                instance = Instantiate(nodePrefab, nodesRoot, false);
            }
            else if (nodeTemplate != null)
            {
                instance = Instantiate(nodeTemplate.gameObject, nodesRoot, false);
                instance.SetActive(true);
            }

            if (instance == null)
            {
                return CreateFallbackNodeView();
            }

            var view = instance.GetComponent<TechTreeNodeView>();
            if (view == null)
            {
                view = instance.AddComponent<TechTreeNodeView>();
            }

            return view;
        }

        private TechTreeNodeView CreateFallbackNodeView()
        {
            var root = CreateUiNode("TechNodeItem", nodesRoot, typeof(Image), typeof(Button), typeof(Outline));
            root.GetComponent<Image>().color = new Color(0.94f, 0.95f, 0.98f, 0.98f);

            var stateFrame = CreateUiNode("StateFrame", root.transform, typeof(Image));
            ConfigureStretch(stateFrame.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            stateFrame.GetComponent<Image>().color = new Color(0.23f, 0.33f, 0.48f, 1f);

            var icon = CreateUiNode("Icon", root.transform, typeof(Image));
            ConfigureAnchored(icon.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(64f, 64f), new Vector2(0f, 0.5f));

            var content = CreateUiNode("Content", root.transform);
            ConfigureStretch(content.GetComponent<RectTransform>(), 104f, 18f, 26f, 54f);

            var title = CreateTextNode("TitleText", content.transform, 26f, FontStyles.Bold);
            ConfigureStretch(title.rectTransform, 0f, 0f, 0f, 32f);

            var description = CreateTextNode("DescriptionText", content.transform, 18f, FontStyles.Normal);
            ConfigureStretch(description.rectTransform, 0f, 0f, 36f, 0f);
            description.textWrappingMode = TextWrappingModes.Normal;

            var statusBadge = CreateUiNode("StatusBadge", root.transform, typeof(Image));
            ConfigureAnchored(statusBadge.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -22f), new Vector2(154f, 34f), new Vector2(1f, 1f));
            statusBadge.GetComponent<Image>().color = new Color(0.18f, 0.43f, 0.85f, 1f);

            var statusText = CreateTextNode("StatusBadgeText", statusBadge.transform, 17f, FontStyles.Bold);
            ConfigureStretch(statusText.rectTransform, 8f, 8f, 4f, 4f);
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.white;

            var progressRoot = CreateUiNode("ProgressRoot", root.transform, typeof(Image));
            ConfigureAnchored(progressRoot.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 18f), new Vector2(190f, 22f), new Vector2(1f, 0f));
            progressRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);

            var progressFill = CreateUiNode("ProgressFill", progressRoot.transform, typeof(Image));
            ConfigureStretch(progressFill.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            progressFill.GetComponent<Image>().color = new Color(0.23f, 0.51f, 0.91f, 1f);

            var progressText = CreateTextNode("ProgressText", progressRoot.transform, 15f, FontStyles.Normal);
            ConfigureStretch(progressText.rectTransform, 8f, 8f, 0f, 0f);
            progressText.alignment = TextAlignmentOptions.Center;

            return root.AddComponent<TechTreeNodeView>();
        }

        private static GameObject CreateUiNode(string name, Transform parent, params Type[] components)
        {
            var componentTypes = new Type[components.Length + 2];
            componentTypes[0] = typeof(RectTransform);
            componentTypes[1] = typeof(CanvasRenderer);
            for (var i = 0; i < components.Length; i++)
            {
                componentTypes[i + 2] = components[i];
            }

            var go = new GameObject(name, componentTypes);
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return go;
        }

        private static TextMeshProUGUI CreateTextNode(string name, Transform parent, float fontSize, FontStyles fontStyle)
        {
            var go = CreateUiNode(name, parent);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = new Color(0.1f, 0.12f, 0.16f, 1f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = string.Empty;
            return text;
        }

        private static void ConfigureStretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void ConfigureAnchored(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private void OnTechnologyClicked(string technologyId)
        {
            if (string.IsNullOrWhiteSpace(technologyId))
            {
                return;
            }

            _gameIntentService?.SetResearchTarget(technologyId);
        }

        private void CreateLine(Vector2 start, Vector2 end)
        {
            var root = lineRoot != null ? lineRoot : nodesRoot;
            if (root == null)
            {
                return;
            }

            var go = new GameObject("TechEdge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.transform as RectTransform;
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var direction = end - start;
            var length = Mathf.Max(1f, direction.magnitude);
            rect.sizeDelta = new Vector2(length, Mathf.Max(1f, defaultLineThickness));
            rect.anchoredPosition = (start + end) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            var image = go.GetComponent<Image>();
            image.color = lineColor;
            image.raycastTarget = false;
            _runtimeLines.Add(go);
        }

        private Vector2 ToLinePoint(RectTransform source, Vector3 localPosition)
        {
            var root = lineRoot != null ? lineRoot : nodesRoot;
            return root.InverseTransformPoint(source.TransformPoint(localPosition));
        }

        private void EnsureRoots()
        {
            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            if (nodesRoot == null)
            {
                nodesRoot = panelRoot != null ? panelRoot : transform as RectTransform;
            }

            if (lineRoot == null)
            {
                lineRoot = nodesRoot;
            }
        }

        private void ResolveTemplateFallback()
        {
            if (nodeTemplate != null || nodesRoot == null)
            {
                return;
            }

            for (var i = 0; i < nodesRoot.childCount; i++)
            {
                if (nodesRoot.GetChild(i) is not RectTransform rect)
                {
                    continue;
                }

                if (!Key(rect.name).Contains("technodeitem"))
                {
                    continue;
                }

                nodeTemplate = rect;
                nodeTemplate.gameObject.SetActive(false);
                break;
            }
        }

        private void HideLegacyNodes()
        {
            if (nodesRoot == null)
            {
                return;
            }

            for (var i = 0; i < nodesRoot.childCount; i++)
            {
                if (nodesRoot.GetChild(i) is not RectTransform rect)
                {
                    continue;
                }

                var key = Key(rect.name);
                if (key.Contains("btnclose"))
                {
                    continue;
                }

                if (nodeTemplate != null && ReferenceEquals(rect, nodeTemplate))
                {
                    rect.gameObject.SetActive(false);
                    continue;
                }

                if (_runtimeNodes.Contains(rect.gameObject) || _runtimeLines.Contains(rect.gameObject))
                {
                    continue;
                }

                rect.gameObject.SetActive(false);
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

            if (closeButton == null && autoCreateCloseButton && panelRoot != null)
            {
                var go = new GameObject("BtnCloseTechTree", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                var rect = go.GetComponent<RectTransform>();
                rect.SetParent(panelRoot, false);
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-20f, -20f);
                rect.sizeDelta = new Vector2(120f, 44f);

                go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
                closeButton = go.GetComponent<Button>();

                var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
                    .GetComponent<TextMeshProUGUI>();
                var labelRect = label.rectTransform;
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(6f, 4f);
                labelRect.offsetMax = new Vector2(-6f, -4f);
                label.text = string.IsNullOrWhiteSpace(closeButtonText) ? "Close" : closeButtonText;
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 20f;
                label.color = Color.white;
                label.textWrappingMode = TextWrappingModes.NoWrap;
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

        private Sprite LoadIcon(string iconKey)
        {
            var normalizedIconKey = (iconKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalizedIconKey))
            {
                return null;
            }

            var normalizedRoot = (iconResourcesRoot ?? string.Empty).Trim().Trim('/');
            if (!string.IsNullOrEmpty(normalizedRoot))
            {
                var sprite = Resources.Load<Sprite>($"{normalizedRoot}/{normalizedIconKey}");
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return Resources.Load<Sprite>(normalizedIconKey);
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

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static string Key(string value)
        {
            return Normalize(value);
        }
    }
}
