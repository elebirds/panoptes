/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.Selection.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Unit and inspectable node selection, info proxy creation, and UnitInfo panel routing.
 *************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Presentation.Animation;
using Panoptes.Presentation.UI.Common;
using Panoptes.Presentation.UI.HUD;
using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Presentation.Map
{
    public sealed partial class MapInputHandler
    {
        private bool TryOpenBuildingInfoFromClick()
        {
            if (!TryGetClickedNodeContext(out var node, out var nodeState))
            {
                return false;
            }

            if (!TryGetInspectableNodeInfo(nodeState, out var buildingType, out var isResourcePoint))
            {
                return false;
            }

            if (string.Equals(buildingType, "city_core", StringComparison.Ordinal))
            {
                HighlightTerritoryForNode(nodeState);
            }
            else
            {
                ClearTerritoryHighlights();
            }

            var proxy = GetOrCreateNodeInfoProxy(node, nodeState, buildingType, isResourcePoint);
            if (proxy == null)
            {
                return false;
            }

            ClearMoveSelection(false);
            NotifyUnitSelectionChanged(proxy);
            NotifyUnitInfoPanel(proxy);
            return true;
        }

        private bool TrySelectOwnedUnitFromNodeClick()
        {
            if (!TryGetClickedNodeContext(out var nodeView, out var nodeState))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map == null || nodeView == null || string.IsNullOrWhiteSpace(nodeView.NodeId))
            {
                return false;
            }

            if (!map.TryGetUnitsOnNode(nodeView.NodeId, _nodeClickUnits))
            {
                return false;
            }

            UnitView selectedOwnedUnit = null;
            for (var i = 0; i < _nodeClickUnits.Count; i++)
            {
                var candidate = _nodeClickUnits[i];
                if (candidate == null || !CanControlUnit(candidate))
                {
                    continue;
                }

                if (selectedOwnedUnit == null)
                {
                    selectedOwnedUnit = candidate;
                }

                if (IsTerritoryExpansionUnitType(candidate.UnitType))
                {
                    selectedOwnedUnit = candidate;
                    break;
                }
            }

            if (selectedOwnedUnit == null)
            {
                return false;
            }

            if (ReferenceEquals(_selectedUnit, selectedOwnedUnit)
                && TryGetInspectableNodeInfo(nodeState, out _, out _))
            {
                return false;
            }

            SelectUnit(selectedOwnedUnit);
            return true;
        }

        private bool TryGetClickedNodeContext(out NodeView nodeView, out NodeDto nodeState)
        {
            nodeView = null;
            nodeState = null;

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return false;
            }

            if (TryRaycastNode(out nodeView)
                && nodeView != null
                && !string.IsNullOrWhiteSpace(nodeView.NodeId)
                && map.TryGetNodeState(nodeView.NodeId, out nodeState)
                && nodeState != null)
            {
                return true;
            }

            if (!TryRaycastUnit(out var unit) || unit == null)
            {
                return false;
            }

            if (!map.TryGetNodeIdByGrid(unit.GridPos, out var nodeId) || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            if (!map.TryGetNodeView(nodeId, out nodeView) || nodeView == null)
            {
                return false;
            }

            return map.TryGetNodeState(nodeId, out nodeState) && nodeState != null;
        }

        private static bool TryGetInspectableNodeInfo(NodeDto nodeState, out string buildingType, out bool isResourcePoint)
        {
            buildingType = string.Empty;
            isResourcePoint = false;
            if (nodeState == null)
            {
                return false;
            }

            buildingType = NormalizeToken(nodeState.BuildingType);
            isResourcePoint = nodeState.IsResourcePoint;
            return !string.IsNullOrEmpty(buildingType) || isResourcePoint;
        }

        private UnitView GetOrCreateNodeInfoProxy(NodeView nodeView, NodeDto nodeState, string normalizedBuildingType, bool isResourcePoint)
        {
            if (nodeView == null || nodeState == null)
            {
                return null;
            }

            if (_buildingInfoProxy == null)
            {
                var proxyGo = new GameObject("BuildingInfoProxy");
                _buildingInfoProxy = proxyGo.AddComponent<UnitView>();
                proxyGo.hideFlags = HideFlags.DontSave;
            }

            var infoType = normalizedBuildingType;
            if (string.IsNullOrEmpty(infoType) && isResourcePoint)
            {
                var resourceType = NormalizeToken(nodeState.ResourceType);
                infoType = string.IsNullOrEmpty(resourceType) ? "resource_point" : $"resource_{resourceType}";
            }

            if (string.IsNullOrEmpty(infoType))
            {
                return null;
            }

            var hp = nodeState.BuildingHp > 0 ? nodeState.BuildingHp : (isResourcePoint ? 1 : 100);
            var maxHp = ResolveNodeInfoMaxHp(nodeView, nodeState, infoType, hp, isResourcePoint);
            var unit = new UnitDto
            {
                Id = nodeState.Id ?? string.Empty,
                Type = infoType,
                Owner = !string.IsNullOrWhiteSpace(nodeState.Owner) ? nodeState.Owner : nodeState.TerritoryOwner,
                Q = nodeState.Q,
                R = nodeState.R,
                Hp = hp,
                MaxHp = maxHp
            };

            var worldPos = nodeView.BuildingAnchor != null
                ? nodeView.BuildingAnchor.position
                : nodeView.transform.position;

            _buildingInfoProxy.gameObject.SetActive(true);
            _buildingInfoProxy.Bind(unit, worldPos);
            _buildingInfoProxy.SetSelected(false);
            var collider = _buildingInfoProxy.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
            _buildingInfoProxy.gameObject.SetActive(false);
            return _buildingInfoProxy;
        }

        private static int ResolveNodeInfoMaxHp(NodeView nodeView, NodeDto nodeState, string infoType, int hp, bool isResourcePoint)
        {
            if (isResourcePoint)
            {
                return Mathf.Max(1, hp);
            }

            var maxHp = nodeState != null ? nodeState.BuildingMaxHp : 0;
            if (maxHp <= 0 && nodeView != null && nodeView.BuildingInstance != null)
            {
                maxHp = nodeView.BuildingInstance.MaxHitPoints;
            }

            if (maxHp <= 0)
            {
                var catalog = StaticCatalogCache.EnsureInstance();
                var normalizedType = NormalizeToken(infoType);
                if (catalog != null)
                {
                    if (string.Equals(normalizedType, "city_core", StringComparison.OrdinalIgnoreCase) &&
                        catalog.Rules != null &&
                        catalog.Rules.city_core_max_hp > 0)
                    {
                        maxHp = catalog.Rules.city_core_max_hp;
                    }
                    else if (catalog.TryGetBuilding(normalizedType, out var buildingEntry) && buildingEntry != null)
                    {
                        maxHp = buildingEntry.max_hp;
                    }
                }
            }

            return Mathf.Max(1, Mathf.Max(maxHp, hp));
        }

        private void CloseCurrentInfoSelection()
        {
            if (_selectedUnit != null)
            {
                ClearMoveSelection();
                return;
            }

            NotifyUnitSelectionChanged(null);
            NotifyUnitInfoPanel(null);
        }

        private void SelectUnit(UnitView unit)
        {
            if (unit == null)
            {
                return;
            }

            var canControl = !onlyControlOwnUnits || CanControlUnit(unit);

            ClearTerritoryHighlights();
            ClearMoveSelection(false);
            _selectedUnit = unit;
            _selectedUnit.SetSelected(true);
            _combatActionMode = CombatActionMode.None;
            ClearMovePreviewState();
            NotifyCombatSelectionChanged();
            NotifyUnitSelectionChanged(_selectedUnit);
            NotifyUnitInfoPanel(_selectedUnit);

            if (!canControl || !IsCombatPhase())
            {
                ClearNodeHighlights();
                return;
            }
        }

        private void ClearMoveSelection(bool notify = true)
        {
            var changed = _selectedUnit != null;
            if (_selectedUnit != null)
            {
                _selectedUnit.SetSelected(false);
            }

            _selectedUnit = null;
            ClearNodeHighlights();

            if (notify && changed)
            {
                NotifyUnitSelectionChanged(null);
            }

            if (changed)
            {
                NotifyUnitInfoPanel(null);
            }
        }

        private void NotifyUnitSelectionChanged(UnitView unit)
        {
            try
            {
                UnitSelectionChanged?.Invoke(unit);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapInputHandler] UnitSelectionChanged callback failed: {ex.Message}");
            }
        }

        private void NotifyUnitInfoPanel(UnitView unit)
        {
            if (_unitInfoPanelController == null)
            {
                _unitInfoPanelController = UnityEngine.Object.FindAnyObjectByType<UnitInfoPanelController>();
            }

            if (_unitInfoPanelController == null)
            {
                return;
            }

            if (unit == null)
            {
                _unitInfoPanelController.Close();
                return;
            }

            _unitInfoPanelController.OpenForUnit(unit);
        }
    }
}
