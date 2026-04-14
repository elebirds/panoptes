package domain

import "testing"

func TestNewGameStateInitializesTurnRuntimeContainers(t *testing.T) {
	t.Parallel()

	state := NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &MapData{})

	if state.TurnRuntime.Planning.UnitOrders == nil {
		t.Fatalf("planning unit orders map is nil")
	}
	if state.TurnRuntime.Planning.MinisterDirectives == nil {
		t.Fatalf("planning minister directives map is nil")
	}
	if state.TurnRuntime.Planning.WarDirectives == nil {
		t.Fatalf("planning war directives map is nil")
	}
	if state.TurnRuntime.Resolving.UnitOrders == nil {
		t.Fatalf("resolving unit orders map is nil")
	}
	if state.TurnRuntime.Resolving.ActiveMarches == nil {
		t.Fatalf("resolving active marches map is nil")
	}
}
