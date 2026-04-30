// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规划单位指令、地图动作与 resolving 单位订单的状态转换逻辑。

package orders

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
)

type RoutePreviewFunc func(unitID string, destinationNodeID string) (domain.RoutePreview, bool)
type RoutePathPreviewFunc func(unitID string, pathNodeIDs []string) (domain.RoutePreview, bool)

type RoutePreviewCallbacks struct {
	ByDestination RoutePreviewFunc
	ByPath        RoutePathPreviewFunc
}

// ApplyPlanningUnitOrder 是 planning 阶段单位订单的唯一状态写入口。
// 它同时维护 TurnRuntime.Planning.UnitOrders 与跨回合 ActiveMarches：
// move 会刷新行军缓存，非 move 会清掉对应缓存，attack 会尽量继承已规划的行军路径。
func ApplyPlanningUnitOrder(state *domain.GameState, order UnitOrder, routes RoutePreviewCallbacks) {
	if state == nil || order.UnitID == "" {
		return
	}
	if !order.IsUnitResolutionAction() && !isSupportedPlanningMapAction(order.Action) {
		return
	}
	if state.TurnRuntime.Planning.UnitOrders == nil {
		state.TurnRuntime.Planning.UnitOrders = make(map[string]domain.UnitDirective)
	}
	if state.TurnRuntime.Resolving.ActiveMarches == nil {
		state.TurnRuntime.Resolving.ActiveMarches = make(map[string]domain.ActiveMarch)
	}
	if order.PlayerID == "" {
		order.PlayerID = PlayerIDForUnit(state, order.UnitID)
	}

	preserveAttackPathFromMarch(state, &order, routes)

	state.TurnRuntime.Planning.UnitOrders[order.UnitID] = order.ToDirective()
	if resolutionOrder, ok := order.ToResolutionOrder(); ok && resolutionOrder.Action == domain.UnitResolutionActionMove {
		syncActiveMarchWithOrder(state, resolutionOrder, routes)
		return
	}
	delete(state.TurnRuntime.Resolving.ActiveMarches, order.UnitID)
}

func isSupportedPlanningMapAction(action UnitAction) bool {
	switch action {
	case ActionSettleCity, ActionBuildRoad, ActionRepairRoad:
		return true
	default:
		return false
	}
}

func CancelPlanningUnitOrder(state *domain.GameState, playerID string, unitID string) {
	if state == nil {
		return
	}
	directive, ok := state.TurnRuntime.Planning.UnitOrders[unitID]
	if !ok {
		return
	}
	if playerID != "" && directive.PlayerID != "" && directive.PlayerID != playerID {
		return
	}
	delete(state.TurnRuntime.Planning.UnitOrders, unitID)
	delete(state.TurnRuntime.Resolving.ActiveMarches, unitID)
}

func PlayerIDForUnit(state *domain.GameState, unitID string) string {
	entry, ok := findUnitByID(state, unitID)
	if !ok {
		return ""
	}
	return ecs.UnitStatsC.Get(entry).Faction
}

// preserveAttackPathFromMarch 保留“先移动再攻击”的规划语义。
// 玩家在同一 planning 窗口把 move 替换为 attack 时，攻击结算需要从移动后的路径/位置推导。
func preserveAttackPathFromMarch(state *domain.GameState, order *UnitOrder, routes RoutePreviewCallbacks) {
	if state == nil || order == nil || order.Action != ActionAttack || len(order.PathNodeIDs) > 0 {
		return
	}
	if march, ok := state.TurnRuntime.Resolving.ActiveMarches[order.UnitID]; ok {
		if len(march.LastPreview.PathNodeIDs) > 0 {
			order.PathNodeIDs = append([]string(nil), march.LastPreview.PathNodeIDs...)
		} else if preview, ok := previewByDestination(routes, order.UnitID, march.DestinationNodeID); ok && len(preview.PathNodeIDs) > 0 {
			order.PathNodeIDs = append([]string(nil), preview.PathNodeIDs...)
		}
	}
	if len(order.PathNodeIDs) == 0 && order.SecondaryNodeID != "" {
		if preview, ok := previewByDestination(routes, order.UnitID, order.SecondaryNodeID); ok && len(preview.PathNodeIDs) > 0 {
			order.PathNodeIDs = append([]string(nil), preview.PathNodeIDs...)
		}
	}
}

func syncActiveMarchWithOrder(state *domain.GameState, order domain.UnitResolutionOrder, routes RoutePreviewCallbacks) {
	if state == nil || order.UnitID == "" {
		return
	}
	if state.TurnRuntime.Resolving.ActiveMarches == nil {
		state.TurnRuntime.Resolving.ActiveMarches = make(map[string]domain.ActiveMarch)
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
	if preview, ok := previewByDestination(routes, order.UnitID, order.TargetNodeID); ok {
		march.LastPreview = preview
	}
	state.TurnRuntime.Resolving.ActiveMarches[order.UnitID] = march
}

func previewByDestination(routes RoutePreviewCallbacks, unitID string, destinationNodeID string) (domain.RoutePreview, bool) {
	if routes.ByDestination == nil {
		return domain.RoutePreview{}, false
	}
	return routes.ByDestination(unitID, destinationNodeID)
}

func previewByPath(routes RoutePreviewCallbacks, unitID string, pathNodeIDs []string) (domain.RoutePreview, bool) {
	if routes.ByPath == nil {
		return domain.RoutePreview{}, false
	}
	return routes.ByPath(unitID, pathNodeIDs)
}
