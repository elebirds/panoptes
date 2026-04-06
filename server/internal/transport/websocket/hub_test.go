package websocket

import (
	"context"
	"sync/atomic"
	"testing"
	"time"
)

func TestHubSetRoom(t *testing.T) {
	hub := NewHub("secret")
	hub.clients["player-1"] = &Client{playerID: "player-1"}

	hub.SetRoom("player-1", "room-1")

	if hub.clients["player-1"].roomID != "room-1" {
		t.Fatalf("roomID = %q", hub.clients["player-1"].roomID)
	}
}

func TestHubUnregisterCallsLeaveRoom(t *testing.T) {
	hub := NewHub("secret")
	var called atomic.Int32
	done := make(chan struct{}, 1)
	hub.SetLeaveRoomFunc(func(_ context.Context, playerID string) error {
		if playerID != "player-1" {
			t.Fatalf("playerID = %q", playerID)
		}
		called.Add(1)
		done <- struct{}{}
		return nil
	})

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()
	go hub.Run(ctx)

	client := &Client{playerID: "player-1"}
	hub.register <- client
	hub.unregister <- client

	select {
	case <-done:
	case <-time.After(time.Second):
		t.Fatalf("leave room callback not invoked")
	}

	if called.Load() != 1 {
		t.Fatalf("leave room calls = %d", called.Load())
	}
}
