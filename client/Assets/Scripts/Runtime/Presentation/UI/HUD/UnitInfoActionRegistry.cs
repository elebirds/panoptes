using System;
using System.Collections.Generic;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    /// <summary>
    /// Central registry for unit info panel action buttons.
    /// Other scripts register button handlers here.
    /// </summary>
    public sealed class UnitInfoActionRegistry : MonoBehaviour
    {
        public event Action ActionRegistryChanged;

        private sealed class ActionRegistration
        {
            public string Label;
            public Action<UnitView> Handler;
            public Func<UnitView, bool> VisiblePredicate;
        }

        private readonly Dictionary<string, ActionRegistration> _actions =
            new Dictionary<string, ActionRegistration>(StringComparer.OrdinalIgnoreCase);

        public void RegisterAction(
            string actionId,
            Action<UnitView> handler,
            string label = null,
            Func<UnitView, bool> visiblePredicate = null)
        {
            if (string.IsNullOrWhiteSpace(actionId) || handler == null)
            {
                return;
            }

            var key = actionId.Trim();
            if (!_actions.TryGetValue(key, out var registration))
            {
                registration = new ActionRegistration();
                _actions[key] = registration;
            }

            registration.Handler = handler;
            registration.Label = string.IsNullOrWhiteSpace(label) ? key : label.Trim();
            registration.VisiblePredicate = visiblePredicate;
            ActionRegistryChanged?.Invoke();
        }

        public bool TryResolve(
            string actionId,
            UnitView unit,
            out Action<UnitView> handler,
            out string label,
            out bool isVisible)
        {
            handler = null;
            label = string.Empty;
            isVisible = false;

            if (string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            if (!_actions.TryGetValue(actionId.Trim(), out var registration) || registration == null)
            {
                return false;
            }

            handler = registration.Handler;
            label = registration.Label ?? actionId.Trim();
            isVisible = registration.VisiblePredicate == null || registration.VisiblePredicate(unit);
            return true;
        }
    }
}
