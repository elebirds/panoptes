/*************************************************
 * Project: Panoptes
 * File: CombatTargetingInputMode.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Planning input mode marker for combat target selection flow.
 *************************************************/

using Panoptes.Presentation.Planning.Input;

namespace Panoptes.Presentation.Planning.Input.Modes
{
    /// <summary>
    /// Base lifecycle holder for combat targeting input; targets are forwarded as intents.
    /// </summary>
    public sealed class CombatTargetingInputMode : IPlanningInputMode
    {
        public void Enter(PlanningInputContext context) { }
        public void Exit() { }
        public void Tick() { }
        public bool HandlePrimary() => false;
        public bool HandleCancel() => false;
    }
}
