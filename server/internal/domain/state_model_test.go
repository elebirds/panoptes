package domain

import "testing"

func TestNewGameStateInitializesStructuredStateModel(t *testing.T) {
	state := NewGameState("game-1", []string{"player-1"}, []string{"alice"}, nil)

	if state.Meta.GameID != state.GameID {
		t.Fatalf("meta game id = %q, want %q", state.Meta.GameID, state.GameID)
	}
	if state.Clock.Turn != state.Turn || state.Clock.Phase != state.Phase {
		t.Fatalf("clock = %+v, want turn=%d phase=%s", state.Clock, state.Turn, state.Phase)
	}
	if state.Outcome.IsOver != state.IsOver || state.Outcome.WinnerID != state.WinnerID {
		t.Fatalf("outcome = %+v, want legacy fields", state.Outcome)
	}
	if state.PlayerStore.Players["player-1"] != state.Players["player-1"] {
		t.Fatalf("player store does not share player map with legacy field")
	}
	if state.Runtime.Planning != &state.TurnRuntime.Planning {
		t.Fatalf("runtime planning does not point at legacy planning inputs")
	}
	if state.Runtime.Resolving != &state.TurnRuntime.Resolving {
		t.Fatalf("runtime resolving does not point at legacy resolving state")
	}
}
