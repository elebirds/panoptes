package game

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/combat"
	"github.com/yohamta/donburi"
)

func (r *GameRoom) syncActiveMarchWithOrder(order domain.UnitResolutionOrder) {
	state := r.State()
	if r == nil || state == nil || order.UnitID == "" {
		return
	}

	if order.Action != domain.UnitResolutionActionMove || order.TargetNodeID == "" {
		delete(state.TurnRuntime.Resolving.ActiveMarches, order.UnitID)
		return
	}

	march := domain.ActiveMarch{
		PlayerID:          order.PlayerID,
		UnitID:            order.UnitID,
		Action:            domain.UnitResolutionActionMove,
		DestinationNodeID: order.TargetNodeID,
	}
	if preview, ok := r.buildRoutePreview(order.UnitID, order.TargetNodeID); ok {
		march.LastPreview = preview
	}
	state.TurnRuntime.Resolving.ActiveMarches[order.UnitID] = march
}

func (r *GameRoom) refreshActiveMarchesAfterSettlement() {
	state := r.State()
	if r == nil || state == nil {
		return
	}

	for unitID, march := range state.TurnRuntime.Resolving.ActiveMarches {
		entry, ok := r.findUnitByID(unitID)
		if !ok {
			delete(state.TurnRuntime.Resolving.ActiveMarches, unitID)
			continue
		}
		currentPos := ecs.PositionC.Get(entry)
		currentNodeID := r.nodeIDAt(domain.Position{X: currentPos.X, Y: currentPos.Y})
		if currentNodeID == "" {
			delete(state.TurnRuntime.Resolving.ActiveMarches, unitID)
			continue
		}
		targetEntry, ok := state.GetNode(march.DestinationNodeID)
		if !ok {
			delete(state.TurnRuntime.Resolving.ActiveMarches, unitID)
			continue
		}
		targetPos := ecs.PositionC.Get(targetEntry)
		if currentPos.X == targetPos.X && currentPos.Y == targetPos.Y {
			delete(state.TurnRuntime.Resolving.ActiveMarches, unitID)
			continue
		}
		if preview, ok := r.advanceActiveMarchPreview(unitID, march, currentNodeID); ok {
			march.LastPreview = preview
			state.TurnRuntime.Resolving.ActiveMarches[unitID] = march
			continue
		}
		if preview, ok := r.buildRoutePreview(unitID, march.DestinationNodeID); ok {
			march.LastPreview = preview
			state.TurnRuntime.Resolving.ActiveMarches[unitID] = march
			continue
		}
		delete(state.TurnRuntime.Resolving.ActiveMarches, unitID)
	}
}

func (r *GameRoom) buildRoutePreview(unitID string, destinationNodeID string) (domain.RoutePreview, bool) {
	state := r.State()
	if r == nil || state == nil {
		return domain.RoutePreview{}, false
	}
	planner := combat.NewWeightedRoutePlanner(combat.DefaultTerrainCostPolicy{})
	return planner.BuildPreview(state.World, state, unitID, destinationNodeID)
}

func (r *GameRoom) buildRoutePreviewFromPathNodeIDs(unitID string, pathNodeIDs []string) (domain.RoutePreview, bool) {
	state := r.State()
	if r == nil || state == nil {
		return domain.RoutePreview{}, false
	}
	planner := combat.NewWeightedRoutePlanner(combat.DefaultTerrainCostPolicy{})
	return planner.BuildPreviewFromPathNodeIDs(state.World, state, unitID, pathNodeIDs)
}

func (r *GameRoom) advanceActiveMarchPreview(unitID string, march domain.ActiveMarch, currentNodeID string) (domain.RoutePreview, bool) {
	remainingPathNodeIDs, ok := trimMarchPathFromCurrentNode(march.LastPreview.PathNodeIDs, currentNodeID, march.DestinationNodeID)
	if !ok {
		return domain.RoutePreview{}, false
	}
	return r.buildRoutePreviewFromPathNodeIDs(unitID, remainingPathNodeIDs)
}

func trimMarchPathFromCurrentNode(pathNodeIDs []string, currentNodeID string, destinationNodeID string) ([]string, bool) {
	if len(pathNodeIDs) == 0 || currentNodeID == "" || destinationNodeID == "" {
		return nil, false
	}

	currentIdx := -1
	destinationSeen := false
	for i, nodeID := range pathNodeIDs {
		if currentIdx < 0 && nodeID == currentNodeID {
			currentIdx = i
		}
		if currentIdx >= 0 && nodeID == destinationNodeID {
			destinationSeen = true
			break
		}
	}
	if currentIdx < 0 || !destinationSeen {
		return nil, false
	}

	remaining := append([]string(nil), pathNodeIDs[currentIdx:]...)
	return remaining, len(remaining) > 0
}

func (r *GameRoom) findUnitByID(unitID string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	state := r.State()
	if r == nil || state == nil {
		return nil, false
	}
	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if found != nil {
			return
		}
		if ecs.UnitStatsC.Get(entry).ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}
