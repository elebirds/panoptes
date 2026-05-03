package event

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

func TestGameOverEventRefreshesStructuredOutcome(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, nil)

	GameOverEvent{WinnerID: "player-1", Reason: "timeout_draw"}.Apply(state.World, state)

	if !state.Outcome.IsOver {
		t.Fatalf("structured outcome is not over")
	}
	if state.Outcome.WinnerID != "player-1" || state.Outcome.Reason != "timeout_draw" {
		t.Fatalf("structured outcome = %+v", state.Outcome)
	}
}

func TestCityCoreDestroyedEventRefreshesStructuredOutcome(t *testing.T) {
	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", Q: 0, R: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.CreateBuilding(world, "city_core", "player-1", "A1", nodeEntry)

	state := domain.NewGameState("game-1", []string{"player-1", "player-2"}, []string{"alice", "bob"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"A1": nodeEntity},
	})
	state.World = world
	state.NodeIndex = state.Map.NodeIndex
	state.Players["player-1"].CapitalCityID = "A1"

	CityCoreDestroyedEvent{NodeID: "A1", ConquerorFaction: "player-2"}.Apply(world, state)

	if !state.Outcome.IsOver {
		t.Fatalf("structured outcome is not over")
	}
	if state.Outcome.WinnerID != "player-2" || state.Outcome.Reason != "city_core_destroyed" {
		t.Fatalf("structured outcome = %+v", state.Outcome)
	}
}
