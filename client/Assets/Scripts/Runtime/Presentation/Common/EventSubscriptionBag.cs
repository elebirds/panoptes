using System;
using System.Collections.Generic;

namespace Panoptes.Presentation.Common
{
    public sealed class EventSubscriptionBag
    {
        private readonly List<Action> _unsubscribers = new();

        public void Subscribe(Action subscribe, Action unsubscribe)
        {
            Clear();
            Add(subscribe, unsubscribe);
        }

        public void Add(Action subscribe, Action unsubscribe)
        {
            if (subscribe == null || unsubscribe == null)
            {
                return;
            }

            subscribe();
            _unsubscribers.Add(unsubscribe);
        }

        public void Clear()
        {
            for (var i = _unsubscribers.Count - 1; i >= 0; i--)
            {
                _unsubscribers[i]?.Invoke();
            }

            _unsubscribers.Clear();
        }
    }
}
