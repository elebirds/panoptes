/*************************************************
 * Project: Panoptes
 * File: TerritoryDeployInputMode.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Planning input mode marker for territory deploy requests.
 *************************************************/

using Panoptes.Presentation.Planning.Input;
using Panoptes.Presentation.Planning.Feedback;
using System;

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

        public static bool IsTerritoryExpansionUnitType(string unitType, string[] territoryExpansionUnitTypes)
        {
            if (string.IsNullOrWhiteSpace(unitType) ||
                territoryExpansionUnitTypes == null ||
                territoryExpansionUnitTypes.Length == 0)
            {
                return false;
            }

            var normalized = MapInputTokens.Normalize(unitType);
            for (var i = 0; i < territoryExpansionUnitTypes.Length; i++)
            {
                if (string.Equals(normalized, MapInputTokens.Normalize(territoryExpansionUnitTypes[i]), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
