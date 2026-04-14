// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证大厅模块的机器人服务协同行为。

package lobby

import (
	"context"
	"testing"

	"github.com/elebirds/panoptes/internal/auth"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestLobbyServiceAddBotBroadcastsUpdatedRoomState(t *testing.T) {
	store := newMemoryLobbyStore()
	transport := newStubTransport()
	authSvc := auth.NewService(&stubUserStore{
		users: map[string]*auth.User{
			"host-1": {ID: "host-1", Username: "host"},
		},
	}, "secret", 60)

	svc := NewService(store, transport, authSvc, 4, false)
	room := NewRoom("room-1", "ABCD23", "bot-room", "host-1", "host", 4, false)
	if err := store.CreateRoom(context.Background(), room); err != nil {
		t.Fatalf("CreateRoom() error = %v", err)
	}

	if err := svc.AddBot(context.Background(), "host-1"); err != nil {
		t.Fatalf("AddBot() error = %v", err)
	}

	msgs := transport.sent["host-1"]
	if len(msgs) != 1 {
		t.Fatalf("host send count = %d", len(msgs))
	}

	state, ok := msgs[0].(*pb.MsgRoomState)
	if !ok {
		t.Fatalf("message type = %T", msgs[0])
	}
	if len(state.GetPlayers()) != 2 {
		t.Fatalf("players len = %d", len(state.GetPlayers()))
	}
	if !state.GetPlayers()[1].GetIsBot() || !state.GetPlayers()[1].GetIsReady() {
		t.Fatalf("bot player state = %#v", state.GetPlayers()[1])
	}
}

func TestLobbyServiceKickPlayerNotifiesHumanTarget(t *testing.T) {
	store := newMemoryLobbyStore()
	transport := newStubTransport()
	authSvc := auth.NewService(&stubUserStore{
		users: map[string]*auth.User{
			"host-1":  {ID: "host-1", Username: "host"},
			"guest-1": {ID: "guest-1", Username: "guest"},
		},
	}, "secret", 60)

	svc := NewService(store, transport, authSvc, 4, false)
	room := NewRoom("room-1", "ABCD23", "kick-room", "host-1", "host", 4, false)
	if err := room.AddPlayer("guest-1", "guest"); err != nil {
		t.Fatalf("AddPlayer() error = %v", err)
	}
	room.Status = RoomStatusReady
	if err := store.CreateRoom(context.Background(), room); err != nil {
		t.Fatalf("CreateRoom() error = %v", err)
	}

	if err := svc.KickPlayer(context.Background(), "host-1", "guest-1"); err != nil {
		t.Fatalf("KickPlayer() error = %v", err)
	}

	targetMsgs := transport.sent["guest-1"]
	if len(targetMsgs) != 1 {
		t.Fatalf("guest send count = %d", len(targetMsgs))
	}
	kicked, ok := targetMsgs[0].(*pb.MsgPlayerKicked)
	if !ok {
		t.Fatalf("guest message type = %T", targetMsgs[0])
	}
	if kicked.GetPlayerId() != "guest-1" || kicked.GetUsername() != "guest" {
		t.Fatalf("kicked payload = %#v", kicked)
	}

	hostMsgs := transport.sent["host-1"]
	if len(hostMsgs) != 1 {
		t.Fatalf("host send count = %d", len(hostMsgs))
	}
	state, ok := hostMsgs[0].(*pb.MsgRoomState)
	if !ok {
		t.Fatalf("host message type = %T", hostMsgs[0])
	}
	if len(state.GetPlayers()) != 1 || state.GetPlayers()[0].GetPlayerId() != "host-1" {
		t.Fatalf("room state players = %#v", state.GetPlayers())
	}
	if state.GetStatus() != string(RoomStatusWaiting) {
		t.Fatalf("room status = %q", state.GetStatus())
	}
}
