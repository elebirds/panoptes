/*************************************************
 * Project: Panoptes
 * File: SettlementPlaybackController.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Plays unified turn settlement sections in server order.
 *************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Animation;
using R3;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif
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
        [SerializeField] private SettlementPlaybackMode playbackMode = SettlementPlaybackMode.Fast;
        [SerializeField] private float fastMoveDurationSeconds = 0.35f;
        [SerializeField] private float minimumVisibleMoveDurationSeconds = 0.55f;
        [SerializeField] private float fastAttackPlaybackSeconds = 0.32f;
        [SerializeField] private float fastFocusPauseSeconds = 0.08f;
        [SerializeField] private float ambientMoveProxyScale = 0.96f;
        [SerializeField] private KeyCode skipPlaybackKey = KeyCode.Space;
        [SerializeField] private KeyCode alternateSkipPlaybackKey = KeyCode.Escape;
        [SerializeField] private bool enableDamagePopups = true;
        [SerializeField] private DamageNumberPopupController damagePopupController;

        private SettlementStore _settlementStore;
        private TurnStore _turnStore;
        private GameIntentService _gameIntentService;
        private MapRenderer _mapRenderer;
        private AnimationQueue _animationQueue;
        private IDisposable _settlementSubscription;
        private IDisposable _turnSubscription;
        private Coroutine _playbackCoroutine;
        private SettlementPlaybackRunner _runner;
        private UnitView _playbackSelectedUnit;
        private SettlementCameraPolicy _cameraPolicy;
        private SettlementPlaybackControlOverlay _controlOverlay;
        private TurnSettlementDto _activeSettlement;
        private readonly List<GameObject> _activeMoveProxies = new();
        private int _lastHandledSettlementSequence;
        private bool _inputLockedForPlayback;
        private bool _isPlayingSettlement;
        private int _turnReportTurn;
        private float _turnReportDeadline = -1f;
        private bool _turnReportPlaybackCompleted;
        private int _turnReportPlaybackCompletedTurn;
        private bool _turnReportAckSent;
        private bool _turnReportActive;

        [Inject]
        private void Construct(
            SettlementStore settlementStore,
            TurnStore turnStore,
            GameIntentService gameIntentService,
            MapRenderer mapRenderer,
            AnimationQueue animationQueue,
            DamageNumberPopupController injectedDamagePopupController)
        {
            _settlementStore = settlementStore;
            _turnStore = turnStore;
            _gameIntentService = gameIntentService;
            _mapRenderer = mapRenderer;
            _animationQueue = animationQueue;
            if (damagePopupController == null)
            {
                damagePopupController = injectedDamagePopupController;
            }
            if (isActiveAndEnabled)
            {
                SubscribeTurn();
                SubscribeSettlement();
            }
        }

        private void OnEnable()
        {
            EnsureRunner();
            EnsureControlOverlay();
            UpdatePlaybackControls();
            SubscribeTurn();
            SubscribeSettlement();
        }

        private void OnDisable()
        {
            _turnSubscription?.Dispose();
            _turnSubscription = null;

            _settlementSubscription?.Dispose();
            _settlementSubscription = null;

            StopPlayback(false);
            ResetTurnReportGate();
            _controlOverlay?.Dispose();
            _controlOverlay = null;
        }

        private void Update()
        {
            if (_isPlayingSettlement && WasSkipRequested())
            {
                SkipPlayback();
            }

            if (_turnReportActive &&
                !_turnReportAckSent &&
                _turnReportDeadline > 0f &&
                Time.unscaledTime >= _turnReportDeadline)
            {
                HandleTurnReportTimeout();
            }
        }

        public void SetPlaybackMode(SettlementPlaybackMode mode)
        {
            if (playbackMode == mode)
            {
                UpdatePlaybackControls();
                return;
            }

            playbackMode = mode;
            if (_isPlayingSettlement && _activeSettlement != null)
            {
                RestartActivePlayback();
                return;
            }

            UpdatePlaybackControls();
        }

        public void SkipPlayback()
        {
            StopPlayback(true);
        }

        private void StopPlayback(bool acknowledgeTurnReport)
        {
            if (_playbackCoroutine != null)
            {
                StopCoroutine(_playbackCoroutine);
                _playbackCoroutine = null;
            }
            _runner?.StopActive();
            ClearActiveMoveProxies();
            _mapRenderer?.ReconcileUnitsToCurrentState();
            EndPlaybackInputLock();
            ClearPlaybackSelection();
            _cameraPolicy = null;
            _isPlayingSettlement = false;
            UpdatePlaybackControls();
            if (acknowledgeTurnReport)
            {
                MarkTurnReportPlaybackCompleted();
            }
        }

        public bool FocusSettlementEvent(TurnEventDto evt)
        {
            if (_mapRenderer == null || evt == null)
            {
                return false;
            }

            var unitId = ResolveFocusUnitId(evt);
            if (!string.IsNullOrWhiteSpace(unitId) &&
                _mapRenderer.TryGetUnitView(unitId.Trim(), out var unit) &&
                unit != null)
            {
                CinemachineMapCameraController.TryFocus(unit.transform.position, false);
                return true;
            }

            if (TryResolveNodeForEvent(evt, out var node) && node != null)
            {
                CinemachineMapCameraController.TryFocus(node.transform.position, false);
                return true;
            }

            if (string.Equals(evt.Type, "unit_moved", StringComparison.Ordinal) &&
                _mapRenderer.TryGetNodeIdByGrid(new Vector2Int(evt.ToQ, evt.ToR), out var targetNodeId) &&
                _mapRenderer.TryGetNodeView(targetNodeId, out node) &&
                node != null)
            {
                CinemachineMapCameraController.TryFocus(node.transform.position, false);
                return true;
            }

            return false;
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
            _activeSettlement = settlement;
            if (settlement?.Sections == null || settlement.Sections.Count == 0)
            {
                UpdatePlaybackControls();
                MarkTurnReportPlaybackCompleted();
                return;
            }

            if (_playbackCoroutine != null)
            {
                StopCoroutine(_playbackCoroutine);
                _runner?.StopActive();
                EndPlaybackInputLock();
            }

            _playbackCoroutine = StartCoroutine(PlaySettlement(settlement));
        }

        private void SubscribeTurn()
        {
            _turnSubscription?.Dispose();
            _turnSubscription = _turnStore?.State.Subscribe(this, static (state, self) => self.OnTurnChanged(state));
            OnTurnChanged(_turnStore?.Snapshot);
        }

        private void OnTurnChanged(TurnState state)
        {
            if (state == null)
            {
                ResetTurnReportGate();
                return;
            }

            if (!string.Equals(state.Phase, GamePhases.TurnReport, StringComparison.Ordinal))
            {
                if (_turnReportActive && !_turnReportAckSent && _isPlayingSettlement)
                {
                    StopPlayback(false);
                }

                ResetTurnReportGate();
                return;
            }

            var turn = state.Turn;
            var timeoutSeconds = Mathf.Max(0, state.TimeoutSeconds);
            var turnChanged = turn != _turnReportTurn;
            _turnReportTurn = turn;
            _turnReportActive = true;
            if (turnChanged)
            {
                if (!_turnReportAckSent || _turnReportPlaybackCompletedTurn != turn)
                {
                    _turnReportAckSent = false;
                }
                _turnReportPlaybackCompleted = _turnReportPlaybackCompletedTurn == turn;
            }

            _turnReportDeadline = timeoutSeconds > 0 ? Time.unscaledTime + timeoutSeconds : -1f;
            TryAcknowledgeTurnReport();
        }

        private void HandleTurnReportTimeout()
        {
            if (!_turnReportActive || _turnReportAckSent)
            {
                return;
            }

            if (_isPlayingSettlement)
            {
                StopPlayback(true);
                return;
            }

            MarkTurnReportPlaybackCompleted();
        }

        private void MarkTurnReportPlaybackCompleted()
        {
            if (_turnReportAckSent)
            {
                return;
            }

            _turnReportPlaybackCompleted = true;
            if (_turnStore?.Snapshot != null && _turnStore.Snapshot.Turn > 0)
            {
                _turnReportPlaybackCompletedTurn = _turnStore.Snapshot.Turn;
            }
            else if (_turnReportTurn > 0)
            {
                _turnReportPlaybackCompletedTurn = _turnReportTurn;
            }
            TryAcknowledgeTurnReport();
        }

        private void TryAcknowledgeTurnReport()
        {
            if (_turnReportAckSent)
            {
                return;
            }

            var turn = _turnReportTurn > 0 ? _turnReportTurn : _turnReportPlaybackCompletedTurn;
            if (turn <= 0)
            {
                return;
            }

            var deadlineReached = _turnReportDeadline > 0f && Time.unscaledTime >= _turnReportDeadline;
            if (!_turnReportPlaybackCompleted && !deadlineReached)
            {
                return;
            }

            if (_gameIntentService?.AcknowledgeTurnReport(turn) == true)
            {
                _turnReportAckSent = true;
            }
        }

        private void ResetTurnReportGate()
        {
            _turnReportTurn = 0;
            _turnReportDeadline = -1f;
            _turnReportPlaybackCompleted = false;
            _turnReportAckSent = false;
            _turnReportActive = false;
        }

        private IEnumerator PlaySettlement(TurnSettlementDto settlement)
        {
            BeginPlaybackInputLock();
            _isPlayingSettlement = true;
            _activeSettlement = settlement;
            _cameraPolicy = SettlementCameraPolicy.Start(playbackMode);
            _animationQueue?.CancelUnitMoves();
            UpdatePlaybackControls();
            var playbackCompletedNaturally = false;
            try
            {
                var steps = SettlementPlaybackPlanBuilder.Build(settlement);
                var schedule = SettlementPlaybackScheduler.Build(steps, playbackMode);
                if (schedule.Windows.Count == 0)
                {
                    _mapRenderer?.ReconcileUnitsToCurrentState();
                    playbackCompletedNaturally = true;
                }
                else
                {
                    var scheduledSteps = CollectScheduledSteps(schedule.Windows);
                    EnsureSettlementPlaybackImpactUnits(settlement);
                    _mapRenderer?.PrepareSettlementPlaybackUnits(scheduledSteps);
                    PrimeUnitHpBeforePlayback(scheduledSteps);
                    PrimeBuildingHpBeforePlayback(scheduledSteps);
                    if (initialPlaybackDelaySeconds > 0.0001f)
                    {
                        yield return new WaitForSecondsRealtime(initialPlaybackDelaySeconds);
                    }

                    _mapRenderer?.PlaceSettlementPlaybackUnitsAtMoveStarts(scheduledSteps, 0, preserveHitPoints: true);
                    PrimeUnitHpBeforePlayback(scheduledSteps);
                    PrimeBuildingHpBeforePlayback(scheduledSteps);
                    for (var windowIndex = 0; windowIndex < schedule.Windows.Count; windowIndex++)
                    {
                        yield return PlayWindow(schedule.Windows[windowIndex]);
                    }

                    playbackCompletedNaturally = true;
                }
            }
            finally
            {
                _mapRenderer?.ReconcileUnitsToCurrentState();
                _runner?.StopActive();
                ClearActiveMoveProxies();
                EndPlaybackInputLock();
                _playbackCoroutine = null;
                _cameraPolicy = null;
                _isPlayingSettlement = false;
                UpdatePlaybackControls();
            }

            if (playbackCompletedNaturally)
            {
                MarkTurnReportPlaybackCompleted();
            }
        }

        private IEnumerator PlayWindow(SettlementPlaybackWindow window)
        {
            if (window == null || window.Steps == null || window.Steps.Count == 0)
            {
                yield break;
            }

            var allowFocus = _cameraPolicy == null
                ? window.AllowsCameraFocus
                : _cameraPolicy.ShouldFocus(window);
            EnsureRunner();
            yield return _runner.PlayWindow(window, allowFocus, PlayBatchMove, PlayStep, ClearPlaybackSelection);
        }

        private IEnumerator PlayBatchMove(TurnEventDto evt)
        {
            _mapRenderer?.PlaceSettlementPlaybackUnitAtMoveStart(evt, preserveHitPoints: true);
            yield return PlayMove(evt, followCamera: false, waitAfterMove: false, useProxy: false, ambientMove: true);
        }

        private IEnumerator PlayStep(SettlementPlaybackStep step, bool allowCameraFocus)
        {
            if (step == null)
            {
                yield break;
            }

            if (step.HasMove)
            {
                _mapRenderer?.PlaceSettlementPlaybackUnitAtMoveStart(step.MoveEvent, preserveHitPoints: true);
            }

            if (step.HasActor)
            {
                yield return FocusActorForPlayback(step.ActorUnitId, allowCameraFocus);
            }
            else if (allowCameraFocus && step.ImpactEvents.Count > 0)
            {
                yield return FocusNodeForPlayback(step.ImpactEvents[0]);
            }

            if (step.HasMove)
            {
                yield return PlayMove(step.MoveEvent, allowCameraFocus, ambientMove: false);
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

            var stepGap = ResolveStepGap();
            if (stepGap > 0.0001f)
            {
                yield return new WaitForSecondsRealtime(stepGap);
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
            yield return PlayMove(evt, followCamera: true, waitAfterMove: true);
        }

        private IEnumerator PlayMove(
            TurnEventDto evt,
            bool followCamera,
            bool waitAfterMove = true,
            bool useProxy = false,
            bool ambientMove = false)
        {
            if (_mapRenderer == null || evt == null || string.IsNullOrWhiteSpace(evt.UnitId))
            {
                yield break;
            }

            _mapRenderer.PlaceSettlementPlaybackUnitAtMoveStart(evt, preserveHitPoints: true);
            var grid = new Vector2Int(evt.ToQ, evt.ToR);
            if (!_mapRenderer.TryGetNodeIdByGrid(grid, out var targetNodeId) ||
                !_mapRenderer.TryGetNodeView(targetNodeId, out var nodeView) ||
                nodeView == null)
            {
                yield break;
            }

            if (!_mapRenderer.TryGetUnitView(evt.UnitId, out var unitView) || unitView == null)
            {
                _mapRenderer.SetUnitNode(evt.UnitId, targetNodeId);
                yield break;
            }

            var target = nodeView.ResolveUnitAnchorWorldPosition();
            var duration = ResolveMoveDuration(ambientMove);
            if (useProxy)
            {
                var start = unitView.transform.position;
                _mapRenderer.SetUnitNode(evt.UnitId, targetNodeId);
                yield return PlayMoveProxy(unitView, start, target, duration);
                yield break;
            }

            yield return UnitMoveAnim.Play(
                unitView,
                target,
                duration,
                Camera.main,
                followCameraEnabled: followCamera);
            _mapRenderer.SetUnitNode(evt.UnitId, targetNodeId);
            if (waitAfterMove)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.02f, moveEventWaitSeconds * 0.25f));
            }
        }

        private IEnumerator PlayMoveProxy(UnitView sourceUnit, Vector3 start, Vector3 target, float duration)
        {
            if (sourceUnit == null)
            {
                yield break;
            }

            var proxyObject = Instantiate(sourceUnit.gameObject, start, sourceUnit.transform.rotation, sourceUnit.transform.parent);
            proxyObject.name = sourceUnit.gameObject.name + "_PlaybackProxy";
            proxyObject.transform.localScale = sourceUnit.transform.localScale * Mathf.Max(0.1f, ambientMoveProxyScale);
            DisableProxyInteraction(proxyObject);
            _activeMoveProxies.Add(proxyObject);

            var proxyUnit = proxyObject.GetComponent<UnitView>();
            if (proxyUnit == null)
            {
                _activeMoveProxies.Remove(proxyObject);
                DestroyProxy(proxyObject);
                yield break;
            }

            proxyUnit.SetSelected(false);
            try
            {
                yield return UnitMoveAnim.Play(proxyUnit, target, duration);
            }
            finally
            {
                _activeMoveProxies.Remove(proxyObject);
                DestroyProxy(proxyObject);
            }
        }

        private static void DisableProxyInteraction(GameObject proxyObject)
        {
            if (proxyObject == null)
            {
                return;
            }

            var colliders = proxyObject.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private static void DestroyProxy(GameObject proxyObject)
        {
            if (proxyObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(proxyObject);
                return;
            }

            DestroyImmediate(proxyObject);
        }

        private void ClearActiveMoveProxies()
        {
            for (var i = _activeMoveProxies.Count - 1; i >= 0; i--)
            {
                DestroyProxy(_activeMoveProxies[i]);
            }

            _activeMoveProxies.Clear();
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
            _mapRenderer?.EnsureSettlementPlaybackUnitForEvent(evt);
            if (_mapRenderer == null || !_mapRenderer.TryGetUnitView(evt.UnitId, out var unit) || unit == null)
            {
                TryShowUnitDamagePopupFallback(evt);
                yield break;
            }

            ApplyUnitHpAfter(evt);
            TryShowDamagePopup(unit.transform, evt, isBuilding: false);
            yield return PulseUnit(unit.transform, Mathf.Max(0.05f, damagePulseSeconds), Mathf.Max(1.02f, damagePulseScale));
        }

        private IEnumerator PlayDeath(TurnEventDto evt, bool showDamageCue)
        {
            _mapRenderer?.EnsureSettlementPlaybackUnitForEvent(evt);
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
                ApplyUnitHpAfter(evt);
                TryShowDamagePopup(unit.transform, evt, isBuilding: false);
                yield return PulseUnit(unit.transform, Mathf.Max(0.05f, damagePulseSeconds), Mathf.Max(1.02f, damagePulseScale));
            }
            _mapRenderer.RemoveRuntimeUnit(evt.UnitId, false);
        }

        private void EnsureSettlementPlaybackImpactUnits(TurnSettlementDto settlement)
        {
            if (_mapRenderer == null || settlement?.Sections == null)
            {
                return;
            }

            for (var sectionIndex = 0; sectionIndex < settlement.Sections.Count; sectionIndex++)
            {
                var events = settlement.Sections[sectionIndex]?.Events;
                if (events == null)
                {
                    continue;
                }

                for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
                {
                    var evt = events[eventIndex];
                    if (!IsUnitDamageOrDeathEvent(evt))
                    {
                        continue;
                    }

                    _mapRenderer.EnsureSettlementPlaybackUnitForEvent(evt);
                }
            }
        }

        private IEnumerator PlayBuildingDamage(TurnEventDto evt)
        {
            if (!TryResolveNodeForEvent(evt, out var node) || node == null)
            {
                yield break;
            }

            var popupTarget = node.BuildingInstance != null ? node.BuildingInstance.transform : node.transform;
            ApplyBuildingHpAfter(evt);
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

            yield return FocusActorForPlayback(unitId, allowCameraFocus: true);
            unit.PlayAttackAnimation();
            yield return new WaitForSecondsRealtime(ResolveAttackDuration());
            ClearPlaybackSelection();

            var gap = ResolveStepGap();
            if (gap > 0.0001f)
            {
                yield return new WaitForSecondsRealtime(gap);
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
            yield return new WaitForSecondsRealtime(ResolveAttackDuration());
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

        private void ApplyUnitHpAfter(TurnEventDto evt)
        {
            if (evt == null || _mapRenderer == null || string.IsNullOrWhiteSpace(evt.UnitId))
            {
                return;
            }

            var hpAfter = ResolveHpAfter(evt);
            if (hpAfter < 0)
            {
                return;
            }

            _mapRenderer.ApplySettlementUnitHitPoints(evt.UnitId, hpAfter);
        }

        private void PrimeUnitHpBeforePlayback(IReadOnlyList<SettlementPlaybackStep> steps)
        {
            if (_mapRenderer == null || steps == null || steps.Count == 0)
            {
                return;
            }

            var primedUnitIds = new HashSet<string>();
            for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                var step = steps[stepIndex];
                if (step?.Impacts == null)
                {
                    continue;
                }

                for (var impactIndex = 0; impactIndex < step.Impacts.Count; impactIndex++)
                {
                    var impact = step.Impacts[impactIndex];
                    var evt = impact?.DamageEvent ?? impact?.DeathEvent;
                    if (evt == null || string.IsNullOrWhiteSpace(evt.UnitId))
                    {
                        continue;
                    }

                    var unitId = evt.UnitId.Trim();
                    if (!primedUnitIds.Add(unitId))
                    {
                        continue;
                    }

                    var hpBefore = ResolveHpBefore(evt);
                    if (hpBefore < 0)
                    {
                        continue;
                    }

                    _mapRenderer.ApplySettlementUnitHitPoints(unitId, hpBefore);
                }
            }
        }

        private void ApplyBuildingHpAfter(TurnEventDto evt)
        {
            if (evt == null || _mapRenderer == null)
            {
                return;
            }

            var hpAfter = ResolveHpAfter(evt);
            if (hpAfter < 0)
            {
                return;
            }

            _mapRenderer.ApplySettlementBuildingHitPoints(evt, hpAfter);
        }

        private void PrimeBuildingHpBeforePlayback(IReadOnlyList<SettlementPlaybackStep> steps)
        {
            if (_mapRenderer == null || steps == null || steps.Count == 0)
            {
                return;
            }

            var primedNodeIds = new HashSet<string>();
            for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                var step = steps[stepIndex];
                if (step?.Impacts == null)
                {
                    continue;
                }

                for (var impactIndex = 0; impactIndex < step.Impacts.Count; impactIndex++)
                {
                    var evt = step.Impacts[impactIndex]?.BuildingEvent;
                    if (evt == null)
                    {
                        continue;
                    }

                    var hpBefore = ResolveHpBefore(evt);
                    if (hpBefore <= 0 ||
                        !TryResolveNodeForEvent(evt, out var node) ||
                        node == null ||
                        string.IsNullOrWhiteSpace(node.NodeId) ||
                        !primedNodeIds.Add(node.NodeId))
                    {
                        continue;
                    }

                    _mapRenderer.ApplySettlementBuildingHitPoints(evt, hpBefore);
                }
            }
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

        private static bool IsUnitDamageOrDeathEvent(TurnEventDto evt)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.UnitId))
            {
                return false;
            }

            return string.Equals(evt.Type, "unit_damaged", StringComparison.Ordinal) ||
                   string.Equals(evt.Type, "unit_died", StringComparison.Ordinal);
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

        private static int ResolveHpBefore(TurnEventDto evt)
        {
            if (evt == null)
            {
                return -1;
            }

            var explicitHpBefore = ReadEventInt(evt, "hp_before", "unit_hp_before", "building_hp_before");
            if (explicitHpBefore > 0)
            {
                return explicitHpBefore;
            }

            var hpAfter = ResolveHpAfter(evt);
            var damage = ResolveDamageValue(evt);
            if (hpAfter >= 0 && damage > 0)
            {
                return hpAfter + damage;
            }

            return -1;
        }

        private static int ResolveHpAfter(TurnEventDto evt)
        {
            if (evt == null)
            {
                return -1;
            }

            if (evt.HpAfter > 0)
            {
                return evt.HpAfter;
            }

            if (evt.Data != null)
            {
                if (evt.Data.TryGetValue("hp_after", out var rawHpAfter) && int.TryParse(rawHpAfter, out var parsedHpAfter))
                {
                    return Mathf.Max(0, parsedHpAfter);
                }

                if (evt.Data.TryGetValue("building_hp", out var rawBuildingHp) && int.TryParse(rawBuildingHp, out var parsedBuildingHp))
                {
                    return Mathf.Max(0, parsedBuildingHp);
                }
            }

            if (string.Equals(evt.Type, "unit_died", StringComparison.Ordinal))
            {
                return 0;
            }

            return -1;
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

            var dataNodeId = ReadEventString(evt, "node_id", "target_node_id");
            if (!string.IsNullOrWhiteSpace(dataNodeId) && _mapRenderer.TryGetNodeView(dataNodeId.Trim(), out node))
            {
                return node != null;
            }

            if (string.Equals(evt.Type, "unit_moved", StringComparison.Ordinal) &&
                _mapRenderer.TryGetNodeIdByGrid(new Vector2Int(evt.ToQ, evt.ToR), out var moveTargetNodeId))
            {
                return _mapRenderer.TryGetNodeView(moveTargetNodeId, out node) && node != null;
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

        private IEnumerator FocusActorForPlayback(string unitId, bool allowCameraFocus)
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
            if (allowCameraFocus)
            {
                CinemachineMapCameraController.TryFocus(unit.transform.position, false);
                yield return new WaitForSecondsRealtime(ResolveFocusPause());
            }
        }

        private IEnumerator FocusNodeForPlayback(TurnEventDto evt)
        {
            if (evt == null || !TryResolveNodeForEvent(evt, out var node) || node == null || !node.IsCurrentlyVisible)
            {
                yield break;
            }

            CinemachineMapCameraController.TryFocus(node.transform.position, false);
            yield return new WaitForSecondsRealtime(ResolveFocusPause());
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

        private void EnsureRunner()
        {
            if (_runner != null)
            {
                return;
            }

            _runner = new SettlementPlaybackRunner(this);
        }

        private void EnsureControlOverlay()
        {
            if (_controlOverlay != null)
            {
                return;
            }

            _controlOverlay = new SettlementPlaybackControlOverlay(SkipPlayback, SetPlaybackMode);
        }

        private void UpdatePlaybackControls()
        {
            EnsureControlOverlay();
            _controlOverlay?.Refresh(playbackMode, _isPlayingSettlement);
        }

        private void RestartActivePlayback()
        {
            var settlement = _activeSettlement;
            if (settlement == null)
            {
                UpdatePlaybackControls();
                return;
            }

            if (_playbackCoroutine != null)
            {
                StopCoroutine(_playbackCoroutine);
                _playbackCoroutine = null;
            }

            _runner?.StopActive();
            ClearActiveMoveProxies();
            _mapRenderer?.ReconcileUnitsToCurrentState();
            EndPlaybackInputLock();
            ClearPlaybackSelection();
            _cameraPolicy = null;
            _isPlayingSettlement = false;
            _playbackCoroutine = StartCoroutine(PlaySettlement(settlement));
        }

        private void BeginPlaybackInputLock()
        {
            if (_inputLockedForPlayback)
            {
                return;
            }

            _inputLockedForPlayback = true;
            MapPlanningInputController.SetPlaybackInputLocked(true);
        }

        private void EndPlaybackInputLock()
        {
            if (!_inputLockedForPlayback)
            {
                return;
            }

            _inputLockedForPlayback = false;
            MapPlanningInputController.SetPlaybackInputLocked(false);
        }

        private static IReadOnlyList<SettlementPlaybackStep> CollectScheduledSteps(IReadOnlyList<SettlementPlaybackWindow> windows)
        {
            var steps = new List<SettlementPlaybackStep>();
            if (windows == null)
            {
                return steps;
            }

            for (var windowIndex = 0; windowIndex < windows.Count; windowIndex++)
            {
                var window = windows[windowIndex];
                if (window?.Steps == null)
                {
                    continue;
                }

                for (var stepIndex = 0; stepIndex < window.Steps.Count; stepIndex++)
                {
                    if (window.Steps[stepIndex] != null)
                    {
                        steps.Add(window.Steps[stepIndex]);
                    }
                }
            }

            return steps;
        }

        private bool WasSkipRequested()
        {
            return IsKeyPressed(skipPlaybackKey) || IsKeyPressed(alternateSkipPlaybackKey);
        }

        private static bool IsKeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            if (key == KeyCode.None)
            {
                return false;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            var control = ResolveKeyboardControl(keyboard, key);
            return control != null && control.wasPressedThisFrame;
#else
            return key != KeyCode.None && Input.GetKeyDown(key);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static ButtonControl ResolveKeyboardControl(Keyboard keyboard, KeyCode key)
        {
            return key switch
            {
                KeyCode.Space => keyboard.spaceKey,
                KeyCode.Escape => keyboard.escapeKey,
                KeyCode.Return => keyboard.enterKey,
                KeyCode.KeypadEnter => keyboard.numpadEnterKey,
                KeyCode.Tab => keyboard.tabKey,
                KeyCode.Backspace => keyboard.backspaceKey,
                KeyCode.Delete => keyboard.deleteKey,
                KeyCode.Insert => keyboard.insertKey,
                KeyCode.Home => keyboard.homeKey,
                KeyCode.End => keyboard.endKey,
                KeyCode.PageUp => keyboard.pageUpKey,
                KeyCode.PageDown => keyboard.pageDownKey,
                KeyCode.UpArrow => keyboard.upArrowKey,
                KeyCode.DownArrow => keyboard.downArrowKey,
                KeyCode.LeftArrow => keyboard.leftArrowKey,
                KeyCode.RightArrow => keyboard.rightArrowKey,
                KeyCode.A => keyboard.aKey,
                KeyCode.B => keyboard.bKey,
                KeyCode.C => keyboard.cKey,
                KeyCode.D => keyboard.dKey,
                KeyCode.E => keyboard.eKey,
                KeyCode.F => keyboard.fKey,
                KeyCode.G => keyboard.gKey,
                KeyCode.H => keyboard.hKey,
                KeyCode.I => keyboard.iKey,
                KeyCode.J => keyboard.jKey,
                KeyCode.K => keyboard.kKey,
                KeyCode.L => keyboard.lKey,
                KeyCode.M => keyboard.mKey,
                KeyCode.N => keyboard.nKey,
                KeyCode.O => keyboard.oKey,
                KeyCode.P => keyboard.pKey,
                KeyCode.Q => keyboard.qKey,
                KeyCode.R => keyboard.rKey,
                KeyCode.S => keyboard.sKey,
                KeyCode.T => keyboard.tKey,
                KeyCode.U => keyboard.uKey,
                KeyCode.V => keyboard.vKey,
                KeyCode.W => keyboard.wKey,
                KeyCode.X => keyboard.xKey,
                KeyCode.Y => keyboard.yKey,
                KeyCode.Z => keyboard.zKey,
                KeyCode.Alpha0 => keyboard.digit0Key,
                KeyCode.Alpha1 => keyboard.digit1Key,
                KeyCode.Alpha2 => keyboard.digit2Key,
                KeyCode.Alpha3 => keyboard.digit3Key,
                KeyCode.Alpha4 => keyboard.digit4Key,
                KeyCode.Alpha5 => keyboard.digit5Key,
                KeyCode.Alpha6 => keyboard.digit6Key,
                KeyCode.Alpha7 => keyboard.digit7Key,
                KeyCode.Alpha8 => keyboard.digit8Key,
                KeyCode.Alpha9 => keyboard.digit9Key,
                KeyCode.Keypad0 => keyboard.numpad0Key,
                KeyCode.Keypad1 => keyboard.numpad1Key,
                KeyCode.Keypad2 => keyboard.numpad2Key,
                KeyCode.Keypad3 => keyboard.numpad3Key,
                KeyCode.Keypad4 => keyboard.numpad4Key,
                KeyCode.Keypad5 => keyboard.numpad5Key,
                KeyCode.Keypad6 => keyboard.numpad6Key,
                KeyCode.Keypad7 => keyboard.numpad7Key,
                KeyCode.Keypad8 => keyboard.numpad8Key,
                KeyCode.Keypad9 => keyboard.numpad9Key,
                KeyCode.LeftShift => keyboard.leftShiftKey,
                KeyCode.RightShift => keyboard.rightShiftKey,
                KeyCode.LeftControl => keyboard.leftCtrlKey,
                KeyCode.RightControl => keyboard.rightCtrlKey,
                KeyCode.LeftAlt => keyboard.leftAltKey,
                KeyCode.RightAlt => keyboard.rightAltKey,
                _ => null
            };
        }
#endif

        private float ResolveMoveDuration(bool ambientMove)
        {
            var minimumDuration = Mathf.Max(0.05f, minimumVisibleMoveDurationSeconds);
            if (playbackMode == SettlementPlaybackMode.Fast && ambientMove)
            {
                return Mathf.Max(minimumDuration, fastMoveDurationSeconds);
            }

            return Mathf.Max(minimumDuration, unitMoveDurationSeconds);
        }

        private float ResolveAttackDuration()
        {
            if (playbackMode == SettlementPlaybackMode.Fast)
            {
                return Mathf.Max(0.05f, fastAttackPlaybackSeconds);
            }

            return Mathf.Max(0.05f, attackPlaybackSeconds);
        }

        private float ResolveFocusPause()
        {
            if (playbackMode == SettlementPlaybackMode.Fast)
            {
                return Mathf.Max(0.02f, fastFocusPauseSeconds);
            }

            return Mathf.Max(0.02f, actorFocusPauseSeconds);
        }

        private float ResolveStepGap()
        {
            if (playbackMode == SettlementPlaybackMode.Fast)
            {
                return 0f;
            }

            return Mathf.Max(0f, perUnitPlaybackGapSeconds);
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

        private static string ResolveFocusUnitId(TurnEventDto evt)
        {
            if (evt == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(evt.UnitId))
            {
                return evt.UnitId;
            }

            if (!string.IsNullOrWhiteSpace(evt.TargetUnitId))
            {
                return evt.TargetUnitId;
            }

            if (!string.IsNullOrWhiteSpace(evt.EnemyUnitId))
            {
                return evt.EnemyUnitId;
            }

            return ResolveActorUnitId(evt);
        }

    }
}
