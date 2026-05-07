using NUnit.Framework;
using Panoptes.Presentation.Map;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapPlanningInputCoordinatorTests
    {
        [Test]
        public void Tick_ShouldUpdateBuildModeAndSkipClickRouting()
        {
            var coordinator = new MapPlanningInputCoordinator();
            var context = new FakeContext { IsBuildModeActive = true };

            coordinator.Tick(context, leftMouseDown: true, rightMouseDown: true);

            Assert.That(context.UpdateBuildModeCalls, Is.EqualTo(1));
            Assert.That(context.UpdateCombatModeCalls, Is.Zero);
            Assert.That(context.HandleCombatCancelCalls, Is.Zero);
        }

        [Test]
        public void HandleLeftClick_AttackMode_ShouldPrioritizeStructureAttack()
        {
            var coordinator = new MapPlanningInputCoordinator();
            var context = new FakeContext
            {
                CombatActionMode = MapPlanningInputController.CombatActionMode.Attack,
                IssueAttackStructureResult = true
            };

            coordinator.HandleLeftClick(context);

            Assert.That(context.PrepareMapCommandClickCalls, Is.EqualTo(1));
            Assert.That(context.TryIssueAttackStructureCalls, Is.EqualTo(1));
            Assert.That(context.HandleCombatSelectionClickCalls, Is.Zero);
        }

        [Test]
        public void HandleLeftClick_MoveMode_ShouldRouteToMoveSelection()
        {
            var coordinator = new MapPlanningInputCoordinator();
            var context = new FakeContext
            {
                CombatActionMode = MapPlanningInputController.CombatActionMode.Move
            };

            coordinator.HandleLeftClick(context);

            Assert.That(context.PrepareMapCommandClickCalls, Is.EqualTo(1));
            Assert.That(context.HandleMoveSelectionClickCalls, Is.EqualTo(1));
        }

        [Test]
        public void HandleLeftClick_ChargeMode_ShouldRouteToCombatSelection()
        {
            var coordinator = new MapPlanningInputCoordinator();
            var context = new FakeContext
            {
                CombatActionMode = MapPlanningInputController.CombatActionMode.Charge
            };

            coordinator.HandleLeftClick(context);

            Assert.That(context.PrepareMapCommandClickCalls, Is.EqualTo(1));
            Assert.That(context.HandleCombatSelectionClickCalls, Is.EqualTo(1));
            Assert.That(context.TrySelectOwnedUnitCalls, Is.Zero);
            Assert.That(context.TryOpenBuildingInfoCalls, Is.Zero);
        }

        [Test]
        public void HandleLeftClick_DefaultMode_ShouldSelectUnitBeforeOpeningPanels()
        {
            var coordinator = new MapPlanningInputCoordinator();
            var context = new FakeContext { SelectOwnedUnitResult = true };

            coordinator.HandleLeftClick(context);

            Assert.That(context.TrySelectOwnedUnitCalls, Is.EqualTo(1));
            Assert.That(context.TryOpenBuildingInfoCalls, Is.Zero);
            Assert.That(context.HandleCombatSelectionClickCalls, Is.Zero);
        }

        [Test]
        public void Tick_ShouldRouteRightClickToCancel()
        {
            var coordinator = new MapPlanningInputCoordinator();
            var context = new FakeContext();

            coordinator.Tick(context, leftMouseDown: false, rightMouseDown: true);

            Assert.That(context.UpdateCombatModeCalls, Is.EqualTo(1));
            Assert.That(context.HandleCombatCancelCalls, Is.EqualTo(1));
        }

        private sealed class FakeContext : IMapPlanningInputCoordinatorContext
        {
            public bool IsBuildModeActive { get; set; }
            public MapPlanningInputController.CombatActionMode CombatActionMode { get; set; }
            public bool PointerOverUI { get; set; }
            public bool IssueAttackStructureResult { get; set; }
            public bool ShouldPrioritizeStructureAttackResult { get; set; }
            public bool SelectOwnedUnitResult { get; set; }
            public bool OpenBuildingInfoResult { get; set; }

            public int UpdateBuildModeCalls { get; private set; }
            public int UpdateCombatModeCalls { get; private set; }
            public int PrepareMapCommandClickCalls { get; private set; }
            public int TryIssueAttackStructureCalls { get; private set; }
            public int HandleCombatSelectionClickCalls { get; private set; }
            public int HandleMoveSelectionClickCalls { get; private set; }
            public int ShouldPrioritizeStructureAttackCalls { get; private set; }
            public int TrySelectOwnedUnitCalls { get; private set; }
            public int TryOpenBuildingInfoCalls { get; private set; }
            public int HandleCombatCancelCalls { get; private set; }

            public void UpdateBuildMode(bool leftMouseDown, bool rightMouseDown)
            {
                UpdateBuildModeCalls++;
            }

            public void UpdateCombatMode()
            {
                UpdateCombatModeCalls++;
            }

            public bool IsPointerOverUI()
            {
                return PointerOverUI;
            }

            public void PrepareMapCommandClick()
            {
                PrepareMapCommandClickCalls++;
            }

            public bool TryIssueAttackStructureFromCurrentClick()
            {
                TryIssueAttackStructureCalls++;
                return IssueAttackStructureResult;
            }

            public void HandleCombatSelectionClick()
            {
                HandleCombatSelectionClickCalls++;
            }

            public void HandleMoveSelectionClick()
            {
                HandleMoveSelectionClickCalls++;
            }

            public bool ShouldPrioritizeStructureAttackClick()
            {
                ShouldPrioritizeStructureAttackCalls++;
                return ShouldPrioritizeStructureAttackResult;
            }

            public bool TrySelectOwnedUnitFromNodeClick()
            {
                TrySelectOwnedUnitCalls++;
                return SelectOwnedUnitResult;
            }

            public bool TryOpenBuildingInfoFromClick()
            {
                TryOpenBuildingInfoCalls++;
                return OpenBuildingInfoResult;
            }

            public void HandleCombatCancel()
            {
                HandleCombatCancelCalls++;
            }
        }
    }
}
