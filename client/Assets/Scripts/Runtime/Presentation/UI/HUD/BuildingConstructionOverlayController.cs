/*************************************************
 * Project: Panoptes
 * File: BuildingConstructionOverlayController.cs
 * Author: Panoptes Team
 * Date: 2026-04-16
 * Description: Screen-space text overlay for under-construction buildings.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class BuildingConstructionOverlayController : MonoBehaviour
    {
        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateOverlayCanvas = true;
        [SerializeField] private string canvasName = "BuildingConstructionOverlayCanvas";

        [Header("Tracking")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float screenYOffset = 28f;
        [SerializeField] private bool hideWhenBehindCamera = true;
        [SerializeField] private bool hideWhenOffScreen = true;

        [Header("Entry Style")]
        [SerializeField] private Vector2 labelSize = new Vector2(136f, 28f);
        [SerializeField] private int fontSize = 20;
        [SerializeField] private Color textColor = new Color(0.45f, 1f, 0.52f, 1f);
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.12f, 0.08f, 0.65f);
        [SerializeField] private float worldHeightPadding = 0.22f;

        [Header("Display Rules")]
        [SerializeField] private bool preferGhostOnly = true;
        [SerializeField] private bool showPendingBuildTextWhenNoProgress = true;
        [SerializeField] private string fallbackPendingText = "建造中";

        private sealed class Entry
        {
            public string NodeId;
            public BuildingView Building;
            public RectTransform Root;
            public Image Background;
            public TextMeshProUGUI Text;
            public float WorldHeightOffset;
        }

        private readonly Dictionary<string, Entry> _entries = new();
        private readonly HashSet<string> _aliveNodeIds = new();
        private readonly HashSet<string> _pendingBuildNodeIds = new();

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private GameStateStore _gameStateStore;
        private PlanningDraftStore _planningDraftStore;
        private MapRenderer _mapRenderer;
        private GameStateStoreState _latestGameState = new();
        private PlanningDraftState _latestPlanningDraft = new();
        private IDisposable _gameStateSubscription;
        private IDisposable _planningDraftSubscription;

        [Inject]
        private void Construct(
            GameStateStore gameStateStore,
            PlanningDraftStore planningDraftStore,
            MapRenderer mapRenderer)
        {
            _gameStateStore = gameStateStore;
            _planningDraftStore = planningDraftStore;
            _mapRenderer = mapRenderer;
            _latestGameState = _gameStateStore?.Snapshot ?? new GameStateStoreState();
            _latestPlanningDraft = _planningDraftStore?.Snapshot ?? new PlanningDraftState();
            if (isActiveAndEnabled)
            {
                SubscribeToStores();
            }
        }

        private void Awake()
        {
            ResolveCamera();
            EnsureCanvas();
        }

        private void OnEnable()
        {
            SubscribeToStores();
        }

        private void OnDisable()
        {
            UnsubscribeFromStores();
        }

        private void SubscribeToStores()
        {
            _gameStateSubscription?.Dispose();
            _planningDraftSubscription?.Dispose();
            _gameStateSubscription = _gameStateStore?.State.Subscribe(this, static (state, self) =>
            {
                self._latestGameState = state ?? new GameStateStoreState();
            });
            _planningDraftSubscription = _planningDraftStore?.State.Subscribe(this, static (state, self) =>
            {
                self._latestPlanningDraft = state ?? new PlanningDraftState();
            });
            _latestGameState = _gameStateStore?.Snapshot ?? _latestGameState ?? new GameStateStoreState();
            _latestPlanningDraft = _planningDraftStore?.Snapshot ?? _latestPlanningDraft ?? new PlanningDraftState();
        }

        private void UnsubscribeFromStores()
        {
            _gameStateSubscription?.Dispose();
            _gameStateSubscription = null;
            _planningDraftSubscription?.Dispose();
            _planningDraftSubscription = null;
        }

        private void LateUpdate()
        {
            ResolveCamera();
            if (_canvasRect == null || targetCamera == null)
            {
                return;
            }

            var map = _mapRenderer;
            if (map == null || map.TileViews == null || map.TileViews.Count == 0)
            {
                HideAll();
                return;
            }

            CapturePendingBuildNodes();
            SyncEntries(map);
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

            _canvas = GetComponentInParent<Canvas>();
            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;
            if (_canvas != null && _canvasRect != null)
            {
                return;
            }

            if (!autoCreateOverlayCanvas)
            {
                return;
            }

            var root = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasRect = root.transform as RectTransform;
            _canvas = root.GetComponent<Canvas>();
            if (_canvas != null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 380;
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

        private void CapturePendingBuildNodes()
        {
            _pendingBuildNodeIds.Clear();

            var pending = _latestPlanningDraft?.BuildOrders;
            if (pending == null)
            {
                return;
            }

            for (var i = 0; i < pending.Count; i++)
            {
                var nodeId = pending[i]?.NodeId;
                if (!string.IsNullOrWhiteSpace(nodeId))
                {
                    _pendingBuildNodeIds.Add(nodeId.Trim());
                }
            }
        }

        private void SyncEntries(MapRenderer map)
        {
            _aliveNodeIds.Clear();

            foreach (var pair in map.TileViews)
            {
                var nodeView = pair.Value;
                if (nodeView == null)
                {
                    continue;
                }

                var nodeId = nodeView.NodeId;
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    continue;
                }

                var building = nodeView.BuildingInstance;
                if (building == null)
                {
                    continue;
                }

                TryGetNodeState(nodeId, out var nodeState);
                if (!ShouldShow(nodeView, nodeState))
                {
                    continue;
                }

                _aliveNodeIds.Add(nodeId);
                if (!_entries.TryGetValue(nodeId, out var entry) || entry == null)
                {
                    entry = CreateEntry(nodeId, building);
                    _entries[nodeId] = entry;
                }
                else
                {
                    entry.Building = building;
                }

                entry.WorldHeightOffset = ComputeHeightOffset(building);
                RefreshEntryVisual(entry, nodeState);
            }

            var stale = new List<string>();
            foreach (var pair in _entries)
            {
                if (!_aliveNodeIds.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                RemoveEntry(stale[i]);
            }
        }

        private bool ShouldShow(NodeView nodeView, NodeDto nodeState)
        {
            if (nodeView == null || nodeView.BuildingInstance == null)
            {
                return false;
            }

            if (nodeView.BuildingInstance.IsGhost)
            {
                return true;
            }

            if (_pendingBuildNodeIds.Contains(nodeView.NodeId))
            {
                return true;
            }

            if (preferGhostOnly)
            {
                return false;
            }

            if (nodeState == null)
            {
                return false;
            }

            var required = Mathf.Max(0, nodeState.OperationRequiredProgress);
            var current = Mathf.Max(0, nodeState.OperationCurrentProgress);
            if (required > 0 && current < required)
            {
                return true;
            }

            return false;
        }

        private bool TryGetNodeState(string nodeId, out NodeDto nodeState)
        {
            nodeState = null;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            var nodes = _latestGameState?.Nodes;
            return nodes != null && nodes.TryGetValue(nodeId, out nodeState) && nodeState != null;
        }

        private Entry CreateEntry(string nodeId, BuildingView building)
        {
            var go = new GameObject($"ConstructionProgress_{nodeId}", typeof(RectTransform));
            var root = go.transform as RectTransform;
            root.SetParent(_canvasRect, false);
            root.sizeDelta = labelSize;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            var bg = go.AddComponent<Image>();
            bg.color = backgroundColor;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRect = textGo.transform as RectTransform;
            textRect.SetParent(root, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 2f);
            textRect.offsetMax = new Vector2(-6f, -2f);

            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.color = textColor;
            text.raycastTarget = false;

            return new Entry
            {
                NodeId = nodeId,
                Building = building,
                Root = root,
                Background = bg,
                Text = text,
                WorldHeightOffset = ComputeHeightOffset(building)
            };
        }

        private void RefreshEntryVisual(Entry entry, NodeDto nodeState)
        {
            if (entry == null || entry.Text == null)
            {
                return;
            }

            var text = BuildProgressText(entry.NodeId, nodeState);
            if (string.IsNullOrWhiteSpace(text))
            {
                entry.Root.gameObject.SetActive(false);
                return;
            }

            entry.Root.gameObject.SetActive(true);
            entry.Text.text = text;
            if (entry.Background != null)
            {
                entry.Background.color = backgroundColor;
            }
            entry.Text.color = textColor;
        }

        private string BuildProgressText(string nodeId, NodeDto nodeState)
        {
            if (nodeState != null)
            {
                var required = Mathf.Max(0, nodeState.OperationRequiredProgress);
                var current = Mathf.Max(0, nodeState.OperationCurrentProgress);
                if (required > 0)
                {
                    current = Mathf.Clamp(current, 0, required);
                    return $"建造中 {current}/{required}";
                }

                if (!string.IsNullOrWhiteSpace(nodeState.BuildingStatus))
                {
                    var status = nodeState.BuildingStatus.Trim().ToLowerInvariant();
                    if (status == "disabled" || status == "blocked")
                    {
                        return fallbackPendingText;
                    }
                }
            }

            if (showPendingBuildTextWhenNoProgress && _pendingBuildNodeIds.Contains(nodeId))
            {
                return "建造中 0/1";
            }

            return fallbackPendingText;
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

            return Mathf.Max(0.25f, bounds.max.y - building.transform.position.y + worldHeightPadding);
        }

        private void HideAll()
        {
            foreach (var pair in _entries)
            {
                if (pair.Value?.Root != null)
                {
                    pair.Value.Root.gameObject.SetActive(false);
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
