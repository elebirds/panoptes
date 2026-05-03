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
using R3;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.Map
{
    public sealed class SettlementPlaybackController : MonoBehaviour
    {
        [SerializeField] private float moveEventWaitSeconds = 0.38f;
        [SerializeField] private float conflictFlashSeconds = 0.18f;
        [SerializeField] private float damagePulseSeconds = 0.16f;
        [SerializeField] private float damagePulseScale = 1.14f;
        [SerializeField] private float sectionPauseSeconds = 0.14f;
        [SerializeField] private bool enableDamagePopups = true;
        [SerializeField] private DamageNumberPopupController damagePopupController;

        private SettlementStore _settlementStore;
        private MapRenderer _mapRenderer;
        private IDisposable _settlementSubscription;
        private Coroutine _playbackCoroutine;
        private int _lastHandledSettlementSequence;

        [Inject]
        private void Construct(SettlementStore settlementStore, MapRenderer mapRenderer)
        {
            _settlementStore = settlementStore;
            _mapRenderer = mapRenderer;
            if (isActiveAndEnabled)
            {
                SubscribeSettlement();
            }
        }

        private void Awake()
        {
            EnsureDamagePopupController();
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
            }

            _playbackCoroutine = StartCoroutine(PlaySettlement(settlement));
        }

        private IEnumerator PlaySettlement(TurnSettlementDto settlement)
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
                    var evt = section.Events[eventIndex];
                    if (evt == null)
                    {
                        continue;
                    }

                    switch (evt.Type)
                    {
                        case "unit_moved":
                            yield return PlayMove(evt);
                            break;
                        case "conflict":
                            yield return PlayConflict(evt);
                            break;
                        case "unit_damaged":
                            yield return PlayDamage(evt);
                            break;
                        case "unit_died":
                            yield return PlayDeath(evt);
                            break;
                        case "city_founded":
                        case "building_built":
                        case "building_status_changed":
                        case "facility_takeover_progressed":
                        case "facility_takeover_completed":
                        case "building_ruined":
                        case "road_built":
                            yield return PlayMapPulse(evt);
                            break;
                        case "building_damaged":
                        case "city_core_damaged":
                        case "city_core_destroyed":
                            yield return PlayBuildingDamage(evt);
                            break;
                        case "recipe_progressed":
                        case "recipe_skipped":
                        case "recipe_completed":
                        case "technology_completed":
                        case "technology_activated":
                        case "technology_grant_applied":
                        case "national_policy_changed":
                        case "institution_loadout_activated":
                            yield return PlayAuthorityCue(evt);
                            break;
                        default:
                            yield return null;
                            break;
                    }
                }

                if (sectionIndex < settlement.Sections.Count - 1)
                {
                    yield return new WaitForSecondsRealtime(Mathf.Max(0.02f, sectionPauseSeconds));
                }
            }

            _playbackCoroutine = null;
        }

        private IEnumerator PlayMove(TurnEventDto evt)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, moveEventWaitSeconds));
        }

        private IEnumerator PlayConflict(TurnEventDto evt)
        {
            if (_mapRenderer == null || !TryResolveNodeForEvent(evt, out var node))
            {
                yield break;
            }

            TryPlayUnitAttackAnimation(evt.UnitId);
            TryPlayUnitAttackAnimation(!string.IsNullOrWhiteSpace(evt.TargetUnitId) ? evt.TargetUnitId : evt.EnemyUnitId);
            node.SetHighlight(true, new Color(1f, 0.45f, 0.2f, 1f));
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, conflictFlashSeconds));
            node.SetHighlightVisible(false);
        }

        private IEnumerator PlayDamage(TurnEventDto evt)
        {
            if (_mapRenderer == null || !_mapRenderer.TryGetUnitView(evt.UnitId, out var unit) || unit == null)
            {
                TryPlayAttackerAnimation(evt);
                TryShowUnitDamagePopupFallback(evt);
                yield break;
            }

            TryPlayAttackerAnimation(evt);
            TryShowDamagePopup(unit.transform, evt, isBuilding: false);
            yield return PulseUnit(unit.transform, Mathf.Max(0.05f, damagePulseSeconds), Mathf.Max(1.02f, damagePulseScale));
        }

        private IEnumerator PlayDeath(TurnEventDto evt)
        {
            if (_mapRenderer == null || !_mapRenderer.TryGetUnitView(evt.UnitId, out var unit) || unit == null)
            {
                TryPlayUnitAttackAnimation(evt.KillerId);
                TryShowUnitDamagePopupFallback(evt);
                yield break;
            }

            TryPlayUnitAttackAnimation(evt.KillerId);
            TryShowDamagePopup(unit.transform, evt, isBuilding: false);
            yield return PulseUnit(unit.transform, Mathf.Max(0.05f, damagePulseSeconds), Mathf.Max(1.02f, damagePulseScale));
            _mapRenderer.RemoveRuntimeUnit(evt.UnitId, false);
        }

        private IEnumerator PlayBuildingDamage(TurnEventDto evt)
        {
            if (!TryResolveNodeForEvent(evt, out var node) || node == null)
            {
                yield break;
            }

            var popupTarget = node.BuildingInstance != null ? node.BuildingInstance.transform : node.transform;
            TryPlayAttackerAnimation(evt);
            TryShowDamagePopup(popupTarget, evt, isBuilding: true);

            node.SetHighlight(true, new Color(0.35f, 0.9f, 1f, 1f));
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, conflictFlashSeconds));
            node.SetHighlightVisible(false);
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

            EnsureDamagePopupController();
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

            EnsureDamagePopupController();
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

        private void EnsureDamagePopupController()
        {
            if (!enableDamagePopups || damagePopupController != null)
            {
                return;
            }

            damagePopupController = FindAnyObjectByType<DamageNumberPopupController>();
            if (damagePopupController != null)
            {
                return;
            }

            var popupRoot = new GameObject("DamageNumberPopupController");
            damagePopupController = popupRoot.AddComponent<DamageNumberPopupController>();
        }
    }
}
