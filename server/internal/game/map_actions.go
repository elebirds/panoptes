// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的地图动作结算逻辑。

package game

import (
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func (r *GameRoom) applyPlannedMapActions() []*pb.TurnEvent {
	state := r.State()
	if r == nil || state == nil {
		return nil
	}
	events := make([]*pb.TurnEvent, 0)
	for _, directive := range state.TurnRuntime.Planning.UnitOrders {
		switch gameorders.UnitAction(directive.Action) {
		case gameorders.ActionSettleCity:
			if evt, ok := r.applySettleCityOrder(directive); ok {
				events = append(events, evt)
			}
		}
	}
	return events
}

func (r *GameRoom) applySettleCityOrder(order domain.UnitDirective) (*pb.TurnEvent, bool) {
	state := r.State()
	if r == nil || state == nil {
		return nil, false
	}
	unitEntry, ok := findUnitEntryByID(state.World, order.UnitID)
	if !ok {
		return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "unit_not_found"}}, true
	}

	stats := ecs.UnitStatsC.Get(unitEntry)
	if stats.Faction != order.PlayerID || !isTerritoryExpansionUnit(stats.Type) {
		return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "invalid_unit_type"}}, true
	}

	unitPos := ecs.PositionC.Get(unitEntry)
	centerEntry, ok := domain.GetNodeAt(state.World, domain.Position{X: unitPos.X, Y: unitPos.Y})
	if !ok {
		return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "invalid_target"}}, true
	}
	centerNodeID := ecs.NodeC.Get(centerEntry).ID
	if target := strings.TrimSpace(order.TargetNodeID); target != "" && target != centerNodeID {
		targetEntry, found := r.NodeByID(target)
		if !found {
			return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "invalid_target"}}, true
		}
		centerEntry = targetEntry
		centerNodeID = target
	}

	footprintEntries, footprintIDs, err := collectTerritory3x3(state.World, centerEntry)
	if err != nil {
		return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "territory_out_of_bounds"}}, true
	}
	for _, entry := range footprintEntries {
		if entry == nil {
			continue
		}
		nodeComp := ecs.NodeC.Get(entry)
		if nodeComp.IsResource {
			return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "territory_blocked"}}, true
		}
		if !entry.HasComponent(ecs.BuildingC) {
			continue
		}
		building := ecs.BuildingC.Get(entry)
		buildingType := normalizeMapActionToken(string(building.Type))
		nodeID := ecs.NodeC.Get(entry).ID
		if nodeID == centerNodeID && buildingType == "castle" {
			continue
		}
		return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "territory_blocked"}}, true
	}

	barracksEntry, barracksNodeID := pickBarracksNode(centerEntry, footprintEntries)
	if barracksEntry == nil || barracksNodeID == "" {
		return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "no_barracks_slot"}}, true
	}

	for _, entry := range footprintEntries {
		node := ecs.NodeC.Get(entry)
		node.TerritoryOwner = order.PlayerID
		node.Owner = order.PlayerID
	}
	setBuildingOnNode(centerEntry, "castle", order.PlayerID, centerNodeID)
	setBuildingOnNode(barracksEntry, "barracks", order.PlayerID, centerNodeID)
	state.EnsureCastleState(order.PlayerID, centerNodeID)
	state.World.Remove(unitEntry.Entity())

	return &pb.TurnEvent{
		Type: "settle_city",
		Data: map[string]string{
			"player_id":        order.PlayerID,
			"unit_id":          order.UnitID,
			"center_node_id":   centerNodeID,
			"barracks_node_id": barracksNodeID,
			"updated_nodes":    strings.Join(footprintIDs, ","),
		},
	}, true
}

func collectTerritory3x3(world donburi.World, centerEntry *donburi.Entry) ([]*donburi.Entry, []string, error) {
	if centerEntry == nil {
		return nil, nil, fmt.Errorf("center entry is nil")
	}

	centerPos := ecs.PositionC.Get(centerEntry)
	entries := make([]*donburi.Entry, 0, 9)
	ids := make([]string, 0, 9)
	for dy := -1; dy <= 1; dy++ {
		for dx := -1; dx <= 1; dx++ {
			pos := domain.Position{X: centerPos.X + dx, Y: centerPos.Y + dy}
			entry, ok := domain.GetNodeAt(world, pos)
			if !ok {
				return nil, nil, fmt.Errorf("node out of map (%d,%d)", pos.X, pos.Y)
			}
			entries = append(entries, entry)
			ids = append(ids, ecs.NodeC.Get(entry).ID)
		}
	}
	return entries, ids, nil
}

func pickBarracksNode(centerEntry *donburi.Entry, footprintEntries []*donburi.Entry) (*donburi.Entry, string) {
	if centerEntry == nil {
		return nil, ""
	}

	centerPos := ecs.PositionC.Get(centerEntry)
	byPos := make(map[domain.Position]*donburi.Entry, len(footprintEntries))
	for _, entry := range footprintEntries {
		if entry == nil {
			continue
		}
		pos := ecs.PositionC.Get(entry)
		byPos[domain.Position{X: pos.X, Y: pos.Y}] = entry
	}
	candidates := []domain.Position{
		{X: centerPos.X + 1, Y: centerPos.Y},
		{X: centerPos.X - 1, Y: centerPos.Y},
		{X: centerPos.X, Y: centerPos.Y + 1},
		{X: centerPos.X, Y: centerPos.Y - 1},
	}
	for _, candidate := range candidates {
		entry, ok := byPos[candidate]
		if !ok || entry == nil {
			continue
		}
		return entry, ecs.NodeC.Get(entry).ID
	}
	for _, entry := range footprintEntries {
		if entry == nil || entry == centerEntry {
			continue
		}
		return entry, ecs.NodeC.Get(entry).ID
	}
	return nil, ""
}

func setBuildingOnNode(nodeEntry *donburi.Entry, buildingType, owner string, castleID string) {
	if nodeEntry == nil {
		return
	}
	maxHP, wallLevel, towers := resolveBuildingTemplate(buildingType)
	comp := domain.BuildingComp{
		Type:      domain.BuildingType(buildingType),
		HP:        maxHP,
		MaxHP:     maxHP,
		WallLevel: wallLevel,
		Owner:     owner,
		CastleID:  castleID,
		Towers:    towers,
	}
	if !nodeEntry.HasComponent(ecs.BuildingC) {
		nodeEntry.AddComponent(ecs.BuildingC)
	}
	ecs.BuildingC.SetValue(nodeEntry, comp)
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = owner
}

func resolveBuildingTemplate(buildingType string) (maxHP int, wallLevel int, towers int) {
	maxHP = 100
	catalog := staticdata.Default()
	if catalog != nil {
		if cfg, ok := catalog.GetBuilding(buildingType); ok {
			maxHP = cfg.Combat.MaxHP
			wallLevel = cfg.Combat.WallLevel
			towers = cfg.Combat.Towers
			return
		}
		if normalizeMapActionToken(buildingType) == "castle" {
			maxHP = catalog.Rules().CastleBaseHP
			return
		}
	}
	switch normalizeMapActionToken(buildingType) {
	case "barracks":
		return 120, 0, 0
	case "castle":
		return 100, 0, 0
	default:
		return maxHP, 0, 0
	}
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
