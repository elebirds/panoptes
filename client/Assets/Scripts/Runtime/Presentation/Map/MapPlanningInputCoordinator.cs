namespace Panoptes.Presentation.Map
{
    public interface IMapPlanningInputCoordinatorContext
    {
        bool IsBuildModeActive { get; }
        MapPlanningInputController.CombatActionMode CombatActionMode { get; }

        void UpdateBuildMode(bool leftMouseDown, bool rightMouseDown);
        void UpdateCombatMode();
        bool IsPointerOverUI();
        void PrepareMapCommandClick();
        bool TryIssueAttackStructureFromCurrentClick();
        void HandleCombatSelectionClick();
        void HandleMoveSelectionClick();
        bool ShouldPrioritizeStructureAttackClick();
        bool TrySelectOwnedUnitFromNodeClick();
        bool TryOpenBuildingInfoFromClick();
        void HandleCombatCancel();
    }

    public sealed class MapPlanningInputCoordinator
    {
        public void Tick(
            IMapPlanningInputCoordinatorContext context,
            bool leftMouseDown,
            bool rightMouseDown)
        {
            if (context == null)
            {
                return;
            }

            if (context.IsBuildModeActive)
            {
                context.UpdateBuildMode(leftMouseDown, rightMouseDown);
                return;
            }

            context.UpdateCombatMode();

            if (leftMouseDown)
            {
                HandleLeftClick(context);
            }

            if (rightMouseDown)
            {
                context.HandleCombatCancel();
            }
        }

        public void HandleLeftClick(IMapPlanningInputCoordinatorContext context)
        {
            if (context == null || context.IsPointerOverUI())
            {
                return;
            }

            if (context.CombatActionMode == MapPlanningInputController.CombatActionMode.Attack)
            {
                context.PrepareMapCommandClick();
                if (!context.TryIssueAttackStructureFromCurrentClick())
                {
                    context.HandleCombatSelectionClick();
                }

                return;
            }

            if (context.CombatActionMode == MapPlanningInputController.CombatActionMode.Move)
            {
                context.PrepareMapCommandClick();
                context.HandleMoveSelectionClick();
                return;
            }

            if (context.ShouldPrioritizeStructureAttackClick())
            {
                context.PrepareMapCommandClick();
                context.HandleCombatSelectionClick();
                return;
            }

            if (context.TrySelectOwnedUnitFromNodeClick())
            {
                return;
            }

            if (context.TryOpenBuildingInfoFromClick())
            {
                return;
            }

            context.PrepareMapCommandClick();
            context.HandleCombatSelectionClick();
        }
    }
}
