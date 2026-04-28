/*************************************************
 * Project: Panoptes
 * File: PlanningInputContext.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Immutable planning input context shared by modes without owning gameplay state.
 *************************************************/

using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.Map;

namespace Panoptes.Presentation.Planning.Input
{
    /// <summary>
    /// Provides read-only access to presentation caches and the current selected unit for input modes.
    /// </summary>
    public readonly struct PlanningInputContext
    {
        public PlanningInputContext(GameStateCache gameStateCache, PlanningDraftCache draftCache, UnitView selectedUnit)
        {
            GameStateCache = gameStateCache;
            DraftCache = draftCache;
            SelectedUnit = selectedUnit;
        }

        public GameStateCache GameStateCache { get; }
        public PlanningDraftCache DraftCache { get; }
        public UnitView SelectedUnit { get; }
    }
}
