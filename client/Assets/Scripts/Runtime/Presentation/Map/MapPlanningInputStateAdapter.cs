using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.ViewModels;

namespace Panoptes.Presentation.Map
{
    public sealed class MapPlanningInputStateAdapter
    {
        private PlanningToolService _planningToolService;
        private PlanningToolViewModel _planningToolViewModel;
        private SelectionService _selectionService;
        private bool _legacyBuildModeActive;

        public MapPlanningInputController.CombatActionMode CombatActionMode { get; private set; }

        public string CurrentPrompt
        {
            get
            {
                if (_planningToolViewModel != null)
                {
                    return _planningToolViewModel.Current.Prompt;
                }

                return CombatActionMode switch
                {
                    MapPlanningInputController.CombatActionMode.Move => "悬停节点预览路径，点击后下达移动指令。",
                    MapPlanningInputController.CombatActionMode.Attack => "点击敌方单位或敌方建筑下达攻击指令。",
                    MapPlanningInputController.CombatActionMode.Charge => "点击敌方单位下达冲锋指令。",
                    _ => "选择动作后再指定目标"
                };
            }
        }

        public void Configure(
            PlanningToolService planningToolService,
            SelectionService selectionService,
            PlanningToolViewModel planningToolViewModel)
        {
            _planningToolService = planningToolService;
            _selectionService = selectionService;
            _planningToolViewModel = planningToolViewModel;
        }

        public void ClearToolAndSelection()
        {
            CombatActionMode = MapPlanningInputController.CombatActionMode.None;
            _legacyBuildModeActive = false;
            if (_planningToolService?.IsDisposed == false)
            {
                _planningToolService.ClearTool();
            }

            if (_selectionService?.IsDisposed == false)
            {
                _selectionService.Clear();
            }
        }

        public void SetCombatActionMode(
            MapPlanningInputController.CombatActionMode mode,
            bool publishToolState = true)
        {
            CombatActionMode = mode;
            if (!publishToolState)
            {
                return;
            }

            switch (mode)
            {
                case MapPlanningInputController.CombatActionMode.Move:
                    if (_planningToolService?.IsDisposed == false)
                    {
                        _planningToolService.BeginMove();
                    }
                    break;
                case MapPlanningInputController.CombatActionMode.Attack:
                    if (_planningToolService?.IsDisposed == false)
                    {
                        _planningToolService.BeginAttack();
                    }
                    break;
                case MapPlanningInputController.CombatActionMode.Charge:
                    if (_planningToolService?.IsDisposed == false)
                    {
                        _planningToolService.BeginCharge();
                    }
                    break;
                default:
                    if (_planningToolService?.IsDisposed == false)
                    {
                        _planningToolService.ClearTool();
                    }
                    break;
            }
        }

        public void SetBuildModeActive(
            bool active,
            string buildType,
            string activeBuildCityId,
            MapPlanningInputController.BuildPlacementRule buildRule)
        {
            _legacyBuildModeActive = active;
            if (active)
            {
                if (_planningToolService?.IsDisposed == false)
                {
                    _planningToolService.EnterBuild(
                        buildType,
                        activeBuildCityId,
                        ConvertBuildRule(buildRule));
                }
                return;
            }

            if (_planningToolService?.IsDisposed == false)
            {
                _planningToolService.ClearTool();
            }
        }

        public bool IsBuildModeActive()
        {
            if (_planningToolViewModel != null)
            {
                return _planningToolViewModel.Current.Mode == PlanningToolMode.Build;
            }

            return _legacyBuildModeActive;
        }

        public void SetMovePreviewTarget(string nodeId)
        {
            if (_planningToolService?.IsDisposed == false)
            {
                _planningToolService.SetMovePreviewTarget(nodeId);
            }
        }

        public void ClearMovePreviewTarget()
        {
            if (_planningToolService?.IsDisposed == false)
            {
                _planningToolService.ClearMovePreviewTarget();
            }
        }

        public void SetBuildPreviewTarget(string nodeId)
        {
            if (_planningToolService?.IsDisposed == false)
            {
                _planningToolService.SetBuildPreviewTarget(nodeId);
            }
        }

        public void ClearBuildPreviewTarget()
        {
            if (_planningToolService?.IsDisposed == false)
            {
                _planningToolService.ClearBuildPreviewTarget();
            }
        }

        public void PublishSelectedUnit(string unitId)
        {
            if (_selectionService?.IsDisposed != false)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(unitId))
            {
                _selectionService.Clear();
                return;
            }

            _selectionService.SelectUnit(unitId);
        }

        public static PlanningBuildPlacementRule ConvertBuildRule(MapPlanningInputController.BuildPlacementRule rule)
        {
            return rule switch
            {
                MapPlanningInputController.BuildPlacementRule.ResourceOnly => PlanningBuildPlacementRule.ResourceOnly,
                MapPlanningInputController.BuildPlacementRule.CityOnly => PlanningBuildPlacementRule.CityOnly,
                _ => PlanningBuildPlacementRule.AnyTerrain
            };
        }
    }
}
