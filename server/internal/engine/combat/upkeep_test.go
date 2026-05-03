// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-01 00:00:00 +0800
// Description: Tests combat upkeep supply integration with road networks.

package combat

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestCombatUpkeepUsesRoadNetworkSupply(t *testing.T) {
	useUpkeepSupplyTestCatalog(t)
	state := newUpkeepSupplyTestState(t)
	unit := state.World.Entry(ecs.CreateUnit(state.World, "infantry", "player-1", domain.Position{Q: 2, R: 0}))
	ecs.UnitStatsC.Get(unit).ID = "infantry-1"
	state.Players["player-1"].Resources.Set(domain.ResourceFood, 2)
	event.RoadBuiltEvent{FromNode: "C1", ToNode: "M1", Owner: "player-1"}.Apply(state.World, state)
	event.RoadBuiltEvent{FromNode: "M1", ToNode: "F1", Owner: "player-1"}.Apply(state.World, state)

	events := (&CombatUpkeepSystem{}).Run(state.World, state)
	if hasStarvingEvent(events, "infantry-1") {
		t.Fatalf("connected unit starved: %#v", events)
	}

	event.RoadDestroyedEvent{FromNode: "M1", ToNode: "F1", DestroyerID: "raider-1"}.Apply(state.World, state)
	events = (&CombatUpkeepSystem{}).Run(state.World, state)
	if !hasStarvingEvent(events, "infantry-1") {
		t.Fatalf("disconnected unit did not starve: %#v", events)
	}
}

func hasStarvingEvent(events []event.Event, unitID string) bool {
	for _, evt := range events {
		starving, ok := evt.(event.UnitStarvingEvent)
		if ok && starving.UnitID == unitID {
			return true
		}
	}
	return false
}

func useUpkeepSupplyTestCatalog(t *testing.T) {
	t.Helper()
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			CityCoreMaxHP: 100,
		},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true, CanAttackStructures: true}},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))
}

func newUpkeepSupplyTestState(t *testing.T) *domain.GameState {
	t.Helper()
	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{
		"C1": ecs.CreateNode(world, ecs.MapNode{ID: "C1", Q: 0, R: 0, Terrain: "plain"}),
		"M1": ecs.CreateNode(world, ecs.MapNode{ID: "M1", Q: 1, R: 0, Terrain: "plain"}),
		"F1": ecs.CreateNode(world, ecs.MapNode{ID: "F1", Q: 2, R: 0, Terrain: "plain"}),
	}
	state := domain.NewGameState("upkeep-supply-test", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "upkeep-supply-test",
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	state.EnsureCityState("player-1", "C1")
	state.Players["player-1"].CapitalCityID = "C1"
	return state
}
