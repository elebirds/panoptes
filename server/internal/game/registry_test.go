package game

import (
	"testing"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestRegistryRegisterCancelsPreviousRoomForOverlappingPlayer(t *testing.T) {
	registry := NewGameRoomRegistry()
	transport := newStubTransport()

	oldRoom := NewRoom("game-old", []Player{
		NewHumanPlayer("player-1", "alice", transport),
	}, transport, &config.Config{})
	oldCanceled := false
	oldRoom.runtime.SetCancelFunc(func() {
		oldCanceled = true
	})
	registry.Register(oldRoom)

	newRoom := NewRoom("game-new", []Player{
		NewHumanPlayer("player-1", "alice", transport),
	}, transport, &config.Config{})
	registry.Register(newRoom)

	if !oldCanceled {
		t.Fatalf("previous room should be canceled when overlapping player enters a new room")
	}
	if _, exists := registry.rooms["game-old"]; exists {
		t.Fatalf("previous room should be removed from registry.rooms")
	}
	room, ok := registry.GetRoomByPlayerID("player-1")
	if !ok {
		t.Fatalf("player-1 should still resolve to an active room")
	}
	if room != newRoom {
		t.Fatalf("registry room = %p, want %p", room, newRoom)
	}
}

func TestRegistryHandlePlayerDisconnectEndsActiveGame(t *testing.T) {
	registry := NewGameRoomRegistry()
	transport := newStubTransport()
	room := NewRoom("game-1", []Player{
		NewHumanPlayer("player-1", "alice", transport),
		NewHumanPlayer("player-2", "bob", transport),
	}, transport, &config.Config{})
	room.runtime.SetState(domain.NewGameState("game-1", []string{"player-1", "player-2"}, []string{"alice", "bob"}, &domain.MapData{}))

	canceled := false
	room.runtime.SetCancelFunc(func() {
		canceled = true
	})

	registry.Register(room)
	registry.HandlePlayerDisconnect("player-1")

	state := room.State()
	if !state.IsOver {
		t.Fatalf("state.IsOver = false, want true")
	}
	if state.WinnerID != "player-2" {
		t.Fatalf("state.WinnerID = %q, want player-2", state.WinnerID)
	}
	if state.OverReason != "player_disconnected" {
		t.Fatalf("state.OverReason = %q, want player_disconnected", state.OverReason)
	}
	if !canceled {
		t.Fatalf("runtime should be canceled after disconnect forfeit")
	}
	if _, ok := registry.GetRoomByPlayerID("player-1"); ok {
		t.Fatalf("player-1 should be removed from active registry after disconnect forfeit")
	}
	if _, ok := registry.GetRoomByPlayerID("player-2"); ok {
		t.Fatalf("player-2 should be removed from active registry after disconnect forfeit")
	}

	for _, playerID := range []string{"player-1", "player-2"} {
		gameOver := firstMessage[*pb.MsgGameOver](transport.sent[playerID])
		if gameOver == nil {
			t.Fatalf("%s did not receive MsgGameOver: %#v", playerID, transport.sent[playerID])
		}
		if gameOver.GetWinnerId() != "player-2" {
			t.Fatalf("%s game over winner = %q, want player-2", playerID, gameOver.GetWinnerId())
		}
		if gameOver.GetReason() != "player_disconnected" {
			t.Fatalf("%s game over reason = %q, want player_disconnected", playerID, gameOver.GetReason())
		}
	}
}
