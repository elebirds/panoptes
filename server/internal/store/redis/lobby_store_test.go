package redis

import (
	"context"
	"testing"
	"time"

	"github.com/alicebob/miniredis/v2"
	"github.com/elebirds/panoptes/internal/lobby"
	goredis "github.com/redis/go-redis/v9"
)

func TestLobbyStoreCRUD(t *testing.T) {
	mr, err := miniredis.Run()
	if err != nil {
		t.Fatalf("miniredis.Run() error = %v", err)
	}
	defer mr.Close()

	client := &Client{Client: goredis.NewClient(&goredis.Options{Addr: mr.Addr()})}
	store := NewLobbyStore(client)

	room := &lobby.Room{
		ID:         "room-1",
		Code:       "ABC234",
		Name:       "redis-room",
		HostID:     "host-1",
		MaxPlayers: 4,
		Status:     lobby.RoomStatusWaiting,
		CreatedAt:  time.Unix(10, 0),
		Players: []*lobby.RoomPlayer{
			{PlayerID: "host-1", Username: "host", JoinedAt: time.Unix(10, 0)},
			{PlayerID: "guest-1", Username: "guest", JoinedAt: time.Unix(11, 0)},
		},
	}

	ctx := context.Background()
	if err := store.CreateRoom(ctx, room); err != nil {
		t.Fatalf("CreateRoom() error = %v", err)
	}

	byID, err := store.GetRoom(ctx, "room-1")
	if err != nil {
		t.Fatalf("GetRoom() error = %v", err)
	}
	if byID.Name != "redis-room" {
		t.Fatalf("room name = %q", byID.Name)
	}

	byCode, err := store.GetRoomByCode(ctx, "ABC234")
	if err != nil {
		t.Fatalf("GetRoomByCode() error = %v", err)
	}
	if byCode.ID != "room-1" {
		t.Fatalf("room id by code = %q", byCode.ID)
	}

	byPlayer, err := store.GetRoomByPlayerID(ctx, "guest-1")
	if err != nil {
		t.Fatalf("GetRoomByPlayerID() error = %v", err)
	}
	if byPlayer.ID != "room-1" {
		t.Fatalf("room id by player = %q", byPlayer.ID)
	}

	room.Players = room.Players[:1]
	if err := store.UpdateRoom(ctx, room); err != nil {
		t.Fatalf("UpdateRoom() error = %v", err)
	}

	if _, err := store.GetRoomByPlayerID(ctx, "guest-1"); err != lobby.ErrRoomNotFound {
		t.Fatalf("GetRoomByPlayerID() error = %v", err)
	}

	if err := store.DeleteRoom(ctx, "room-1"); err != nil {
		t.Fatalf("DeleteRoom() error = %v", err)
	}

	if _, err := store.GetRoom(ctx, "room-1"); err != lobby.ErrRoomNotFound {
		t.Fatalf("GetRoom() error = %v", err)
	}
}
