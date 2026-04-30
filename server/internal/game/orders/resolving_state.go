// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规划单位指令、地图动作与 resolving 单位订单的状态转换逻辑。

package orders

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
)

// BuildResolvingUnitOrders 冻结 planning 草案到 resolving 输入。
// ActiveMarches 先入队，随后本回合显式 UnitOrders 覆盖它；这样长距离行军和玩家最新指令
// 可以共享同一个 UnitResolutionOrder 面向 engine/combat。
func BuildResolvingUnitOrders(state *domain.GameState, routes RoutePreviewCallbacks) {
	if state == nil {
		return
	}
	if state.TurnRuntime.Resolving.UnitOrders == nil {
		state.TurnRuntime.Resolving.UnitOrders = make(map[string]domain.UnitResolutionOrder)
	}
	clear(state.TurnRuntime.Resolving.UnitOrders)

	for unitID, march := range state.TurnRuntime.Resolving.ActiveMarches {
		state.TurnRuntime.Resolving.UnitOrders[unitID] = domain.UnitResolutionOrder{
			PlayerID:     march.PlayerID,
			UnitID:       unitID,
			Action:       domain.UnitResolutionActionMove,
			TargetNodeID: march.DestinationNodeID,
			PathNodeIDs:  append([]string(nil), march.LastPreview.PathNodeIDs...),
		}
	}

	for unitID, directive := range state.TurnRuntime.Planning.UnitOrders {
		order := FromDirective(directive)
		if resolutionOrder, ok := order.ToResolutionOrder(); ok {
			if resolutionOrder.Action == domain.UnitResolutionActionMove {
				if march, ok := state.TurnRuntime.Resolving.ActiveMarches[unitID]; ok && len(march.LastPreview.PathNodeIDs) > 0 {
					resolutionOrder.TargetNodeID = march.DestinationNodeID
					resolutionOrder.PathNodeIDs = append([]string(nil), march.LastPreview.PathNodeIDs...)
				} else if preview, ok := previewByDestination(routes, unitID, resolutionOrder.TargetNodeID); ok {
					resolutionOrder.PathNodeIDs = append([]string(nil), preview.PathNodeIDs...)
				}
			}
			state.TurnRuntime.Resolving.UnitOrders[unitID] = resolutionOrder.Normalized()
			continue
		}

		if UnitAction(order.Action) == ActionSettleCity && strings.TrimSpace(order.TargetNodeID) != "" {
			state.TurnRuntime.Resolving.UnitOrders[unitID] = domain.UnitResolutionOrder{
				PlayerID:     order.PlayerID,
				UnitID:       order.UnitID,
				Action:       domain.UnitResolutionActionMove,
				TargetNodeID: order.TargetNodeID,
			}
		}
	}
}

// RefreshActiveMarchesAfterSettlement 在单位结算后刷新跨回合行军缓存。
// 到达、单位消失、目标失效或路径无法重建时都会清理缓存，避免下一回合继续执行脏路径。
func RefreshActiveMarchesAfterSettlement(state *domain.GameState, routes RoutePreviewCallbacks) {
	if state == nil {
		return
	}
	for unitID, march := range state.TurnRuntime.Resolving.ActiveMarches {
		entry, ok := findUnitByID(state, unitID)
		if !ok {
			delete(state.TurnRuntime.Resolving.ActiveMarches, unitID)
			continue
		}
		currentPos := ecs.PositionC.Get(entry)
		currentNodeID := nodeIDAt(state, domain.Position{Q: currentPos.Q, R: currentPos.R})
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
		if currentPos.Q == targetPos.Q && currentPos.R == targetPos.R {
			delete(state.TurnRuntime.Resolving.ActiveMarches, unitID)
			continue
		}
		if preview, ok := advanceActiveMarchPreview(unitID, march, currentNodeID, routes); ok {
			march.LastPreview = preview
			state.TurnRuntime.Resolving.ActiveMarches[unitID] = march
			continue
		}
		if preview, ok := previewByDestination(routes, unitID, march.DestinationNodeID); ok {
			march.LastPreview = preview
			state.TurnRuntime.Resolving.ActiveMarches[unitID] = march
			continue
		}
		delete(state.TurnRuntime.Resolving.ActiveMarches, unitID)
	}
}

func advanceActiveMarchPreview(unitID string, march domain.ActiveMarch, currentNodeID string, routes RoutePreviewCallbacks) (domain.RoutePreview, bool) {
	remainingPathNodeIDs, ok := trimMarchPathFromCurrentNode(march.LastPreview.PathNodeIDs, currentNodeID, march.DestinationNodeID)
	if !ok {
		return domain.RoutePreview{}, false
	}
	return previewByPath(routes, unitID, remainingPathNodeIDs)
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

func nodeIDAt(state *domain.GameState, pos domain.Position) string {
	if state == nil || state.World == nil {
		return ""
	}
	if entry, ok := domain.GetNodeAt(state.World, pos); ok {
		return ecs.NodeC.Get(entry).ID
	}
	return ""
}
