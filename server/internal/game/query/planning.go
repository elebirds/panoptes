// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局查询模块的规划快照与输入逻辑。

package query

import (
	"sort"

	"github.com/elebirds/panoptes/internal/domain"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func BuildPlanningSnapshot(state *domain.GameState, playerID string) *pb.MsgPlanningSnapshot {
	msg := &pb.MsgPlanningSnapshot{}
	if state == nil {
		return msg
	}

	msg.Turn = int32(state.Turn)
	msg.Phase = state.Phase

	if playerID == "" {
		return msg
	}

	ordersByUnit := make(map[string]*pb.QueuedUnitOrder)
	for unitID, march := range state.TurnRuntime.Resolving.ActiveMarches {
		if march.PlayerID != playerID {
			continue
		}
		ordersByUnit[unitID] = queuedMoveOrder(unitID, march)
	}
	for unitID, directive := range state.TurnRuntime.Planning.UnitOrders {
		if directive.PlayerID != playerID {
			continue
		}
		queued := &pb.QueuedUnitOrder{
			UnitId:          unitID,
			Action:          directive.Action,
			TargetNodeId:    directive.TargetNodeID,
			TargetUnitId:    directive.TargetUnitID,
			SecondaryNodeId: directive.SecondaryNodeID,
			Params:          cloneStringMap(directive.Params),
			PathNodeIds:     append([]string(nil), directive.PathNodeIDs...),
		}
		if gameorders.UnitAction(directive.Action) == gameorders.ActionMove {
			if march, ok := state.TurnRuntime.Resolving.ActiveMarches[unitID]; ok {
				queued = queuedMoveOrder(unitID, march)
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

	if playerState := state.Players[playerID]; playerState != nil {
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
