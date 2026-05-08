package session

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestRuntimeApplyMinisterActionsQueuesValidatedBuildAndMoveOrders(t *testing.T) {
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{CityCoreMaxHP: 100},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanAttackStructures: true}},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "farm", PlacementKind: "resource_node", BuildingScope: "out_of_city", RequiredResourceType: "food", MaxHP: 15, TakeoverMode: "delayed"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{
		"A1": ecs.CreateNode(world, ecs.MapNode{ID: "A1", Q: 0, R: 0, Terrain: "plain"}),
		"A2": ecs.CreateNode(world, ecs.MapNode{ID: "A2", Q: 1, R: 0, Terrain: "plain", IsResourcePoint: true, ResourceType: "food"}),
	}
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	state.Players["player-1"].Research.UnlockBuilding("farm")

	coreEntry := state.World.Entry(nodeIndex["A1"])
	ecs.CreateBuilding(world, "city_core", "player-1", "A1", coreEntry)
	resourceEntry := state.World.Entry(nodeIndex["A2"])
	ecs.NodeC.Get(resourceEntry).TerritoryOwner = "player-1"

	unitEntry := state.World.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unitEntry).ID = "infantry-1"

	runtime := newTestRuntime("game-1", []*capturePlayer{{playerID: "player-1", username: "alice"}}, nil)
	runtime.SetState(state)

	err := runtime.ApplyMinisterActions("player-1", []ministerengine.MinisterActionItem{
		{
			Type: "build",
			Params: map[string]any{
				"node_id":       "A2",
				"building_type": "farm",
				"city_id":       "A1",
			},
		},
		{
			Type: "move_units",
			Params: map[string]any{
				"unit_id":     "infantry-1",
				"target_node": "A2",
			},
		},
	})
	if err != nil {
		t.Fatalf("ApplyMinisterActions error = %v", err)
	}

	if got := len(state.TurnRuntime.Planning.BuildOrders); got != 1 {
		t.Fatalf("build orders = %d, want 1", got)
	}
	build := state.TurnRuntime.Planning.BuildOrders[0]
	if build.PlayerID != "player-1" || build.NodeID != "A2" || build.BuildingType != "farm" || build.CityID != "A1" {
		t.Fatalf("build order = %#v, want minister build queued", build)
	}

	move, ok := state.TurnRuntime.Planning.UnitOrders["infantry-1"]
	if !ok {
		t.Fatalf("minister move order missing")
	}
	if move.Action != "move" || move.TargetNodeID != "A2" {
		t.Fatalf("minister move directive = %#v, want queued move order", move)
	}
	if _, ok := state.TurnRuntime.Resolving.ActiveMarches["infantry-1"]; !ok {
		t.Fatalf("active march missing for minister move")
	}
}
