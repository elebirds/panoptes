package combat

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestCapitalDestroyedEndsGameImmediately(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			CastleBaseHP:       10,
			BuildPointsPerTurn: 10,
			TokensPerTurn:      3,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "castle",
				Category:      "city",
				PlacementRule: "city_only",
				BuildCost:     staticdata.ResourceAmounts{},
				Combat:        staticdata.BuildingCombat{MaxHP: 10},
			},
		},
		Units: []staticdata.UnitDefinition{
			{
				ID:          "siege_engine",
				Class:       "siege",
				MaxHP:       20,
				Attack:      10,
				AttackRange: 1,
				MoveRange:   1,
				VisionRange: 2,
				TrainCost:   staticdata.ResourceAmounts{},
				Upkeep:      staticdata.ResourceAmounts{},
				Flags: staticdata.UnitFlags{
					CanSiege:        true,
					SiegeMultiplier: 1,
					CanCapture:      true,
				},
			},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{
		ID:      "A1",
		X:       0,
		Y:       0,
		Terrain: "plain",
	})
	nodeEntry := world.Entry(nodeEntity)
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = "player-1"
	node.TerritoryOwner = "player-1"
	ecs.CreateBuilding(world, "castle", "player-1", "A1", nodeEntry)

	state := domain.NewGameState("combat-siege", []string{"player-1", "player-2"}, []string{"alice", "bob"}, &domain.MapData{
		ID:        "combat-siege",
		NodeIndex: map[string]donburi.Entity{"A1": nodeEntity},
	})
	state.World = world
	state.Players["player-1"].MainCastleHP = 10

	unitEntry := world.Entry(ecs.CreateUnit(world, "siege_engine", "player-2", domain.Position{X: 0, Y: 0}))
	ecs.UnitStatsC.Get(unitEntry).ID = "siege-1"

	events := (&SiegeSystem{}).Run(world, state)
	if len(events) == 0 {
		t.Fatalf("siege events should not be empty")
	}
	applySiegeEvents(state, events)

	if !state.IsOver {
		t.Fatalf("state.IsOver = false, want true")
	}
	if state.WinnerID != "player-2" {
		t.Fatalf("winner_id = %q, want player-2", state.WinnerID)
	}
	if state.OverReason != "castle_destroyed" {
		t.Fatalf("over_reason = %q, want castle_destroyed", state.OverReason)
	}
}

func applySiegeEvents(state *domain.GameState, events []event.Event) {
	for _, evt := range events {
		evt.Apply(state.World, state)
	}
}
