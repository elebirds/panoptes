package game

import (
	"sort"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/combat"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/yohamta/donburi"
)

func (r *GameRoom) syncActiveMarchWithOrder(order domain.CombatOrder) {
	if r == nil || r.state == nil || order.UnitID == "" {
		return
	}

	if order.Action != domain.CombatActionMove || order.TargetNodeID == "" {
		delete(r.state.ActiveMarches, order.UnitID)
		return
	}

	march := domain.ActiveMarch{
		PlayerID:          order.PlayerID,
		UnitID:            order.UnitID,
		Action:            domain.CombatActionMove,
		DestinationNodeID: order.TargetNodeID,
	}
	if preview, ok := r.buildRoutePreview(order.UnitID, order.TargetNodeID); ok {
		march.LastPreview = preview
	}
	r.state.ActiveMarches[order.UnitID] = march
}

func (r *GameRoom) refreshActiveMarchesAfterSettlement() {
	if r == nil || r.state == nil {
		return
	}

	for unitID, march := range r.state.ActiveMarches {
		entry, ok := r.findUnitByID(unitID)
		if !ok {
			delete(r.state.ActiveMarches, unitID)
			continue
		}
		currentPos := ecs.PositionC.Get(entry)
		currentNodeID := r.nodeIDAt(domain.Position{X: currentPos.X, Y: currentPos.Y})
		if currentNodeID == "" {
			delete(r.state.ActiveMarches, unitID)
			continue
		}
		targetEntry, ok := r.state.GetNode(march.DestinationNodeID)
		if !ok {
			delete(r.state.ActiveMarches, unitID)
			continue
		}
		targetPos := ecs.PositionC.Get(targetEntry)
		if currentPos.X == targetPos.X && currentPos.Y == targetPos.Y {
			delete(r.state.ActiveMarches, unitID)
			continue
		}
		if preview, ok := r.advanceActiveMarchPreview(unitID, march, currentNodeID); ok {
			march.LastPreview = preview
			r.state.ActiveMarches[unitID] = march
			continue
		}
		if preview, ok := r.buildRoutePreview(unitID, march.DestinationNodeID); ok {
			march.LastPreview = preview
			r.state.ActiveMarches[unitID] = march
			continue
		}
		delete(r.state.ActiveMarches, unitID)
	}
}

func (r *GameRoom) buildRoutePreview(unitID string, destinationNodeID string) (domain.RoutePreview, bool) {
	if r == nil || r.state == nil {
		return domain.RoutePreview{}, false
	}
	planner := combat.NewWeightedRoutePlanner(combat.DefaultTerrainCostPolicy{})
	return planner.BuildPreview(r.state.World, r.state, unitID, destinationNodeID)
}

func (r *GameRoom) buildRoutePreviewFromPathNodeIDs(unitID string, pathNodeIDs []string) (domain.RoutePreview, bool) {
	if r == nil || r.state == nil {
		return domain.RoutePreview{}, false
	}
	planner := combat.NewWeightedRoutePlanner(combat.DefaultTerrainCostPolicy{})
	return planner.BuildPreviewFromPathNodeIDs(r.state.World, r.state, unitID, pathNodeIDs)
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

func (r *GameRoom) SendPlanningSnapshot(playerID string) error {
	if r == nil {
		return nil
	}
	return r.SendToPlayer(playerID, r.buildPlanningSnapshot(playerID))
}

func (r *GameRoom) buildPlanningSnapshot(playerID string) *pb.MsgPlanningSnapshot {
	msg := &pb.MsgPlanningSnapshot{
		Turn:  int32(r.Turn),
		Phase: r.Phase,
	}
	if r == nil || r.state == nil || playerID == "" {
		return msg
	}

	ordersByUnit := make(map[string]*pb.QueuedUnitOrder)
	for unitID, march := range r.state.ActiveMarches {
		if march.PlayerID != playerID {
			continue
		}
		ordersByUnit[unitID] = queuedMoveOrder(unitID, march)
	}
	for unitID, order := range r.plannedUnitOrders {
		if order.PlayerID != playerID {
			continue
		}
		queued := &pb.QueuedUnitOrder{
			UnitId:          unitID,
			Action:          string(order.Action),
			TargetNodeId:    order.TargetNodeID,
			TargetUnitId:    order.TargetUnitID,
			SecondaryNodeId: order.SecondaryNodeID,
			Params:          cloneStringMap(order.Params),
		}
		if order.Action == gameorders.ActionMove {
			if march, ok := r.state.ActiveMarches[unitID]; ok {
				queued = queuedMoveOrder(unitID, march)
			} else if preview, ok := r.buildRoutePreview(unitID, order.TargetNodeID); ok {
				queued.PathNodeIds = append(queued.PathNodeIds, preview.PathNodeIDs...)
				queued.FirstTurnNodeId = preview.FirstTurnNodeID
				queued.TotalTurns = int32(preview.TotalTurns)
				queued.TurnStops = toProtoTurnStops(preview.TurnStops)
			}
		}
		ordersByUnit[unitID] = queued
	}

	unitIDs := make([]string, 0, len(ordersByUnit))
	for unitID := range ordersByUnit {
		unitIDs = append(unitIDs, unitID)
	}
	sort.Strings(unitIDs)
	for _, unitID := range unitIDs {
		msg.UnitOrders = append(msg.UnitOrders, ordersByUnit[unitID])
	}
	if playerState := r.state.Players[playerID]; playerState != nil {
		for _, zone := range playerState.WarZones {
			msg.WarZones = append(msg.WarZones, &pb.WarZone{
				Id:         zone.ID,
				Name:       zone.Name,
				NodeIds:    append([]string(nil), zone.NodeIDs...),
				Directive:  zone.Directive,
				TargetNode: zone.Target,
			})
		}
	}
	return msg
}

func (r *GameRoom) findUnitByID(unitID string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	if r == nil || r.state == nil {
		return nil, false
	}
	ecs.AllUnits(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
		if found != nil {
			return
		}
		if ecs.UnitStatsC.Get(entry).ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}

func queuedMoveOrder(unitID string, march domain.ActiveMarch) *pb.QueuedUnitOrder {
	return &pb.QueuedUnitOrder{
		UnitId:          unitID,
		Action:          string(gameorders.ActionMove),
		TargetNodeId:    march.DestinationNodeID,
		PathNodeIds:     append([]string(nil), march.LastPreview.PathNodeIDs...),
		FirstTurnNodeId: march.LastPreview.FirstTurnNodeID,
		TotalTurns:      int32(march.LastPreview.TotalTurns),
		TurnStops:       toProtoTurnStops(march.LastPreview.TurnStops),
	}
}

func toProtoTurnStops(stops []domain.MarchTurnStop) []*pb.MarchTurnStop {
	out := make([]*pb.MarchTurnStop, 0, len(stops))
	for _, stop := range stops {
		out = append(out, &pb.MarchTurnStop{
			TurnIndex: int32(stop.TurnIndex),
			NodeId:    stop.NodeID,
		})
	}
	return out
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
