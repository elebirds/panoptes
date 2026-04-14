package orders

import "github.com/elebirds/panoptes/internal/domain"

type UnitAction string

const (
	ActionMove              UnitAction = "move"
	ActionAttack            UnitAction = "attack"
	ActionHold              UnitAction = "hold"
	ActionCharge            UnitAction = "charge"
	ActionSettleCity        UnitAction = "settle_city"
	ActionBuildRoad         UnitAction = "build_road"
	ActionRepairRoad        UnitAction = "repair_road"
	ActionBuildImprovement  UnitAction = "build_improvement"
	ActionRepairImprovement UnitAction = "repair_improvement"
)

type UnitOrder struct {
	PlayerID        string
	UnitID          string
	Action          UnitAction
	TargetNodeID    string
	TargetUnitID    string
	SecondaryNodeID string
	Params          map[string]string
	PathNodeIDs     []string
}

func (o UnitOrder) IsCombatAction() bool {
	switch o.Action {
	case ActionMove, ActionAttack, ActionHold, ActionCharge:
		return true
	default:
		return false
	}
}

func (o UnitOrder) IsMapAction() bool {
	switch o.Action {
	case ActionSettleCity, ActionBuildRoad, ActionRepairRoad, ActionBuildImprovement, ActionRepairImprovement:
		return true
	default:
		return false
	}
}

func (o UnitOrder) ToCombatOrder() (domain.CombatOrder, bool) {
	if !o.IsCombatAction() {
		return domain.CombatOrder{}, false
	}
	return domain.CombatOrder{
		PlayerID:     o.PlayerID,
		UnitID:       o.UnitID,
		Action:       domain.CombatAction(o.Action),
		TargetNodeID: o.TargetNodeID,
		TargetUnitID: o.TargetUnitID,
		PathNodeIDs:  append([]string(nil), o.PathNodeIDs...),
	}, true
}
