/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.Combat.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Move, attack, and charge selection flow backed by server preview and intent dispatch.
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
        private void HandleMoveSelectionClick()
        {
            if (_selectedUnit == null)
            {
                _combatActionMode = CombatActionMode.None;
                NotifyCombatSelectionChanged();
                return;
            }

            if (!TryGetClickedNodeContext(out var node, out _) ||
                node == null ||
                string.IsNullOrWhiteSpace(node.NodeId))
            {
                ClearMovePreviewState();
                return;
            }

            if (TryIssueAuthoritativeMoveOrder(node.NodeId))
            {
                _combatActionMode = CombatActionMode.None;
                NotifyCombatSelectionChanged();
            }
        }

        private bool ShouldPrioritizeStructureAttackClick()
        {
            if (_selectedUnit == null ||
                _combatActionMode != CombatActionMode.Attack ||
                !CanSelectedUnitAttackStructures() ||
                !TryRaycastNode(out var node) ||
                node == null ||
                string.IsNullOrWhiteSpace(node.NodeId))
            {
                return false;
            }

            return true;
        }

        private void HandleCombatSelectionClick()
        {
            if (_combatActionMode == CombatActionMode.Attack &&
                TryGetClickedNodeContext(out var attackNodeView, out _) &&
                attackNodeView != null &&
                !string.IsNullOrWhiteSpace(attackNodeView.NodeId) &&
                IsEnemyStructureNode(attackNodeView.NodeId))
            {
                if (TryIssueStructureTargetOrder(attackNodeView.NodeId))
                {
                    _combatActionMode = CombatActionMode.None;
                    NotifyCombatSelectionChanged();
                }
                return;
            }

            if (TryRaycastUnit(out var unit) && unit != null)
            {
                if (IsHostileTarget(unit))
                {
                    TryIssueUnitTargetOrder(unit);
                    return;
                }

                SelectUnit(unit);
                return;
            }

            if (!IsCombatPhase())
            {
                if (TryRaycastNode(out _))
                {
                    CloseCurrentInfoSelection();
                }
                return;
            }

            if (_selectedUnit != null &&
                TryRaycastNode(out var node) &&
                node != null)
            {
                if (!string.IsNullOrEmpty(node.NodeId))
                {
                    if (_combatActionMode == CombatActionMode.Attack)
                    {
                        if (TryIssueStructureTargetOrder(node.NodeId))
                        {
                            _combatActionMode = CombatActionMode.None;
                            NotifyCombatSelectionChanged();
                        }
                        return;
                    }
                    if (_combatActionMode == CombatActionMode.Move)
                    {
                        if (TryIssueAuthoritativeMoveOrder(node.NodeId))
                        {
                            _combatActionMode = CombatActionMode.None;
                            NotifyCombatSelectionChanged();
                        }
                        return;
                    }
                }
            }

            if (TryRaycastNode(out _))
            {
                if (_combatActionMode == CombatActionMode.None)
                {
                    CloseCurrentInfoSelection();
                }
                return;
            }

            ClearCombatSelection();
        }

        private void UpdateCombatMode()
        {
            if (_selectedUnit == null)
            {
                if (!string.IsNullOrEmpty(_hoverPreviewNodeId))
                {
                    ClearMovePreviewState();
                }

                if (_highlightNodeIds.Count > 0)
                {
                    ClearNodeHighlights();
                }
                return;
            }

            if (_combatActionMode == CombatActionMode.Attack)
            {
                if (_highlightNodeIds.Count == 0)
                {
                    RefreshAttackRangeHighlights();
                }
                return;
            }

            if (_combatActionMode != CombatActionMode.Move)
            {
                if (_highlightNodeIds.Count > 0)
                {
                    ClearNodeHighlights();
                }
                if (!string.IsNullOrEmpty(_hoverPreviewNodeId))
                {
                    ClearMovePreviewState();
                }
                return;
            }

            if (IsPointerOverUI())
            {
                ClearMovePreviewState();
                return;
            }

            if (!TryRaycastNode(out var node) || node == null || string.IsNullOrEmpty(node.NodeId))
            {
                ClearMovePreviewState();
                return;
            }

            if (string.Equals(_hoverPreviewNodeId, node.NodeId, StringComparison.Ordinal))
            {
                return;
            }

            if (Time.unscaledTime < _nextMovePreviewRequestAt)
            {
                return;
            }

            RequestMovePreview(node.NodeId);
        }

        private void HandleCombatCancel()
        {
            if (_mode == Mode.Build)
            {
                ExitBuildMode();
                BlockInputAfterModeSwitch();
                return;
            }

            if (_combatActionMode != CombatActionMode.None)
            {
                _combatActionMode = CombatActionMode.None;
                ClearMovePreviewState();
                NotifyCombatSelectionChanged();
                BlockInputAfterModeSwitch();
                return;
            }

            if (_selectedUnit != null)
            {
                ClearCombatSelection();
                BlockInputAfterModeSwitch();
            }
        }

        private void RequestMovePreview(string targetNodeId)
        {
            if (_selectedUnit == null || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            ClearNodeHighlights();
            _hoverPreviewNodeId = targetNodeId;
            _nextMovePreviewRequestAt = Time.unscaledTime + Mathf.Max(0.02f, movePreviewRequestThrottleSeconds);
            _movePreviewRequestSequence++;
            var requestId = $"move-preview-{_selectedUnit.UnitId}-{_movePreviewRequestSequence}";
            PlanningDraftCache.EnsureInstance()?.TrackPreviewRequest(requestId, _selectedUnit.UnitId, "move", targetNodeId);
            Debug.Log($"[MapInputHandler] 请求路径预览 unit={_selectedUnit.UnitId} hover_node={targetNodeId} request={requestId}");
            GameIntents.PreviewMove(requestId, _selectedUnit.UnitId, targetNodeId);
        }

        private bool TryIssueAuthoritativeMoveOrder(string targetNodeId)
        {
            if (_selectedUnit == null || string.IsNullOrWhiteSpace(targetNodeId))
            {
                return false;
            }

            if (!TryGetCurrentMovePreview(_selectedUnit.UnitId, targetNodeId, out var preview))
            {
                RequestMovePreview(targetNodeId);
                preview = new PathPreviewDto
                {
                    Valid = true,
                    PathNodeIds = new List<string>()
                };
            }

                if (preview != null && !preview.Valid)
                {
                    ShowUserError(MovePreviewPresenter.ResolveErrorMessage(preview, targetNodeId));
                    return false;
                }

            Debug.Log($"[MapInputHandler] 涓嬭揪绉诲姩鍛戒护 unit={_selectedUnit.UnitId} target={targetNodeId} preview_valid={preview.Valid} preview_nodes={preview.PathNodeIds.Count}");
            SendMoveCommand(_selectedUnit.UnitId, targetNodeId);
            return true;
        }

        private string GetCombatPrompt()
        {
            if (_selectedUnit == null)
            {
                return "Select your unit to start issuing commands.";
            }

            return _combatActionMode switch
            {
                CombatActionMode.Move => "悬停节点预览路径，点击后下达移动指令。",
                CombatActionMode.Attack => "点击敌方单位或敌方建筑下达攻击指令。",
                CombatActionMode.Charge => "点击敌方单位下达冲锋指令。",
                _ => "选择动作后再指定目标"
            };
        }

        private void NotifyCombatSelectionChanged()
        {
            CombatSelectionChanged?.Invoke();
        }
    }
}
