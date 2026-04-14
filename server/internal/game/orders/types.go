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

func (o UnitOrder) IsMapAction() bool {
	switch o.Action {
	case ActionSettleCity, ActionBuildRoad, ActionRepairRoad, ActionBuildImprovement, ActionRepairImprovement:
		return true
	default:
		return false
	}
}

func (o UnitOrder) IsUnitResolutionAction() bool {
	switch o.Action {
	case ActionMove, ActionAttack, ActionHold, ActionCharge:
		return true
	default:
		return false
	}
}

func (o UnitOrder) ToDirective() domain.UnitDirective {
	return domain.UnitDirective{
		PlayerID:        o.PlayerID,
		UnitID:          o.UnitID,
		Action:          string(o.Action),
		TargetNodeID:    o.TargetNodeID,
		TargetUnitID:    o.TargetUnitID,
		SecondaryNodeID: o.SecondaryNodeID,
		Params:          cloneStringMap(o.Params),
		PathNodeIDs:     append([]string(nil), o.PathNodeIDs...),
	}
}

func FromDirective(directive domain.UnitDirective) UnitOrder {
	return UnitOrder{
		PlayerID:        directive.PlayerID,
		UnitID:          directive.UnitID,
		Action:          UnitAction(directive.Action),
		TargetNodeID:    directive.TargetNodeID,
		TargetUnitID:    directive.TargetUnitID,
		SecondaryNodeID: directive.SecondaryNodeID,
		Params:          cloneStringMap(directive.Params),
		PathNodeIDs:     append([]string(nil), directive.PathNodeIDs...),
	}
}

func (o UnitOrder) ToResolutionOrder() (domain.UnitResolutionOrder, bool) {
	if !o.IsUnitResolutionAction() {
		return domain.UnitResolutionOrder{}, false
	}
	return domain.UnitResolutionOrder{
		PlayerID:     o.PlayerID,
		UnitID:       o.UnitID,
		Action:       domain.UnitResolutionAction(o.Action),
		TargetNodeID: o.TargetNodeID,
		TargetUnitID: o.TargetUnitID,
		PathNodeIDs:  append([]string(nil), o.PathNodeIDs...),
	}, true
}

func cloneStringMap(src map[string]string) map[string]string {
	if len(src) == 0 {
		return nil
	}
	dst := make(map[string]string, len(src))
	for k, v := range src {
		dst[k] = v
	}
	return dst
}
