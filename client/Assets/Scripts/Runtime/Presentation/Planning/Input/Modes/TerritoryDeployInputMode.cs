/*************************************************
 * Project: Panoptes
 * File: TerritoryDeployInputMode.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Planning input mode marker for territory deploy requests.
 *************************************************/

using Panoptes.Presentation.Planning.Input;

namespace Panoptes.Presentation.Planning.Input.Modes
{
    /// <summary>
    /// Base lifecycle holder for deploy input; deploy legality remains on the server.
    /// </summary>
    public sealed class TerritoryDeployInputMode : IPlanningInputMode
    {
        public void Enter(PlanningInputContext context) { }
        public void Exit() { }
        public void Tick() { }
        public bool HandlePrimary() => false;
        public bool HandleCancel() => false;
    }
}
