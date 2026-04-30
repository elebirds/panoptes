// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规划单位指令、地图动作与 resolving 单位订单的状态转换逻辑。

package orders

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestBuildMapActionEventsBuildsSettleCityEvent(t *testing.T) {
	useMapActionTestCatalog(t)
	state := newMapActionTestState(t, domain.Position{Q: 0, R: 0})
	state.Turn = 4
	settler := worldEntry(state.World, ecs.CreateUnit(state.World, "settler", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(settler).ID = "settler-1"
	state.TurnRuntime.Planning.UnitOrders["settler-1"] = domain.UnitDirective{
		PlayerID:     "player-1",
		UnitID:       "settler-1",
		Action:       string(ActionSettleCity),
		TargetNodeID: "C",
	}

	events := BuildMapActionEvents(state)

	if len(events) != 1 {
		t.Fatalf("event count = %d, want 1: %#v", len(events), events)
	}
	evt, ok := events[0].(event.CityFoundedEvent)
	if !ok {
		t.Fatalf("event type = %T, want CityFoundedEvent", events[0])
	}
	if evt.PlayerID != "player-1" || evt.UnitID != "settler-1" || evt.CityID != "C" || evt.CenterNodeID != "C" {
		t.Fatalf("city founded event identity = %#v", evt)
	}
	if evt.OnlineOnTurn != 5 {
		t.Fatalf("OnlineOnTurn = %d, want 5", evt.OnlineOnTurn)
	}
	if len(evt.TerritoryIDs) != 7 {
		t.Fatalf("territory ids = %#v, want center plus six neighbors", evt.TerritoryIDs)
	}
}

func TestBuildMapActionEventsBuildsFailureForInvalidSettleCity(t *testing.T) {
	useMapActionTestCatalog(t)

	tests := []struct {
		name      string
		setup     func(*testing.T, *domain.GameState)
		wantUnit  string
		wantCause string
	}{
		{
			name: "missing unit",
			setup: func(t *testing.T, state *domain.GameState) {
				state.TurnRuntime.Planning.UnitOrders["missing"] = domain.UnitDirective{
					PlayerID: "player-1",
					UnitID:   "missing",
					Action:   string(ActionSettleCity),
				}
			},
			wantUnit:  "missing",
			wantCause: "unit_not_found",
		},
		{
			name: "unsupported unit type",
			setup: func(t *testing.T, state *domain.GameState) {
				unit := worldEntry(state.World, ecs.CreateUnit(state.World, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
				ecs.UnitStatsC.Get(unit).ID = "infantry-1"
				state.TurnRuntime.Planning.UnitOrders["infantry-1"] = domain.UnitDirective{
					PlayerID: "player-1",
					UnitID:   "infantry-1",
					Action:   string(ActionSettleCity),
				}
			},
			wantUnit:  "infantry-1",
			wantCause: "invalid_unit_type",
		},
		{
			name: "missing target node",
			setup: func(t *testing.T, state *domain.GameState) {
				unit := worldEntry(state.World, ecs.CreateUnit(state.World, "settler", "player-1", domain.Position{Q: 0, R: 0}))
				ecs.UnitStatsC.Get(unit).ID = "settler-1"
				state.TurnRuntime.Planning.UnitOrders["settler-1"] = domain.UnitDirective{
					PlayerID:     "player-1",
					UnitID:       "settler-1",
					Action:       string(ActionSettleCity),
					TargetNodeID: "missing-node",
				}
			},
			wantUnit:  "settler-1",
			wantCause: "invalid_target",
		},
		{
			name: "blocked territory",
			setup: func(t *testing.T, state *domain.GameState) {
				center, ok := state.GetNode("C")
				if !ok {
					t.Fatalf("missing center node")
				}
				ecs.NodeC.Get(center).IsResource = true
				unit := worldEntry(state.World, ecs.CreateUnit(state.World, "settler", "player-1", domain.Position{Q: 0, R: 0}))
				ecs.UnitStatsC.Get(unit).ID = "settler-1"
				state.TurnRuntime.Planning.UnitOrders["settler-1"] = domain.UnitDirective{
					PlayerID: "player-1",
					UnitID:   "settler-1",
					Action:   string(ActionSettleCity),
				}
			},
			wantUnit:  "settler-1",
			wantCause: "territory_blocked",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			state := newMapActionTestState(t, domain.Position{Q: 0, R: 0})
			tt.setup(t, state)

			events := BuildMapActionEvents(state)

			if len(events) != 1 {
				t.Fatalf("event count = %d, want 1: %#v", len(events), events)
			}
			evt, ok := events[0].(event.CityFoundingFailedEvent)
			if !ok {
				t.Fatalf("event type = %T, want CityFoundingFailedEvent", events[0])
			}
			if evt.PlayerID != "player-1" || evt.UnitID != tt.wantUnit || evt.Reason != tt.wantCause {
				t.Fatalf("failure event = %#v, want player-1/%s/%s", evt, tt.wantUnit, tt.wantCause)
			}
		})
	}
}

func TestBuildMapActionEventsBuildsRoadEvents(t *testing.T) {
	useMapActionTestCatalog(t)
	state := newMapActionTestState(t, domain.Position{Q: 0, R: 0})
	settler := worldEntry(state.World, ecs.CreateUnit(state.World, "settler", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(settler).ID = "settler-1"
	state.TurnRuntime.Planning.UnitOrders["settler-1"] = domain.UnitDirective{
		PlayerID:        "player-1",
		UnitID:          "settler-1",
		Action:          string(ActionBuildRoad),
		TargetNodeID:    "C",
		SecondaryNodeID: "A",
	}

	events := BuildMapActionEvents(state)
	if len(events) != 1 {
		t.Fatalf("event count = %d, want 1: %#v", len(events), events)
	}
	built, ok := events[0].(event.RoadBuiltEvent)
	if !ok {
		t.Fatalf("event type = %T, want RoadBuiltEvent", events[0])
	}
	if built.FromNode != "C" || built.ToNode != "A" || built.Owner != "player-1" {
		t.Fatalf("road built event = %#v", built)
	}

	state.TurnRuntime.Planning.UnitOrders["settler-1"] = domain.UnitDirective{
		PlayerID:        "player-1",
		UnitID:          "settler-1",
		Action:          string(ActionRepairRoad),
		TargetNodeID:    "C",
		SecondaryNodeID: "A",
	}
	events = BuildMapActionEvents(state)
	if len(events) != 1 {
		t.Fatalf("repair event count = %d, want 1: %#v", len(events), events)
	}
	repaired, ok := events[0].(event.RoadRepairedEvent)
	if !ok {
		t.Fatalf("event type = %T, want RoadRepairedEvent", events[0])
	}
	if repaired.FromNode != "C" || repaired.ToNode != "A" || repaired.Owner != "player-1" {
		t.Fatalf("road repaired event = %#v", repaired)
	}
}

func TestBuildMapActionEventsBuildsImprovementEvents(t *testing.T) {
	useMapActionTestCatalog(t)
	state := newMapActionTestState(t, domain.Position{Q: 0, R: 0})
	settler := worldEntry(state.World, ecs.CreateUnit(state.World, "settler", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(settler).ID = "settler-1"
	state.Turn = 6
	core, _ := state.GetNode("C")
	ecs.CreateBuilding(state.World, "city_core", "player-1", "C", core)
	state.EnsureCityState("player-1", "C")
	state.Players["player-1"].CapitalCityID = "C"
	resource, _ := state.GetNode("A")
	node := ecs.NodeC.Get(resource)
	node.IsResource = true
	node.ResourceType = "food"
	state.TurnRuntime.Planning.UnitOrders["settler-1"] = domain.UnitDirective{
		PlayerID:     "player-1",
		UnitID:       "settler-1",
		Action:       string(ActionBuildImprovement),
		TargetNodeID: "A",
		Params:       map[string]string{"city_id": "C"},
	}

	events := BuildMapActionEvents(state)
	if len(events) != 1 {
		t.Fatalf("event count = %d, want 1: %#v", len(events), events)
	}
	built, ok := events[0].(event.BuildingBuiltEvent)
	if !ok {
		t.Fatalf("event type = %T, want BuildingBuiltEvent", events[0])
	}
	if built.NodeID != "A" || built.BuildingType != "farm" || built.Owner != "player-1" || built.CityID != "C" || built.OnlineOnTurn != 7 {
		t.Fatalf("building built event = %#v", built)
	}

	ecs.CreateBuilding(state.World, "farm", "player-1", "C", resource)
	ecs.BuildingC.Get(resource).HP = 1
	state.TurnRuntime.Planning.UnitOrders["settler-1"] = domain.UnitDirective{
		PlayerID:     "player-1",
		UnitID:       "settler-1",
		Action:       string(ActionRepairImprovement),
		TargetNodeID: "A",
	}
	events = BuildMapActionEvents(state)
	if len(events) != 1 {
		t.Fatalf("repair event count = %d, want 1: %#v", len(events), events)
	}
	repaired, ok := events[0].(event.BuildingRepairedEvent)
	if !ok {
		t.Fatalf("event type = %T, want BuildingRepairedEvent", events[0])
	}
	if repaired.NodeID != "A" || repaired.Owner != "player-1" {
		t.Fatalf("building repaired event = %#v", repaired)
	}
}

func useMapActionTestCatalog(t *testing.T) {
	t.Helper()
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			MinimumCityDistance:       0,
		},
		Units: []staticdata.UnitDefinition{
			{ID: "settler", Class: "civilian", MaxHP: 12, MoveRange: 2, VisionRange: 2, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true}},
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true, CanAttackStructures: true}},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "farm", PlacementKind: "resource_node", BuildingScope: "out_of_city", RequiredResourceType: "food", MaxHP: 80, TakeoverMode: "delayed"},
			{ID: "mine", PlacementKind: "resource_node", BuildingScope: "out_of_city", RequiredResourceType: "ore", MaxHP: 80, TakeoverMode: "delayed"},
			{ID: "lumber", PlacementKind: "resource_node", BuildingScope: "out_of_city", RequiredResourceType: "wood", MaxHP: 80, TakeoverMode: "delayed"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))
}

func newMapActionTestState(t *testing.T, center domain.Position) *domain.GameState {
	t.Helper()
	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{}
	addNode := func(id string, pos domain.Position) {
		nodeIndex[id] = ecs.CreateNode(world, ecs.MapNode{ID: id, Q: pos.Q, R: pos.R, Terrain: "plain"})
	}
	addNode("C", center)
	for idx, pos := range center.Neighbors() {
		addNode(string(rune('A'+idx)), pos)
	}
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "map-action-test",
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	return state
}

func worldEntry(world donburi.World, entity donburi.Entity) *donburi.Entry {
	return world.Entry(entity)
}
