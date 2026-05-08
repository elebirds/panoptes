using System;
using System.Collections;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class SettlementPlaybackRunner
    {
        private readonly MonoBehaviour _owner;
        private readonly List<Coroutine> _activeBatchCoroutines = new();

        public SettlementPlaybackRunner(MonoBehaviour owner)
        {
            _owner = owner;
        }

        public IEnumerator PlayWindow(
            SettlementPlaybackWindow window,
            bool allowCameraFocus,
            Func<TurnEventDto, IEnumerator> playBatchMove,
            Func<SettlementPlaybackStep, bool, IEnumerator> playFocusedStep,
            Action clearSelection)
        {
            if (window == null || window.Steps == null || window.Steps.Count == 0)
            {
                yield break;
            }

            if (window.Kind == SettlementPlaybackWindowKind.MoveBatch)
            {
                yield return PlayMoveBatch(window.Steps, playBatchMove, clearSelection);
                yield break;
            }

            for (var i = 0; i < window.Steps.Count; i++)
            {
                yield return playFocusedStep(window.Steps[i], allowCameraFocus);
            }
        }

        public void StopActive()
        {
            if (_owner == null)
            {
                _activeBatchCoroutines.Clear();
                return;
            }

            for (var i = 0; i < _activeBatchCoroutines.Count; i++)
            {
                if (_activeBatchCoroutines[i] != null)
                {
                    _owner.StopCoroutine(_activeBatchCoroutines[i]);
                }
            }

            _activeBatchCoroutines.Clear();
        }

        private IEnumerator PlayMoveBatch(
            IReadOnlyList<SettlementPlaybackStep> steps,
            Func<TurnEventDto, IEnumerator> playBatchMove,
            Action clearSelection)
        {
            if (_owner == null || steps == null || steps.Count == 0)
            {
                yield break;
            }

            var remaining = 0;
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step?.MoveEvent == null)
                {
                    continue;
                }

                remaining++;
                _activeBatchCoroutines.Add(_owner.StartCoroutine(PlayBatchItem(step.MoveEvent, playBatchMove, () => remaining--)));
            }

            while (remaining > 0)
            {
                yield return null;
            }

            _activeBatchCoroutines.Clear();
            clearSelection?.Invoke();
        }

        private static IEnumerator PlayBatchItem(
            TurnEventDto evt,
            Func<TurnEventDto, IEnumerator> playBatchMove,
            Action onComplete)
        {
            try
            {
                yield return playBatchMove(evt);
            }
            finally
            {
                onComplete?.Invoke();
            }
        }
    }
}
