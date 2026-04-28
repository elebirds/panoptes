/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.Targeting.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Attack, charge, structure targeting, and attack range helpers for MapInputHandler.
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
        private bool TryIssueUnitTargetOrder(UnitView targetUnit)
        {
            if (_selectedUnit == null || targetUnit == null)
            {
                return false;
            }

            switch (_combatActionMode)
            {
                case CombatActionMode.Attack:
                    TryResolvePlannedMoveTargetNodeId(_selectedUnit.UnitId, out var plannedMoveTargetNodeId);
                    ClearPendingMoveStateForUnit(_selectedUnit.UnitId);
                    GameIntents.AttackUnit(_selectedUnit.UnitId, targetUnit.UnitId, plannedMoveTargetNodeId);
                    PlaySelectedAttackFeedback();
                    _combatActionMode = CombatActionMode.None;
                    NotifyCombatSelectionChanged();
                    return true;
                case CombatActionMode.Charge:
                    var map = MapRenderer.Instance;
                    if (map == null || !map.TryGetNodeIdByGrid(targetUnit.GridPos, out var targetNodeId))
                    {
                        return false;
                    }
                    GameIntents.ChargeUnit(_selectedUnit.UnitId, targetNodeId, targetUnit.UnitId);
                    _combatActionMode = CombatActionMode.None;
                    NotifyCombatSelectionChanged();
                    return true;
                default:
                    return false;
            }
        }

        private bool IsHostileTarget(UnitView unit)
        {
            if (unit == null || _selectedUnit == null || unit == _selectedUnit)
            {
                return false;
            }

            if (_combatActionMode != CombatActionMode.Attack && _combatActionMode != CombatActionMode.Charge)
            {
                return false;
            }

            return !CanControlUnit(unit);
        }

        private bool CanSelectedUnitAttack()
        {
            return TryGetSelectedUnitCatalog(out var entry) && !HasTag(entry, "civilian");
        }

        private bool CanSelectedUnitAttackStructures()
        {
            return TryGetSelectedUnitCatalog(out var entry)
                   && entry != null
                   && entry.flags != null
                   && entry.flags.can_attack_structures;
        }

        private bool CanSelectedUnitCharge()
        {
            return TryGetSelectedUnitCatalog(out var entry) && HasTag(entry, "charge");
        }

        private bool TryGetSelectedUnitCatalog(out StaticCatalogCache.UnitEntryJson entry)
        {
            entry = null;
            return _selectedUnit != null &&
                   StaticCatalogCache.EnsureInstance() != null &&
                   StaticCatalogCache.Instance.TryGetUnit(_selectedUnit.UnitType, out entry);
        }

        private bool TryIssueStructureTargetOrder(string nodeId)
        {
            if (_selectedUnit == null ||
                _combatActionMode != CombatActionMode.Attack ||
                string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            TryResolvePlannedMoveTargetNodeId(_selectedUnit.UnitId, out var plannedMoveTargetNodeId);
            ClearPendingMoveStateForUnit(_selectedUnit.UnitId);
            GameIntents.AttackNode(_selectedUnit.UnitId, nodeId, plannedMoveTargetNodeId);
            PlaySelectedAttackFeedback();
            _combatActionMode = CombatActionMode.None;
            NotifyCombatSelectionChanged();
            return true;
        }

        private void PlaySelectedAttackFeedback()
        {
            if (_selectedUnit == null)
            {
                return;
            }

            _selectedUnit.PlayAttackAnimation();
        }

        private bool IsEnemyStructureNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetNodeState(nodeId.Trim(), out var nodeState) || nodeState == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(nodeState.BuildingType))
            {
                return false;
            }

            var localOwner = NormalizeToken(GetLocalOwnerId());
            var owner = NormalizeToken(nodeState.Owner);
            if (string.IsNullOrWhiteSpace(owner))
            {
                return false;
            }

            return !string.Equals(owner, localOwner, StringComparison.Ordinal);
        }

        private void RefreshAttackRangeHighlights()
        {
            ClearNodeHighlights();

            if (_selectedUnit == null || _combatActionMode != CombatActionMode.Attack)
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null || map.TileViews == null || map.TileViews.Count == 0)
            {
                return;
            }

            if (!TryResolveSelectedAttackOrigin(out var originGrid, out _))
            {
                return;
            }

            var attackRange = ResolveSelectedUnitAttackRange();
            if (attackRange <= 0)
            {
                return;
            }

            foreach (var pair in map.TileViews)
            {
                var nodeId = pair.Key;
                var nodeView = pair.Value;
                if (nodeView == null || string.IsNullOrWhiteSpace(nodeId))
                {
                    continue;
                }

                if (HexGrid.AxialDistance(originGrid, nodeView.GridPos) > attackRange)
                {
                    continue;
                }

                nodeView.SetHighlight(true, attackRangeHighlightColor);
                _highlightNodeIds.Add(nodeId);
            }
        }

        private bool IsNodeWithinSelectedAttackRange(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetNodeView(nodeId.Trim(), out var nodeView) || nodeView == null)
            {
                return false;
            }

            return IsGridWithinSelectedAttackRange(nodeView.GridPos);
        }

        private bool IsGridWithinSelectedAttackRange(Vector2Int targetGrid)
        {
            if (!TryResolveSelectedAttackOrigin(out var originGrid, out _))
            {
                return false;
            }

            return HexGrid.AxialDistance(originGrid, targetGrid) <= ResolveSelectedUnitAttackRange();
        }

        private bool TryResolveSelectedAttackOrigin(out Vector2Int originGrid, out string originNodeId)
        {
            originGrid = _selectedUnit != null ? _selectedUnit.GridPos : default;
            originNodeId = string.Empty;

            if (_selectedUnit == null)
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return true;
            }

            if (TryResolvePlannedMoveTargetNodeId(_selectedUnit.UnitId, out var plannedTargetNodeId) &&
                map.TryGetNodeView(plannedTargetNodeId, out var plannedNode) &&
                plannedNode != null)
            {
                originGrid = plannedNode.GridPos;
                originNodeId = plannedNode.NodeId;
                return true;
            }

            if (map.TryGetNodeIdByGrid(_selectedUnit.GridPos, out var currentNodeId))
            {
                originNodeId = currentNodeId;
            }

            return true;
        }

        private int ResolveSelectedUnitAttackRange()
        {
            if (TryGetSelectedUnitCatalog(out var entry) && entry != null)
            {
                return Mathf.Max(1, entry.attack_range);
            }

            return 1;
        }
    }
}
