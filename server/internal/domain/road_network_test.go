// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-01 00:00:00 +0800
// Description: Tests backend-owned road network connectivity.

package domain_test

import (
	"reflect"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestRoadConnectedChangesThroughRoadEvents(t *testing.T) {
	useRoadNetworkTestCatalog(t)
	state := newRoadNetworkTestState(t)

	if domain.RoadConnected(state, "C1", "C2") {
		t.Fatalf("RoadConnected before construction = true, want false")
	}

	event.RoadBuiltEvent{FromNode: "C1", ToNode: "M1", Owner: "player-1"}.Apply(state.World, state)
	event.RoadBuiltEvent{FromNode: "M1", ToNode: "C2", Owner: "player-1"}.Apply(state.World, state)
	if !domain.RoadConnected(state, "C1", "C2") {
		t.Fatalf("RoadConnected after construction = false, want true")
	}

	event.RoadDestroyedEvent{FromNode: "M1", ToNode: "C2", DestroyerID: "player-2"}.Apply(state.World, state)
	if domain.RoadConnected(state, "C1", "C2") {
		t.Fatalf("RoadConnected after destruction = true, want false")
	}

	event.RoadRepairedEvent{FromNode: "M1", ToNode: "C2", Owner: "player-1"}.Apply(state.World, state)
	if !domain.RoadConnected(state, "C1", "C2") {
		t.Fatalf("RoadConnected after repair = false, want true")
	}
}

func TestPlayerRoadNetworkStatusReportsConnectedCities(t *testing.T) {
	useRoadNetworkTestCatalog(t)
	state := newRoadNetworkTestState(t)
	event.RoadBuiltEvent{FromNode: "C1", ToNode: "M1", Owner: "player-1"}.Apply(state.World, state)

	status := domain.PlayerRoadNetworkStatus(state, "player-1")
	if status.CapitalCityID != "C1" {
		t.Fatalf("capital = %q, want C1", status.CapitalCityID)
	}
	if !reflect.DeepEqual(status.ConnectedCityIDs, []string{"C1"}) {
		t.Fatalf("connected cities = %#v, want [C1]", status.ConnectedCityIDs)
	}
	if !reflect.DeepEqual(status.DisconnectedCityIDs, []string{"C2"}) {
		t.Fatalf("disconnected cities = %#v, want [C2]", status.DisconnectedCityIDs)
	}

	event.RoadBuiltEvent{FromNode: "M1", ToNode: "C2", Owner: "player-1"}.Apply(state.World, state)
	status = domain.PlayerRoadNetworkStatus(state, "player-1")
	if !reflect.DeepEqual(status.ConnectedCityIDs, []string{"C1", "C2"}) {
		t.Fatalf("connected cities = %#v, want [C1 C2]", status.ConnectedCityIDs)
	}
	if len(status.DisconnectedCityIDs) != 0 {
		t.Fatalf("disconnected cities = %#v, want none", status.DisconnectedCityIDs)
	}
}

func useRoadNetworkTestCatalog(t *testing.T) {
	t.Helper()
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			CityCoreMaxHP:              100,
			InitialCityTerritoryRadius: 1,
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))
}

func newRoadNetworkTestState(t *testing.T) *domain.GameState {
	t.Helper()
	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{
		"C1": ecs.CreateNode(world, ecs.MapNode{ID: "C1", Q: 0, R: 0, Terrain: "plain"}),
		"M1": ecs.CreateNode(world, ecs.MapNode{ID: "M1", Q: 1, R: 0, Terrain: "plain"}),
		"C2": ecs.CreateNode(world, ecs.MapNode{ID: "C2", Q: 2, R: 0, Terrain: "plain"}),
	}
	state := domain.NewGameState("road-network-test", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "road-network-test",
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	state.EnsureCityState("player-1", "C1")
	state.EnsureCityState("player-1", "C2")
	state.Players["player-1"].CapitalCityID = "C1"
	return state
}
