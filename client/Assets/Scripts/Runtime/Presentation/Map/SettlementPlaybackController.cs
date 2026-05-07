/*************************************************
 * Project: Panoptes
 * File: SettlementPlaybackController.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Plays unified turn settlement sections in server order.
 *************************************************/

using System;
using System.Collections;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Animation;
using R3;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.Map
{
    public sealed class SettlementPlaybackController : MonoBehaviour
    {
        [SerializeField] private float moveEventWaitSeconds = 0.54f;
        [SerializeField] private float conflictFlashSeconds = 0.18f;
        [SerializeField] private float damagePulseSeconds = 0.16f;
        [SerializeField] private float damagePulseScale = 1.14f;
        [SerializeField] private float sectionPauseSeconds = 0.14f;
        [SerializeField] private float actorFocusPauseSeconds = 0.25f;
        [SerializeField] private float initialPlaybackDelaySeconds = 0.25f;
        [SerializeField] private float unitMoveDurationSeconds = 0.98f;
        [SerializeField] private float attackPlaybackSeconds = 0.55f;
        [SerializeField] private float perUnitPlaybackGapSeconds = 0.5f;
        [SerializeField] private bool enableDamagePopups = true;
        [SerializeField] private DamageNumberPopupController damagePopupController;

        private SettlementStore _settlementStore;
        private MapRenderer _mapRenderer;
        private AnimationQueue _animationQueue;
        private IDisposable _settlementSubscription;
        private Coroutine _playbackCoroutine;
        private UnitView _playbackSelectedUnit;
        private int _lastHandledSettlementSequence;
        private bool _inputLockedForPlayback;

        [Inject]
        private void Construct(
            SettlementStore settlementStore,
            MapRenderer mapRenderer,
            AnimationQueue animationQueue,
            DamageNumberPopupController injectedDamagePopupController)
        {
            _settlementStore = settlementStore;
            _mapRenderer = mapRenderer;
            _animationQueue = animationQueue;
            if (damagePopupController == null)
            {
                damagePopupController = injectedDamagePopupController;
            }
            if (isActiveAndEnabled)
            {
                SubscribeSettlement();
            }
        }

        private void OnEnable()
        {
            SubscribeSettlement();
        }

        private void OnDisable()
        {
            _settlementSubscription?.Dispose();
            _settlementSubscription = null;

            if (_playbackCoroutine != null)
            {
                StopCoroutine(_playbackCoroutine);
                _playbackCoroutine = null;
            }
            EndPlaybackInputLock();
            ClearPlaybackSelection();
        }

        private void SubscribeSettlement()
        {
            _settlementSubscription?.Dispose();
            _settlementSubscription = _settlementStore?.State.Subscribe(this, static (state, self) => self.OnSettlementChanged(state));
            OnSettlementChanged(_settlementStore?.Snapshot);
        }

        private void OnSettlementChanged(SettlementState state)
        {
            if (state == null || state.Sequence <= 0 || state.Sequence == _lastHandledSettlementSequence)
            {
                return;
            }

            _lastHandledSettlementSequence = state.Sequence;
            var settlement = state.Settlement;
            if (settlement?.Sections == null || settlement.Sections.Count == 0)
            {
                return;
            }

            if (_playbackCoroutine != null)
            {
                StopCoroutine(_playbackCoroutine);
                EndPlaybackInputLock();
            }

            _playbackCoroutine = StartCoroutine(PlaySettlement(settlement));
        }

        private IEnumerator PlaySettlement(TurnSettlementDto settlement)
        {
            BeginPlaybackInputLock();
            _animationQueue?.CancelUnitMoves();
            try
            {
                var steps = SettlementPlaybackPlanBuilder.Build(settlement);
                if (steps.Count == 0)
                {
                    _mapRenderer?.ReconcileUnitsToCurrentState();
                    yield break;
                }

                _mapRenderer?.PrepareSettlementPlaybackUnits(steps);
                if (initialPlaybackDelaySeconds > 0.0001f)
                {
                    yield return new WaitForSecondsRealtime(initialPlaybackDelaySeconds);
                }

                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    yield return PlayStep(steps[stepIndex]);
                }
            }
            finally
            {
                _mapRenderer?.ReconcileUnitsToCurrentState();
                EndPlaybackInputLock();
                _playbackCoroutine = null;
            }
        }

        private IEnumerator PlayStep(SettlementPlaybackStep step)
        {
            if (step == null)
            {
                yield break;
            }

            if (step.HasMove)
            {
                _mapRenderer?.PlaceSettlementPlaybackUnitAtMoveStart(step.MoveEvent);
            }

            if (step.HasActor)
            {
                yield return FocusActorForPlayback(step.ActorUnitId);
            }

            if (step.HasMove)
            {
                yield return PlayMove(step.MoveEvent);
            }

            if (step.HasImpacts)
            {
                if (step.HasActor)
                {
                    yield return PlayAttackForStep(step.ActorUnitId);
                }

                for (var i = 0; i < step.Impacts.Count; i++)
                {
                    yield return PlayImpact(step.Impacts[i]);
                }
            }

            if (perUnitPlaybackGapSeconds > 0.0001f)
            {
                yield return new WaitForSecondsRealtime(perUnitPlaybackGapSeconds);
            }

            ClearPlaybackSelection();
        }

        private IEnumerator PlayImpact(SettlementPlaybackImpact impact)
        {
            if (impact == null)
            {
                yield break;
            }

            if (impact.IsUnitImpact)
            {
                if (impact.DamageEvent != null)
                {
                    yield return PlayDamage(impact.DamageEvent);
                }

                if (impact.DeathEvent != null)
                {
                    yield return PlayDeath(impact.DeathEvent, showDamageCue: impact.DamageEvent == null);
                }

                yield break;
            }

            if (impact.IsBuildingImpact)
            {
                yield return PlayBuildingDamage(impact.BuildingEvent);
            }
        }

        private IEnumerator PlayMove(TurnEventDto evt)
        {
            if (_mapRenderer == null || evt == null || string.IsNullOrWhiteSpace(evt.UnitId))
            {
                yield break;
            }

            _mapRenderer.PlaceSettlementPlaybackUnitAtMoveStart(evt);
            var grid = new Vector2Int(evt.ToQ, evt.ToR);
            if (!_mapRenderer.TryGetNodeIdByGrid(grid, out var targetNodeId) ||
                !_mapRenderer.TryGetNodeView(targetNodeId, out var nodeView) ||
                nodeView == null)
            {
                yield break;
            }

            if (!nodeView.IsCurrentlyVisible && !_mapRenderer.TryGetUnitView(evt.UnitId, out _))
            {
                _mapRenderer.SetUnitNode(evt.UnitId, targetNodeId);
                yield break;
            }

            if (!_mapRenderer.TryGetUnitView(evt.UnitId, out var unitView) || unitView == null)
            {
                _mapRenderer.SetUnitNode(evt.UnitId, targetNodeId);
                yield break;
            }

            var target = nodeView.ResolveUnitAnchorWorldPosition();
            yield return UnitMoveAnim.Play(
                unitView,
                target,
                Mathf.Max(0.05f, unitMoveDurationSeconds),
                Camera.main,
                followCameraEnabled: true);
            _mapRenderer.SetUnitNode(evt.UnitId, targetNodeId);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.02f, moveEventWaitSeconds * 0.25f));
        }

        private IEnumerator PlayConflict(TurnEventDto evt)
        {
            if (_mapRenderer == null || !TryResolveNodeForEvent(evt, out var node))
            {
                yield break;
            }

            var actorId = ResolveActorUnitId(evt);
            yield return PlayUnitAttackForPlayback(actorId);
            var counterActorId = !string.IsNullOrWhiteSpace(evt.TargetUnitId) ? evt.TargetUnitId : evt.EnemyUnitId;
            if (!string.Equals(actorId, counterActorId, StringComparison.Ordinal))
            {
                yield return PlayUnitAttackForPlayback(counterActorId);
            }

            node.SetHighlight(true, new Color(1f, 0.45f, 0.2f, 1f));
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, conflictFlashSeconds));
            node.SetHighlightVisible(false);
            ClearPlaybackSelection();
        }

        private IEnumerator PlayDamage(TurnEventDto evt)
        {
            if (_mapRenderer == null || !_mapRenderer.TryGetUnitView(evt.UnitId, out var unit) || unit == null)
            {
                TryShowUnitDamagePopupFallback(evt);
                yield break;
            }

            TryShowDamagePopup(unit.transform, evt, isBuilding: false);
            yield return PulseUnit(unit.transform, Mathf.Max(0.05f, damagePulseSeconds), Mathf.Max(1.02f, damagePulseScale));
        }

        private IEnumerator PlayDeath(TurnEventDto evt, bool showDamageCue)
        {
            if (_mapRenderer == null || !_mapRenderer.TryGetUnitView(evt.UnitId, out var unit) || unit == null)
            {
                if (showDamageCue)
                {
                    TryShowUnitDamagePopupFallback(evt);
                }
                yield break;
            }

            if (showDamageCue)
            {
                TryShowDamagePopup(unit.transform, evt, isBuilding: false);
                yield return PulseUnit(unit.transform, Mathf.Max(0.05f, damagePulseSeconds), Mathf.Max(1.02f, damagePulseScale));
            }
            _mapRenderer.RemoveRuntimeUnit(evt.UnitId, false);
        }

        private IEnumerator PlayBuildingDamage(TurnEventDto evt)
        {
            if (!TryResolveNodeForEvent(evt, out var node) || node == null)
            {
                yield break;
            }

            var popupTarget = node.BuildingInstance != null ? node.BuildingInstance.transform : node.transform;
            TryShowDamagePopup(popupTarget, evt, isBuilding: true);

            node.SetHighlight(true, new Color(0.35f, 0.9f, 1f, 1f));
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, conflictFlashSeconds));
            node.SetHighlightVisible(false);
        }

        private IEnumerator PlayAttackerAnimation(TurnEventDto evt)
        {
            if (evt == null)
            {
                yield break;
            }

            var attackerId = ResolveActorUnitId(evt);
            if (!string.IsNullOrWhiteSpace(attackerId))
            {
                yield return PlayUnitAttackForPlayback(attackerId);
                yield break;
            }

            yield return PlayUnitAttackForPlayback(ReadEventString(evt, "attacker", "attacker_unit_id", "killer_id"));
        }

        private IEnumerator PlayUnitAttackForPlayback(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                yield break;
            }

            if (_mapRenderer == null || !_mapRenderer.TryGetUnitView(unitId.Trim(), out var unit) || unit == null)
            {
                yield break;
            }

            yield return FocusActorForPlayback(unitId);
            unit.PlayAttackAnimation();
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, attackPlaybackSeconds));
            ClearPlaybackSelection();

            if (perUnitPlaybackGapSeconds > 0.0001f)
            {
                yield return new WaitForSecondsRealtime(perUnitPlaybackGapSeconds);
            }
        }

        private IEnumerator PlayAttackForStep(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                yield break;
            }

            if (_mapRenderer == null || !_mapRenderer.TryGetUnitView(unitId.Trim(), out var unit) || unit == null)
            {
                yield break;
            }

            unit.PlayAttackAnimation();
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, attackPlaybackSeconds));
        }

        private bool TryPlayAttackerAnimation(TurnEventDto evt)
        {
            if (evt == null)
            {
                return false;
            }

            return TryPlayUnitAttackAnimation(evt.UnitId) ||
                   TryPlayUnitAttackAnimation(ReadEventString(evt, "attacker", "attacker_unit_id", "killer_id"));
        }

        private bool TryPlayUnitAttackAnimation(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            if (_mapRenderer == null || !_mapRenderer.TryGetUnitView(unitId, out var unit) || unit == null)
            {
                return false;
            }

            return unit.PlayAttackAnimation();
        }

        private IEnumerator PlayMapPulse(TurnEventDto evt)
        {
            if (!TryResolveNodeForEvent(evt, out var node) || node == null)
            {
                yield break;
            }

            if (!CanShowNodeCue(node))
            {
                yield break;
            }

            node.SetHighlight(true, new Color(0.35f, 0.9f, 1f, 1f));
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, conflictFlashSeconds));
            node.SetHighlightVisible(false);
        }

        private IEnumerator PlayAuthorityCue(TurnEventDto evt)
        {
            if (evt == null)
            {
                yield break;
            }

            if (TryResolveNodeForEvent(evt, out var node) && node != null)
            {
                if (!CanShowNodeCue(node))
                {
                    yield break;
                }

                node.SetHighlight(true, new Color(1f, 0.9f, 0.35f, 1f));
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, sectionPauseSeconds));
                node.SetHighlightVisible(false);
                yield break;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.03f, sectionPauseSeconds * 0.5f));
        }

        private IEnumerator PulseUnit(Transform target, float duration, float scaleMultiplier)
        {
            if (target == null)
            {
                yield break;
            }

            var startScale = target.localScale;
            var peakScale = startScale * scaleMultiplier;
            var half = duration * 0.5f;
            var elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / half);
                target.localScale = Vector3.Lerp(startScale, peakScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / half);
                target.localScale = Vector3.Lerp(peakScale, startScale, t);
                yield return null;
            }

            target.localScale = startScale;
        }

        private void TryShowDamagePopup(Transform target, TurnEventDto evt, bool isBuilding)
        {
            if (!enableDamagePopups || target == null)
            {
                return;
            }

            if (damagePopupController == null)
            {
                return;
            }

            var damage = ResolveDamageValue(evt);
            if (damage <= 0)
            {
                return;
            }

            damagePopupController.ShowDamage(target, damage, isBuilding);
        }

        private void TryShowUnitDamagePopupFallback(TurnEventDto evt)
        {
            var damage = ResolveDamageValue(evt);
            if (damage <= 0 || !TryResolveUnitPopupPosition(evt, out var position))
            {
                return;
            }

            if (damagePopupController == null)
            {
                return;
            }

            damagePopupController.ShowDamage(position, damage, isBuilding: false);
        }

        private bool TryResolveUnitPopupPosition(TurnEventDto evt, out Vector3 position)
        {
            position = default;
            if (evt == null)
            {
                return false;
            }

            if (_mapRenderer == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(evt.NodeId) && _mapRenderer.TryGetNodeView(evt.NodeId, out var eventNode) && eventNode != null)
            {
                position = eventNode.transform.position;
                return true;
            }

            if (HasEventKey(evt, "pos_q") || HasEventKey(evt, "pos_r"))
            {
                if (_mapRenderer.TryGetNodeIdByGrid(new Vector2Int(evt.PosQ, evt.PosR), out var eventNodeId) &&
                    _mapRenderer.TryGetNodeView(eventNodeId, out eventNode) &&
                    eventNode != null)
                {
                    position = eventNode.transform.position;
                    return true;
                }
            }

            if (_mapRenderer.TryGetUnitView(evt.UnitId, out var unit) && unit != null &&
                _mapRenderer.TryGetNodeIdByGrid(unit.GridPos, out var nodeId) &&
                _mapRenderer.TryGetNodeView(nodeId, out var node) &&
                node != null)
            {
                position = node.transform.position;
                return true;
            }

            return false;
        }

        private static bool HasEventKey(TurnEventDto evt, string key)
        {
            return evt?.Data != null &&
                   !string.IsNullOrWhiteSpace(key) &&
                   evt.Data.ContainsKey(key);
        }

        private static int ResolveDamageValue(TurnEventDto evt)
        {
            if (evt == null)
            {
                return 0;
            }

            if (evt.Damage > 0)
            {
                return evt.Damage;
            }

            var hpBefore = ReadEventInt(evt, "hp_before", "unit_hp_before", "building_hp_before");
            var hpAfter = evt.HpAfter > 0
                ? evt.HpAfter
                : ReadEventInt(evt, "hp_after", "unit_hp_after", "building_hp_after", "building_hp");

            if (hpBefore > 0 && hpAfter >= 0 && hpBefore > hpAfter)
            {
                return hpBefore - hpAfter;
            }

            return 0;
        }

        private static int ReadEventInt(TurnEventDto evt, params string[] keys)
        {
            if (evt?.Data == null || keys == null)
            {
                return 0;
            }

            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (evt.Data.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed))
                {
                    return parsed;
                }
            }

            return 0;
        }

        private static string ReadEventString(TurnEventDto evt, params string[] keys)
        {
            if (evt?.Data == null || keys == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (evt.Data.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
                {
                    return raw;
                }
            }

            return string.Empty;
        }

        private bool TryResolveNodeForEvent(TurnEventDto evt, out NodeView node)
        {
            node = null;
            if (_mapRenderer == null || evt == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(evt.NodeId) && _mapRenderer.TryGetNodeView(evt.NodeId, out node))
            {
                return node != null;
            }

            if (_mapRenderer.TryGetNodeIdByGrid(new Vector2Int(evt.PosQ, evt.PosR), out var nodeId))
            {
                return _mapRenderer.TryGetNodeView(nodeId, out node) && node != null;
            }

            return false;
        }

        private static bool CanShowNodeCue(NodeView node)
        {
            return node != null && node.IsCurrentlyVisible;
        }

        private IEnumerator FocusActorForPlayback(string unitId)
        {
            ClearPlaybackSelection();
            if (string.IsNullOrWhiteSpace(unitId) ||
                _mapRenderer == null ||
                !_mapRenderer.TryGetUnitView(unitId.Trim(), out var unit) ||
                unit == null ||
                !unit.gameObject.activeInHierarchy)
            {
                yield break;
            }

            _playbackSelectedUnit = unit;
            _playbackSelectedUnit.SetSelected(true);
            CinemachineMapCameraController.TryFocus(unit.transform.position, false);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.02f, actorFocusPauseSeconds));
        }

        private void ClearPlaybackSelection()
        {
            if (_playbackSelectedUnit == null)
            {
                return;
            }

            _playbackSelectedUnit.SetSelected(false);
            _playbackSelectedUnit = null;
        }

        private void BeginPlaybackInputLock()
        {
            if (_inputLockedForPlayback)
            {
                return;
            }

            _inputLockedForPlayback = true;
            MapPlanningInputController.SetPlaybackInputLocked(true);
            CinemachineMapCameraController.SetPresentationInputLocked(true);
        }

        private void EndPlaybackInputLock()
        {
            if (!_inputLockedForPlayback)
            {
                return;
            }

            _inputLockedForPlayback = false;
            MapPlanningInputController.SetPlaybackInputLocked(false);
            CinemachineMapCameraController.SetPresentationInputLocked(false);
        }

        private static string ResolveActorUnitId(TurnEventDto evt)
        {
            if (evt == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(evt.UnitId)
                ? evt.UnitId
                : ReadEventString(evt, "attacker", "attacker_unit_id", "killer_id");
        }

    }
}
