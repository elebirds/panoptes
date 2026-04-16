// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的地图动作结算逻辑。

package game

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/yohamta/donburi"
)

func (r *GameRoom) plannedMapActionEvents() []event.Event {
	state := r.State()
	if r == nil || state == nil {
		return nil
	}
	events := make([]event.Event, 0)
	// 当前 MVP 主链里，map action 只保留 settle_city。
	// 其它 action 即使协议枚举还在，也不会进入默认 resolving 路径。
	for _, directive := range state.TurnRuntime.Planning.UnitOrders {
		switch gameorders.UnitAction(directive.Action) {
		case gameorders.ActionSettleCity:
			if evt, ok := r.cityFoundingEvent(directive); ok {
				events = append(events, evt)
			}
		}
	}
	return events
}

func (r *GameRoom) cityFoundingEvent(order domain.UnitDirective) (event.Event, bool) {
	state := r.State()
	if r == nil || state == nil {
		return nil, false
	}
	// 这里的职责只有“把建城命令翻译成领域事件”，不直接改 ECS。
	// 真正的状态落地统一交给 CityFoundedEvent.Apply()。
	unitEntry, ok := findUnitEntryByID(state.World, order.UnitID)
	if !ok {
		return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: "unit_not_found"}, true
	}

	stats := ecs.UnitStatsC.Get(unitEntry)
	if stats.Faction != order.PlayerID || !isTerritoryExpansionUnit(stats.Type) {
		return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: "invalid_unit_type"}, true
	}

	unitPos := ecs.PositionC.Get(unitEntry)
	centerEntry, ok := domain.GetNodeAt(state.World, domain.Position{X: unitPos.X, Y: unitPos.Y})
	if !ok {
		return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: "invalid_target"}, true
	}
	centerNodeID := ecs.NodeC.Get(centerEntry).ID
	if target := strings.TrimSpace(order.TargetNodeID); target != "" && target != centerNodeID {
		targetEntry, found := r.NodeByID(target)
		if !found {
			return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: "invalid_target"}, true
		}
		centerEntry = targetEntry
		centerNodeID = target
	}

	if ok, reason := ecs.CanFoundCityAt(state, centerEntry); !ok {
		return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: reason}, true
	}
	_, footprintIDs, reason := ecs.TerritoryFootprint(state, centerEntry)
	if reason != "" {
		return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: reason}, true
	}

	// 成功事件里只携带 Apply 所需的最小闭包：
	// 新城中心、3x3 territory、被消耗的 settler，以及下一回合上线时机。
	return event.CityFoundedEvent{
		PlayerID:     order.PlayerID,
		UnitID:       order.UnitID,
		CityID:       centerNodeID,
		CenterNodeID: centerNodeID,
		TerritoryIDs: append([]string(nil), footprintIDs...),
		OnlineOnTurn: state.Turn + 1,
	}, true
}

func findUnitEntryByID(world donburi.World, unitID string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found != nil || entry == nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}

func isTerritoryExpansionUnit(unitType domain.UnitType) bool {
	switch normalizeMapActionToken(string(unitType)) {
	case "settler", "pioneer", "expander", "engineer":
		return true
	default:
		return false
	}
}

func normalizeMapActionToken(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
}
