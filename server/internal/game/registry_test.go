package game

import (
	"testing"

	"github.com/elebirds/panoptes/internal/config"
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
