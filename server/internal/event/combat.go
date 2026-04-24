// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现事件模型的单位结算事件与领域类型。

package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

type UnitMovedEvent struct {
	UnitID    string
	From      domain.Position
	To        domain.Position
	Timestamp int
}

func (e UnitMovedEvent) Apply(world donburi.World, _ *domain.GameState) {
	entry, ok := findUnitByID(world, e.UnitID)
	if !ok {
		return
	}
	pos := ecs.PositionC.Get(entry)
	pos.Q = e.To.Q
	pos.R = e.To.R
}

func (e UnitMovedEvent) Kind() string { return "unit_moved" }

func (e UnitMovedEvent) String() string {
	return fmt.Sprintf("UnitMovedEvent unit=%s from=(%d,%d) to=(%d,%d)", e.UnitID, e.From.Q, e.From.R, e.To.Q, e.To.R)
}

type UnitDamagedEvent struct {
	UnitID  string
	Damage  int
	HPAfter int
	Source  string
}

func (e UnitDamagedEvent) Apply(world donburi.World, _ *domain.GameState) {
	entry, ok := findUnitByID(world, e.UnitID)
	if !ok {
		return
	}
	stats := ecs.UnitStatsC.Get(entry)
	stats.HP = e.HPAfter
}

func (e UnitDamagedEvent) Kind() string { return "unit_damaged" }

func (e UnitDamagedEvent) String() string {
	return fmt.Sprintf("UnitDamagedEvent unit=%s dmg=%d hp_after=%d source=%s", e.UnitID, e.Damage, e.HPAfter, e.Source)
}

type UnitDiedEvent struct {
	UnitID   string
	KillerID string
	Pos      domain.Position
}

func (e UnitDiedEvent) Apply(world donburi.World, _ *domain.GameState) {
	entry, ok := findUnitByID(world, e.UnitID)
	if !ok {
		return
	}
	world.Remove(entry.Entity())
}

func (e UnitDiedEvent) Kind() string { return "unit_died" }

func (e UnitDiedEvent) String() string {
	return fmt.Sprintf("UnitDiedEvent unit=%s killer=%s", e.UnitID, e.KillerID)
}

type CityCoreDamagedEvent struct {
	NodeID     string
	Damage     int
	HPAfter    int
	AttackerID string
}

func (e CityCoreDamagedEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !nodeEntry.HasComponent(ecs.BuildingC) {
		return
	}
	building := ecs.BuildingC.Get(nodeEntry)
	building.HP = e.HPAfter
	if ownerState, ok := state.Players[building.Owner]; ok {
		if cityID := ecs.ResolveCityID(nodeEntry); cityID != "" && cityID == ownerState.CapitalCityID {
			ownerState.CapitalCityCoreHP = e.HPAfter
		}
	}
}

func (e CityCoreDamagedEvent) Kind() string { return "city_core_damaged" }

func (e CityCoreDamagedEvent) String() string {
	return fmt.Sprintf("CityCoreDamagedEvent node=%s dmg=%d hp_after=%d", e.NodeID, e.Damage, e.HPAfter)
}

type CityCoreDestroyedEvent struct {
	NodeID           string
	ConquerorFaction string
}

func (e CityCoreDestroyedEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !nodeEntry.HasComponent(ecs.BuildingC) {
		return
	}
	building := ecs.BuildingC.Get(nodeEntry)
	ownerState, ok := state.Players[building.Owner]
	if !ok || ownerState == nil {
		return
	}
	if cityID := ecs.ResolveCityID(nodeEntry); cityID == "" || cityID != ownerState.CapitalCityID {
		return
	}
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = e.ConquerorFaction
	building.Owner = e.ConquerorFaction
	state.IsOver = true
	state.WinnerID = e.ConquerorFaction
	state.OverReason = "city_core_destroyed"
}

func (e CityCoreDestroyedEvent) Kind() string { return "city_core_destroyed" }

func (e CityCoreDestroyedEvent) String() string {
	return fmt.Sprintf("CityCoreDestroyedEvent node=%s conqueror=%s", e.NodeID, e.ConquerorFaction)
}

type RoadDestroyedEvent struct {
	FromNode    string
	ToNode      string
	DestroyerID string
}

func (e RoadDestroyedEvent) Apply(world donburi.World, state *domain.GameState) {
	if fromEntry, ok := findNodeByID(world, state, e.FromNode); ok {
		n := ecs.NodeC.Get(fromEntry)
		n.HasRoad = false
	}
	if toEntry, ok := findNodeByID(world, state, e.ToNode); ok {
		n := ecs.NodeC.Get(toEntry)
		n.HasRoad = false
	}
}

func (e RoadDestroyedEvent) Kind() string { return "road_destroyed" }

func (e RoadDestroyedEvent) String() string {
	return fmt.Sprintf("RoadDestroyedEvent %s->%s by=%s", e.FromNode, e.ToNode, e.DestroyerID)
}

type BuildingDamagedEvent struct {
	NodeID  string
	Damage  int
	HPAfter int
}

func (e BuildingDamagedEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !nodeEntry.HasComponent(ecs.BuildingC) {
		return
	}
	building := ecs.BuildingC.Get(nodeEntry)
	building.HP = e.HPAfter
}

func (e BuildingDamagedEvent) Kind() string { return "building_damaged" }

func (e BuildingDamagedEvent) String() string {
	return fmt.Sprintf("BuildingDamagedEvent node=%s dmg=%d hp_after=%d", e.NodeID, e.Damage, e.HPAfter)
}

type ConflictResolvedEvent struct {
	UnitAID      string
	UnitBID      string
	Location     domain.Position
	ConflictType string
}

func (e ConflictResolvedEvent) Apply(donburi.World, *domain.GameState) {}

func (e ConflictResolvedEvent) Kind() string { return "conflict" }

func (e ConflictResolvedEvent) String() string {
	return fmt.Sprintf("ConflictResolvedEvent a=%s b=%s type=%s", e.UnitAID, e.UnitBID, e.ConflictType)
}

func findUnitByID(world donburi.World, unitID string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found != nil {
			return
		}
		if ecs.UnitStatsC.Get(entry).ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}

func findNodeByID(world donburi.World, state *domain.GameState, nodeID string) (*donburi.Entry, bool) {
	if state != nil {
		if entry, ok := state.GetNode(nodeID); ok {
			return entry, true
		}
	}
	return ecs.FindNodeByID(world, nodeID)
}
