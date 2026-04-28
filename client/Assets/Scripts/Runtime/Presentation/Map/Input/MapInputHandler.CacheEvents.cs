/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.CacheEvents.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: GameStateCache and PlanningDraftCache subscriptions and event-driven presentation updates.
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
        private void SubscribeCacheEvents()
        {
            if (_cacheEventsSubscribed)
            {
                return;
            }

            _cache = GameStateCache.Instance;
            _draftCache = PlanningDraftCache.EnsureInstance();
            if (_cache == null)
            {
                return;
            }

            _cache.OnTurnSettled += OnTurnSettled;
            _cache.OnNodeChanged += OnNodeChanged;
            _cache.OnUnitsChanged += OnUnitsChanged;
            _cache.OnTokenResult += OnTokenResult;
            _cache.OnPlanningCommandResult += OnPlanningCommandResult;
            _cacheEventsSubscribed = true;
        }

        private void SubscribeDraftCacheEvents()
        {
            _draftCache = PlanningDraftCache.EnsureInstance();
            _movePathOverlay ??= new MovePathOverlayController(transform);
            _movePreviewOverlay ??= new MovePreviewOverlayController(transform);
            _movePreviewOverlay.SetHostTransform(transform);
            if (_draftCache == null)
            {
                return;
            }

            _draftCache.PreviewChanged -= OnPreviewChanged;
            _draftCache.PreviewChanged += OnPreviewChanged;
            _draftCache.BuildPreviewChanged -= OnBuildPreviewChanged;
            _draftCache.BuildPreviewChanged += OnBuildPreviewChanged;
            _draftCache.OrdersChanged -= OnOrdersChanged;
            _draftCache.OrdersChanged += OnOrdersChanged;
            RefreshQueuedMovePathMarkers();
        }

        private void UnsubscribeCacheEvents()
        {
            if (!_cacheEventsSubscribed)
            {
                return;
            }

            if (_cache != null)
            {
                _cache.OnTurnSettled -= OnTurnSettled;
                _cache.OnNodeChanged -= OnNodeChanged;
                _cache.OnUnitsChanged -= OnUnitsChanged;
                _cache.OnTokenResult -= OnTokenResult;
                _cache.OnPlanningCommandResult -= OnPlanningCommandResult;
            }

            _cache = null;
            _cacheEventsSubscribed = false;
        }

        private void UnsubscribeDraftCacheEvents()
        {
            if (_draftCache == null)
            {
                return;
            }

            _draftCache.PreviewChanged -= OnPreviewChanged;
            _draftCache.BuildPreviewChanged -= OnBuildPreviewChanged;
            _draftCache.OrdersChanged -= OnOrdersChanged;
            _draftCache = null;
        }

        private void OnTurnSettled(TurnSettledEvent settledEvent)
        {
            ClearCombatSelection();
            _pendingMoveUnitIds.Clear();
            _pendingMoveTargetNodeByUnitId.Clear();
            _movePathOverlay?.ClearAllMovePathMarkers();

            var settlement = settledEvent?.Settlement;
            if (settlement?.Sections != null)
            {
                for (var sectionIndex = 0; sectionIndex < settlement.Sections.Count; sectionIndex++)
                {
                    var section = settlement.Sections[sectionIndex];
                    if (section?.Events == null)
                    {
                        continue;
                    }

                    for (var eventIndex = 0; eventIndex < section.Events.Count; eventIndex++)
                    {
                        var eventItem = section.Events[eventIndex];
                        if (eventItem == null || eventItem.Type != "unit_moved")
                        {
                            continue;
                        }

                        var settledPathNodeIds = GetRememberedMovePathNodeIds(eventItem.UnitId);

                        if (!string.IsNullOrWhiteSpace(eventItem.UnitId))
                        {
                            var normalizedUnitId = eventItem.UnitId.Trim();
                            _pendingMoveUnitIds.Remove(normalizedUnitId);
                            _pendingMoveTargetNodeByUnitId.Remove(normalizedUnitId);
                            _pendingMovePathNodeIdsByUnitId.Remove(normalizedUnitId);
                            _movePathOverlay?.ClearMovePathMarkersForUnit(normalizedUnitId);
                        }

                        if (MapRenderer.Instance == null)
                        {
                            continue;
                        }

                        var grid = new Vector2Int(eventItem.ToQ, eventItem.ToR);
                        if (!MapRenderer.Instance.TryGetNodeIdByGrid(grid, out var targetNodeId))
                        {
                            continue;
                        }

                        ApplyBackendMoveCommand(eventItem.UnitId, targetNodeId, true, true, settledPathNodeIds);
                    }
                }
            }
            _pendingMovePathNodeIdsByUnitId.Clear();

            var builtBuildings = settlement?.BuiltBuildings;
            if (builtBuildings == null || builtBuildings.Count == 0)
            {
                return;
            }

            for (int i = 0; i < builtBuildings.Count; i++)
            {
                var built = builtBuildings[i];
                if (built == null || string.IsNullOrWhiteSpace(built.NodeId) || string.IsNullOrWhiteSpace(built.BuildingType))
                {
                    continue;
                }

                var hp = built.BuildingHp > 0 ? built.BuildingHp : 100;
                ApplyBackendBuildCommand(
                    built.BuildingType,
                    built.NodeId,
                    false,
                    built.OwnerId,
                    hp);
            }
        }

        private void OnTokenResult(TokenResultEvent e)
        {
            if (e == null)
            {
                return;
            }

            var action = NormalizeToken(e.Action);
            if (string.Equals(action, "expand_territory", StringComparison.Ordinal))
            {
                if (!e.Success)
                {
                    ClearAllPendingDeployGhosts();
                }
                return;
            }

            if (string.Equals(action, "build", StringComparison.Ordinal) && !e.Success)
            {
                RollbackPendingBuild(GetLastPendingBuildNodeId());
            }
        }

        private void OnPlanningCommandResult(PlanningCommandResultEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            if (string.Equals(NormalizeToken(evt.CommandType), "build", StringComparison.Ordinal))
            {
                if (!evt.Success)
                {
                    RollbackPendingBuild(evt.PrimaryId);
                }
                return;
            }

            if (!string.Equals(NormalizeToken(evt.CommandType), "unit_order", StringComparison.Ordinal) || evt.Success)
            {
                return;
            }

            var action = NormalizeToken(evt.Action);
            if (string.Equals(action, "move", StringComparison.Ordinal))
            {
                var unitId = evt.PrimaryId?.Trim();
                if (!string.IsNullOrWhiteSpace(unitId))
                {
                    _pendingMoveUnitIds.Remove(unitId);
                    _pendingMoveTargetNodeByUnitId.Remove(unitId);
                    _movePathOverlay?.ClearMovePathMarkersForUnit(unitId);
                    RemoveMovePreview(unitId);
                }

                return;
            }

            if (string.Equals(action, "settle_city", StringComparison.Ordinal))
            {
                ClearPendingDeployCityCoreGhostForUnit(evt.PrimaryId);
            }
        }

        private void OnNodeChanged(NodeChangedEvent evt)
        {
            if (evt == null || evt.Node == null || string.IsNullOrWhiteSpace(evt.NodeID))
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            map.ApplyNodeSnapshot(evt.Node);
            TryResolvePendingDeployGhostByNode(evt.NodeID, evt.Node);
            if (map.TryGetNodeView(evt.NodeID, out var nodeView) && nodeView != null)
            {
                if (_territoryHighlightNodeIds.Contains(evt.NodeID))
                {
                    nodeView.SetHighlight(true, territoryHighlightColor);
                }
                else if (_highlightNodeIds.Contains(evt.NodeID))
                {
                    nodeView.SetHighlight(true, attackRangeHighlightColor);
                }
                else if (_movePreviewOverlay != null && _movePreviewOverlay.TryRestorePreviewHighlight(evt.NodeID, nodeView))
                {
                }
            }
        }

        private void OnUnitsChanged(UnitsChangedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            if (evt.RemovedIDs != null)
            {
                for (var i = 0; i < evt.RemovedIDs.Count; i++)
                {
                    var removedId = evt.RemovedIDs[i];
                    if (string.IsNullOrWhiteSpace(removedId))
                    {
                        continue;
                    }

                    map.RemoveRuntimeUnit(removedId, false);
                    RemoveMovePreview(removedId);
                    var normalizedRemovedId = removedId.Trim();
                    _pendingMoveUnitIds.Remove(normalizedRemovedId);
                    _pendingMoveTargetNodeByUnitId.Remove(normalizedRemovedId);
                    _movePathOverlay?.ClearMovePathMarkersForUnit(normalizedRemovedId);
                    ClearPendingDeployCityCoreGhostForUnit(normalizedRemovedId);
                    _knownUnitHpByUnitId.Remove(normalizedRemovedId);
                    _lastDamagePopupTimeByUnitId.Remove(normalizedRemovedId);
                    if (_selectedUnit != null && string.Equals(_selectedUnit.UnitId, removedId, StringComparison.Ordinal))
                    {
                        ClearMoveSelection();
                    }
                }
            }

            if (evt.Added != null)
            {
                for (var i = 0; i < evt.Added.Count; i++)
                {
                    var added = evt.Added[i];
                    if (added == null)
                    {
                        continue;
                    }

                    map.TrySpawnRuntimeUnit(added, true, false);
                    var addedId = string.IsNullOrWhiteSpace(added.Id) ? string.Empty : added.Id.Trim();
                    if (!string.IsNullOrEmpty(addedId))
                    {
                        _knownUnitHpByUnitId[addedId] = Mathf.Max(0, added.Hp);
                    }
                }
            }

            if (evt.Moved != null)
            {
                var allowFallbackPopup = enableUnitDamagePopupFallback &&
                                         !string.Equals(evt.ChangeType, "settlement", StringComparison.OrdinalIgnoreCase);
                for (var i = 0; i < evt.Moved.Count; i++)
                {
                    var moved = evt.Moved[i];
                    if (moved == null || string.IsNullOrWhiteSpace(moved.Id))
                    {
                        continue;
                    }

                    var unitId = moved.Id.Trim();
                    var hpAfter = Mathf.Max(0, moved.Hp);

                    if (!_knownUnitHpByUnitId.TryGetValue(unitId, out var hpBefore))
                    {
                        if (map.TryGetUnitView(unitId, out var unitView) && unitView != null)
                        {
                            hpBefore = Mathf.Max(0, unitView.HitPoints);
                        }
                        else
                        {
                            hpBefore = hpAfter;
                        }
                    }

                    _knownUnitHpByUnitId[unitId] = hpAfter;

                    var damage = hpBefore - hpAfter;
                    if (allowFallbackPopup && damage > 0)
                    {
                        TryShowUnitDamagePopup(unitId, damage);
                    }
                }
            }

            if (_selectedUnit != null && _combatActionMode == CombatActionMode.Attack)
            {
                RefreshAttackRangeHighlights();
            }
        }

        private void TryShowUnitDamagePopup(string unitId, int damage)
        {
            if (string.IsNullOrWhiteSpace(unitId) || damage <= 0)
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
            if (_lastDamagePopupTimeByUnitId.TryGetValue(normalizedUnitId, out var lastPopupAt))
            {
                var cooldown = Mathf.Max(0f, damagePopupRepeatCooldownSeconds);
                if (Time.unscaledTime - lastPopupAt < cooldown)
                {
                    return;
                }
            }

            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetUnitView(normalizedUnitId, out var unitView) || unitView == null)
            {
                return;
            }

            if (damagePopupController == null)
            {
                damagePopupController = FindAnyObjectByType<DamageNumberPopupController>();
            }

            if (damagePopupController == null)
            {
                var popupRoot = new GameObject("DamageNumberPopupController_Fallback");
                damagePopupController = popupRoot.AddComponent<DamageNumberPopupController>();
            }

            damagePopupController.ShowDamage(unitView.transform, damage, isBuilding: false);
            _lastDamagePopupTimeByUnitId[normalizedUnitId] = Time.unscaledTime;
        }

        private void OnPreviewChanged()
        {
            RefreshPreviewVisuals();
        }

        private void OnBuildPreviewChanged()
        {
            RefreshBuildPreviewVisuals();
        }

        private void OnOrdersChanged()
        {
            RememberQueuedMovePaths();
            RefreshQueuedMovePathMarkers();
            if (_selectedUnit != null && _combatActionMode == CombatActionMode.Attack)
            {
                RefreshAttackRangeHighlights();
            }
        }
    }
}
