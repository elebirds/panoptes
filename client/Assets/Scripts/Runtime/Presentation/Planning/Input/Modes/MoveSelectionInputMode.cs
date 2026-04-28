/*************************************************
 * Project: Panoptes
 * File: MoveSelectionInputMode.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Planning input mode marker for move selection flow.
 *************************************************/

using Panoptes.Presentation.Planning.Input;

namespace Panoptes.Presentation.Planning.Input.Modes
{
    /// <summary>
    /// Base lifecycle holder for move selection input; command dispatch remains server-authoritative.
    /// </summary>
    public sealed class MoveSelectionInputMode : IPlanningInputMode
    {
        public void Enter(PlanningInputContext context) { }
        public void Exit() { }
        public void Tick() { }
        public bool HandlePrimary() => false;
        public bool HandleCancel() => false;
    }
}
