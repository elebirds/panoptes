/*************************************************
 * Project: Panoptes
 * File: PlanningInputCoordinator.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Lightweight coordinator for active planning input mode lifecycle.
 *************************************************/

namespace Panoptes.Presentation.Planning.Input
{
    /// <summary>
    /// Owns the active planning input mode lifecycle without reading authoritative gameplay state.
    /// </summary>
    public sealed class PlanningInputCoordinator
    {
        private IPlanningInputMode _activeMode;

        public IPlanningInputMode ActiveMode => _activeMode;

        public void Enter(IPlanningInputMode mode)
        {
            if (ReferenceEquals(_activeMode, mode))
            {
                return;
            }

            _activeMode?.Exit();
            _activeMode = mode;
            _activeMode?.Enter();
        }

        public void Tick()
        {
            _activeMode?.Tick();
        }

        public bool HandlePrimary()
        {
            return _activeMode != null && _activeMode.HandlePrimary();
        }

        public bool HandleCancel()
        {
            return _activeMode != null && _activeMode.HandleCancel();
        }

        public void Clear()
        {
            _activeMode?.Exit();
            _activeMode = null;
        }
    }
}
