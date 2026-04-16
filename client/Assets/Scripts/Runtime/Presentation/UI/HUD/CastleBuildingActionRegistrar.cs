using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.Domestic;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    /// <summary>
    /// Registers castle-specific actions on UnitInfoPanel:
    /// 1) open production/refine panel
    /// 2) open derived build panel
    /// </summary>
    public sealed class CastleBuildingActionRegistrar : UnitInfoActionProviderBase
    {
        [Header("Action IDs")]
        [SerializeField] private string productionActionId = "action_2";
        [SerializeField] private string buildActionId = "action_3";
        [SerializeField] private string techTreeActionId = "action_4";
        [SerializeField] private string fallbackProductionActionId = "castle_open_production";
        [SerializeField] private string fallbackBuildActionId = "castle_open_build";
        [SerializeField] private string fallbackTechTreeActionId = "castle_open_techtree";

        [Header("Labels")]
        [SerializeField] private string productionActionLabel = "Production";
        [SerializeField] private string buildActionLabel = "Build";
        [SerializeField] private string techTreeActionLabel = "Tech Tree";

        [Header("References")]
        [SerializeField] private MapInputHandler mapInputHandler;
        [SerializeField] private UnitInfoPanelController unitInfoPanelController;
        [SerializeField] private CastleProductionPanel castleProductionPanel;
        [SerializeField] private TechTreePanelController techTreePanelController;
        [SerializeField] private BuildPanelSlideToggle buildPanelSlideToggle;
        [SerializeField] private BuildCommandPanel buildCommandPanel;
        [SerializeField] private bool autoSpawnCastleProductionPanelIfMissing = true;
        [SerializeField] private string castleProductionPanelResourcesPath = "Prefabs/UI/CastleProductionPanel";
        [SerializeField] private bool hideTechTreePanelOnStart = true;

        [Header("Build Derived Panel")]
        [SerializeField] private bool startBuildPanelCollapsed = true;
        [SerializeField] private bool hideBuildPanelToggleButton = true;
        [SerializeField] private bool hideBuildPanelCancelButton = true;
        [SerializeField] private float unitInfoShiftXWhenBuildPanelOpen = 360f;
        [SerializeField] private bool useBuildPanelWidthForShift = true;
        [SerializeField] private float buildPanelWidthShiftFactor = 1.18f;
        [SerializeField] private float buildPanelWidthShiftExtra = 0f;

        private bool _buildPanelOpen;

        protected override void RegisterActions(UnitInfoActionRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            ResolveReferences();
            EnsureInitialPanelState();

            registry.RegisterAction(
                productionActionId,
                OnProductionActionClicked,
                string.IsNullOrWhiteSpace(productionActionLabel) ? "Production" : productionActionLabel,
                IsOwnedCastleBuildingProxy);

            registry.RegisterAction(
                buildActionId,
                OnBuildActionClicked,
                string.IsNullOrWhiteSpace(buildActionLabel) ? "Build" : buildActionLabel,
                IsOwnedCastleBuildingProxy);

            registry.RegisterAction(
                techTreeActionId,
                OnTechTreeActionClicked,
                string.IsNullOrWhiteSpace(techTreeActionLabel) ? "Tech Tree" : techTreeActionLabel,
                IsOwnedCastleBuildingProxy);

            // Compatibility: if UnitInfoPanel uses different action IDs.
            if (!string.Equals(fallbackProductionActionId, productionActionId, StringComparison.OrdinalIgnoreCase))
            {
                registry.RegisterAction(
                    fallbackProductionActionId,
                    OnProductionActionClicked,
                    string.IsNullOrWhiteSpace(productionActionLabel) ? "Production" : productionActionLabel,
                    IsOwnedCastleBuildingProxy);
            }

            if (!string.Equals(fallbackBuildActionId, buildActionId, StringComparison.OrdinalIgnoreCase))
            {
                registry.RegisterAction(
                    fallbackBuildActionId,
                    OnBuildActionClicked,
                    string.IsNullOrWhiteSpace(buildActionLabel) ? "Build" : buildActionLabel,
                    IsOwnedCastleBuildingProxy);
            }

            if (!string.Equals(fallbackTechTreeActionId, techTreeActionId, StringComparison.OrdinalIgnoreCase))
            {
                registry.RegisterAction(
                    fallbackTechTreeActionId,
                    OnTechTreeActionClicked,
                    string.IsNullOrWhiteSpace(techTreeActionLabel) ? "Tech Tree" : techTreeActionLabel,
                    IsOwnedCastleBuildingProxy);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveReferences();
            EnsureInitialPanelState();
            SubscribeInputEvents();
        }

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            EnsureInitialPanelState();
        }

        private void OnDisable()
        {
            if (mapInputHandler != null)
            {
                mapInputHandler.NonBuildingMapClicked -= OnNonBuildingMapClicked;
                mapInputHandler.UnitSelectionChanged -= OnUnitSelectionChanged;
            }
        }

        private void SubscribeInputEvents()
        {
            if (mapInputHandler == null)
            {
                return;
            }

            mapInputHandler.NonBuildingMapClicked -= OnNonBuildingMapClicked;
            mapInputHandler.NonBuildingMapClicked += OnNonBuildingMapClicked;

            mapInputHandler.UnitSelectionChanged -= OnUnitSelectionChanged;
            mapInputHandler.UnitSelectionChanged += OnUnitSelectionChanged;
        }

        private void ResolveReferences()
        {
            if (mapInputHandler == null)
            {
                mapInputHandler = MapInputHandler.Instance;
                if (mapInputHandler == null)
                {
                    mapInputHandler = UnityEngine.Object.FindAnyObjectByType<MapInputHandler>();
                }
            }

            if (unitInfoPanelController == null)
            {
                unitInfoPanelController = UnityEngine.Object.FindAnyObjectByType<UnitInfoPanelController>();
            }

            if (castleProductionPanel == null)
            {
                var productionPanels = UnityEngine.Object.FindObjectsByType<CastleProductionPanel>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (productionPanels != null && productionPanels.Length > 0)
                {
                    castleProductionPanel = productionPanels[0];
                }
            }

            if (castleProductionPanel == null && autoSpawnCastleProductionPanelIfMissing)
            {
                var prefab = !string.IsNullOrWhiteSpace(castleProductionPanelResourcesPath)
                    ? Resources.Load<CastleProductionPanel>(castleProductionPanelResourcesPath.Trim())
                    : null;

                var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                var parent = canvas != null ? canvas.transform : null;

                if (prefab != null)
                {
                    castleProductionPanel = UnityEngine.Object.Instantiate(prefab, parent, false);
                }
                else
                {
                    var go = new GameObject("CastleProductionPanel", typeof(RectTransform));
                    if (parent != null)
                    {
                        go.transform.SetParent(parent, false);
                    }
                    castleProductionPanel = go.AddComponent<CastleProductionPanel>();
                }
            }

            if (techTreePanelController == null)
            {
                var techPanels = UnityEngine.Object.FindObjectsByType<TechTreePanelController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (techPanels != null && techPanels.Length > 0)
                {
                    techTreePanelController = techPanels[0];
                }
            }

            if (techTreePanelController == null)
            {
                var allRects = UnityEngine.Object.FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (var i = 0; i < allRects.Length; i++)
                {
                    var rect = allRects[i];
                    if (rect == null || !rect.gameObject.scene.IsValid())
                    {
                        continue;
                    }

                    var name = rect.name ?? string.Empty;
                    if (!string.Equals(name, "TechTreePanel", StringComparison.OrdinalIgnoreCase) &&
                        name.IndexOf("techtree", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    techTreePanelController = rect.GetComponent<TechTreePanelController>();
                    if (techTreePanelController == null)
                    {
                        techTreePanelController = rect.gameObject.AddComponent<TechTreePanelController>();
                    }
                    break;
                }
            }

            if (buildCommandPanel == null)
            {
                var buildPanels = UnityEngine.Object.FindObjectsByType<BuildCommandPanel>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (buildPanels != null && buildPanels.Length > 0)
                {
                    buildCommandPanel = buildPanels[0];
                }
            }

            if (buildPanelSlideToggle == null && buildCommandPanel != null)
            {
                buildPanelSlideToggle = buildCommandPanel.GetComponentInParent<BuildPanelSlideToggle>(true);
            }

            if (buildCommandPanel == null && buildPanelSlideToggle != null)
            {
                buildCommandPanel = buildPanelSlideToggle.GetComponentInParent<BuildCommandPanel>(true);
            }

            if (buildPanelSlideToggle == null)
            {
                var toggles = UnityEngine.Object.FindObjectsByType<BuildPanelSlideToggle>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (toggles != null && toggles.Length > 0)
                {
                    buildPanelSlideToggle = toggles[0];
                }
            }
        }

        private void EnsureInitialPanelState()
        {
            if (buildPanelSlideToggle != null)
            {
                if (startBuildPanelCollapsed)
                {
                    buildPanelSlideToggle.SetCollapsed(true, true);
                    _buildPanelOpen = false;
                }

                if (hideBuildPanelToggleButton)
                {
                    buildPanelSlideToggle.SetToggleButtonVisible(false);
                }
            }

            if (hideBuildPanelCancelButton && buildCommandPanel != null)
            {
                buildCommandPanel.SetCancelButtonVisible(false);
            }

            if (unitInfoPanelController != null && !_buildPanelOpen)
            {
                unitInfoPanelController.SetExternalOffset(Vector2.zero, true);
            }

            if (hideTechTreePanelOnStart && techTreePanelController != null)
            {
                techTreePanelController.gameObject.SetActive(false);
            }
        }

        private void OnProductionActionClicked(UnitView unit)
        {
            if (!TryResolveCastleNodeId(unit, out var nodeId))
            {
                return;
            }

            ResolveReferences();
            CloseBuildPanel(true);
            if (techTreePanelController != null)
            {
                techTreePanelController.gameObject.SetActive(false);
            }

            if (castleProductionPanel == null)
            {
                Debug.LogWarning($"[CastleBuildingActionRegistrar] CastleProductionPanel missing. node={nodeId}");
                return;
            }

            castleProductionPanel.OpenForCastle(nodeId);
        }

        private void OnBuildActionClicked(UnitView unit)
        {
            if (!TryResolveCastleNodeId(unit, out var nodeId))
            {
                return;
            }

            ResolveReferences();
            if (buildPanelSlideToggle == null)
            {
                Debug.LogWarning("[CastleBuildingActionRegistrar] BuildPanelSlideToggle missing.");
                return;
            }

            if (buildCommandPanel != null)
            {
                buildCommandPanel.SetCastleContext(nodeId);
            }

            if (_buildPanelOpen && !buildPanelSlideToggle.IsCollapsed)
            {
                CloseBuildPanel(true);
                return;
            }

            if (techTreePanelController != null)
            {
                techTreePanelController.gameObject.SetActive(false);
            }

            buildPanelSlideToggle.Expand();
            _buildPanelOpen = true;
            if (unitInfoPanelController != null)
            {
                var shiftX = ResolveUnitInfoShiftX();
                unitInfoPanelController.SetExternalOffset(new Vector2(-shiftX, 0f), false);
            }
        }

        private void OnTechTreeActionClicked(UnitView unit)
        {
            if (!TryResolveCastleNodeId(unit, out _))
            {
                return;
            }

            ResolveReferences();
            CloseBuildPanel(true);

            if (castleProductionPanel != null)
            {
                castleProductionPanel.Close();
            }

            if (techTreePanelController == null)
            {
                Debug.LogWarning("[CastleBuildingActionRegistrar] TechTreePanelController missing.");
                return;
            }

            var panelGo = techTreePanelController.gameObject;
            if (panelGo.activeSelf)
            {
                panelGo.SetActive(false);
                return;
            }

            panelGo.SetActive(true);
            var panelRect = panelGo.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.SetAsLastSibling();
            }
        }

        private void OnNonBuildingMapClicked()
        {
            CloseBuildPanel(true);
        }

        private void OnUnitSelectionChanged(UnitView selected)
        {
            if (IsOwnedCastleBuildingProxy(selected))
            {
                return;
            }

            CloseBuildPanel(true);
            if (techTreePanelController != null)
            {
                techTreePanelController.gameObject.SetActive(false);
            }
        }

        private void CloseBuildPanel(bool resetUnitInfoOffset)
        {
            if (buildPanelSlideToggle != null)
            {
                buildPanelSlideToggle.Collapse();
            }
            _buildPanelOpen = false;

            if (buildCommandPanel != null)
            {
                buildCommandPanel.ClearCastleContext();
            }

            if (resetUnitInfoOffset && unitInfoPanelController != null)
            {
                unitInfoPanelController.SetExternalOffset(Vector2.zero, false);
            }
        }

        private bool TryResolveCastleNodeId(UnitView unit, out string nodeId)
        {
            nodeId = string.Empty;
            if (!IsOwnedCastleBuildingProxy(unit))
            {
                return false;
            }

            nodeId = unit.UnitId;
            return !string.IsNullOrWhiteSpace(nodeId);
        }

        private bool IsOwnedCastleBuildingProxy(UnitView unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (!string.Equals(NormalizeToken(unit.UnitType), "castle", StringComparison.Ordinal))
            {
                return false;
            }

            var cache = GameStateCache.Instance;
            if (cache == null || string.IsNullOrWhiteSpace(unit.UnitId))
            {
                return false;
            }

            var node = cache.GetNode(unit.UnitId);
            if (node == null)
            {
                return false;
            }

            var localOwner = NormalizeToken(cache.MyPlayerID);
            if (string.IsNullOrWhiteSpace(localOwner))
            {
                return false;
            }

            var owner = NormalizeToken(node.Owner);
            var territoryOwner = NormalizeToken(node.TerritoryOwner);
            return string.Equals(owner, localOwner, StringComparison.Ordinal)
                   || string.Equals(territoryOwner, localOwner, StringComparison.Ordinal);
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private float ResolveUnitInfoShiftX()
        {
            var fallback = Mathf.Abs(unitInfoShiftXWhenBuildPanelOpen);
            if (!useBuildPanelWidthForShift)
            {
                return fallback;
            }

            var panelWidth = 0f;
            if (buildPanelSlideToggle != null)
            {
                panelWidth = Mathf.Max(panelWidth, buildPanelSlideToggle.GetPanelWidth());
            }

            if (buildCommandPanel != null)
            {
                var rect = buildCommandPanel.transform as RectTransform;
                if (rect != null)
                {
                    panelWidth = Mathf.Max(panelWidth, Mathf.Abs(rect.rect.width));
                }
            }

            if (panelWidth <= 1f)
            {
                return fallback;
            }

            var shiftFactor = Mathf.Max(1.15f, buildPanelWidthShiftFactor);
            var resolved = panelWidth * shiftFactor + buildPanelWidthShiftExtra;
            return Mathf.Max(1f, resolved);
        }
    }
}
