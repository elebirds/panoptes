/*************************************************
 * Project: Panoptes
 * File: SettlementPlaybackController.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Plays unified turn settlement sections in server order.
 *************************************************/

using System.Collections;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Presentation.Animation;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class SettlementPlaybackController : MonoBehaviour
    {
        [SerializeField] private float moveEventWaitSeconds = 0.38f;
        [SerializeField] private float conflictFlashSeconds = 0.18f;
        [SerializeField] private float damagePulseSeconds = 0.16f;
        [SerializeField] private float damagePulseScale = 1.14f;
        [SerializeField] private float sectionPauseSeconds = 0.14f;

        private GameStateCache _cache;
        private Coroutine _playbackCoroutine;

        private void Awake()
        {
            _cache = GameStateCache.Instance;
        }

        private void OnEnable()
        {
            _cache = GameStateCache.Instance;
            if (_cache != null)
            {
                _cache.OnTurnSettled += OnTurnSettled;
            }
        }

        private void OnDisable()
        {
            if (_cache != null)
            {
                _cache.OnTurnSettled -= OnTurnSettled;
            }

            if (_playbackCoroutine != null)
            {
                StopCoroutine(_playbackCoroutine);
                _playbackCoroutine = null;
            }
        }

        private void OnTurnSettled(TurnSettledEvent evt)
        {
            if (evt?.Settlement?.Sections == null || evt.Settlement.Sections.Count == 0)
            {
                return;
            }

            if (_playbackCoroutine != null)
            {
                StopCoroutine(_playbackCoroutine);
            }

            _playbackCoroutine = StartCoroutine(PlaySettlement(evt.Settlement));
        }

        private IEnumerator PlaySettlement(TurnSettlementDto settlement)
        {
            EnsureAnimationQueue();

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
                        case "building_built":
                        case "road_built":
                            yield return PlayMapPulse(evt);
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
            var map = MapRenderer.Instance;
            if (map == null || !TryResolveNodeForEvent(evt, out var node))
            {
                yield break;
            }

            node.SetHighlight(true, new Color(1f, 0.45f, 0.2f, 1f));
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, conflictFlashSeconds));
            node.SetHighlightVisible(false);
        }

        private IEnumerator PlayDamage(TurnEventDto evt)
        {
            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetUnitView(evt.UnitId, out var unit) || unit == null)
            {
                yield break;
            }

            yield return PulseUnit(unit.transform, Mathf.Max(0.05f, damagePulseSeconds), Mathf.Max(1.02f, damagePulseScale));
        }

        private IEnumerator PlayDeath(TurnEventDto evt)
        {
            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetUnitView(evt.UnitId, out var unit) || unit == null)
            {
                yield break;
            }

            yield return PulseUnit(unit.transform, Mathf.Max(0.05f, damagePulseSeconds), Mathf.Max(1.02f, damagePulseScale));
            map.RemoveRuntimeUnit(evt.UnitId, false);
        }

        private IEnumerator PlayMapPulse(TurnEventDto evt)
        {
            if (!TryResolveNodeForEvent(evt, out var node) || node == null)
            {
                yield break;
            }

            node.SetHighlight(true, new Color(0.35f, 0.9f, 1f, 1f));
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, conflictFlashSeconds));
            node.SetHighlightVisible(false);
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

        private static bool TryResolveNodeForEvent(TurnEventDto evt, out NodeView node)
        {
            node = null;
            var map = MapRenderer.Instance;
            if (map == null || evt == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(evt.NodeId) && map.TryGetNodeView(evt.NodeId, out node))
            {
                return node != null;
            }

            if (map.TryGetNodeIdByGrid(new Vector2Int(evt.PosX, evt.PosY), out var nodeId))
            {
                return map.TryGetNodeView(nodeId, out node) && node != null;
            }

            return false;
        }

        private static void EnsureAnimationQueue()
        {
            if (AnimationQueue.Instance != null)
            {
                return;
            }

            var go = new GameObject("AnimationQueue");
            go.AddComponent<AnimationQueue>();
        }
    }
}
