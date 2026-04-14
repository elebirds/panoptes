// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14
// Description: 验证 WebSocket 路由对 Turn V2 提交与错误分发的处理。

package websocket

import (
	"context"
	"errors"
	"testing"

	"github.com/elebirds/panoptes/internal/auth"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/lobby"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	"google.golang.org/protobuf/encoding/protojson"
)

type stubGameRoom struct {
	submits  []string
	messages []string
}

func (r *stubGameRoom) OnHumanSubmitTurn(playerID string) {
	r.submits = append(r.submits, playerID)
}

func (r *stubGameRoom) OnHumanMessage(_ string, msgType string, _ []byte) error {
	r.messages = append(r.messages, msgType)
	return nil
}

type stubGameRoomWithSubmitError struct {
	stubGameRoom
	submitErr error
}

func (r *stubGameRoomWithSubmitError) OnHumanSubmitTurnChecked(playerID string) error {
	r.submits = append(r.submits, playerID)
	return r.submitErr
}

type stubGameRoomRegistry struct {
	room coretransport.GameRoom
	ok   bool
}

func (r *stubGameRoomRegistry) GetRoomByPlayerID(string) (coretransport.GameRoom, bool) {
	return r.room, r.ok
}

func TestRouterRouteAddBotStartGameAndKickPlayer(t *testing.T) {
	store := newRouterStore()
	transport := newRouterTransport()
	authSvc := auth.NewService(&routerUserStore{
		users: map[string]*auth.User{
			"host-1":  {ID: "host-1", Username: "host"},
			"guest-1": {ID: "guest-1", Username: "guest"},
		},
	}, "secret", 60)
	lobbySvc := lobby.NewService(store, transport, authSvc, 4, false)
	room := lobby.NewRoom("room-1", "ABCD23", "router-room", "host-1", "host", 4, false)
	if err := room.AddPlayer("guest-1", "guest"); err != nil {
		t.Fatalf("AddPlayer() error = %v", err)
	}
	if err := store.CreateRoom(context.Background(), room); err != nil {
		t.Fatalf("CreateRoom() error = %v", err)
	}

	router := NewRouter(lobbySvc, &stubGameRoomRegistry{})
	sender := &captureSender{}

	router.Route(sender, "host-1", &pb.Envelope{Type: "MsgAddBot", Payload: "{}"})
	if len(transport.sent["host-1"]) != 1 {
		t.Fatalf("host sent count after add bot = %d", len(transport.sent["host-1"]))
	}

	room, err := store.GetRoom(context.Background(), room.ID)
	if err != nil {
		t.Fatalf("GetRoom() error = %v", err)
	}
	for _, player := range room.Players {
		player.IsReady = true
	}
	room.Status = lobby.RoomStatusReady
	if err := store.UpdateRoom(context.Background(), room); err != nil {
		t.Fatalf("UpdateRoom() error = %v", err)
	}
	router.Route(sender, "host-1", &pb.Envelope{Type: "MsgStartGame", Payload: "{}"})
	if len(transport.sent["host-1"]) < 2 {
		t.Fatalf("host sent count after start game = %d", len(transport.sent["host-1"]))
	}

	payload, err := protojson.Marshal(&pb.MsgKickPlayer{PlayerId: "guest-1"})
	if err != nil {
		t.Fatalf("Marshal() error = %v", err)
	}
	router.Route(sender, "host-1", &pb.Envelope{Type: "MsgKickPlayer", Payload: string(payload)})

	guestMsgs := transport.sent["guest-1"]
	if len(guestMsgs) == 0 {
		t.Fatalf("guest should receive kick notification")
	}
	if _, ok := guestMsgs[len(guestMsgs)-1].(*pb.MsgPlayerKicked); !ok {
		t.Fatalf("guest last message type = %T", guestMsgs[len(guestMsgs)-1])
	}
}

func TestRouterRouteSubmitMessages(t *testing.T) {
	store := newRouterStore()
	transport := newRouterTransport()
	authSvc := auth.NewService(&routerUserStore{users: map[string]*auth.User{}}, "secret", 60)
	room := &stubGameRoom{}
	router := NewRouter(lobby.NewService(store, transport, authSvc, 4, false), &stubGameRoomRegistry{
		room: room,
		ok:   true,
	})

	router.Route(&captureSender{}, "player-1", &pb.Envelope{Type: "MsgSubmitTurn", Payload: "{}"})

	if len(room.submits) != 1 || room.submits[0] != "player-1" {
		t.Fatalf("submits = %#v", room.submits)
	}
}

func TestRouterRouteUnknownSubmitMessageIsIgnored(t *testing.T) {
	store := newRouterStore()
	transport := newRouterTransport()
	authSvc := auth.NewService(&routerUserStore{users: map[string]*auth.User{}}, "secret", 60)
	room := &stubGameRoom{}
	router := NewRouter(lobby.NewService(store, transport, authSvc, 4, false), &stubGameRoomRegistry{
		room: room,
		ok:   true,
	})

	router.Route(&captureSender{}, "player-1", &pb.Envelope{Type: "MsgSubmitUnknown", Payload: "{}"})

	if len(room.submits) != 0 {
		t.Fatalf("unknown submit should not route, got %#v", room.submits)
	}
}

func TestRouterRouteSubmitTurnReturnsGameErrorResponse(t *testing.T) {
	store := newRouterStore()
	transport := newRouterTransport()
	authSvc := auth.NewService(&routerUserStore{users: map[string]*auth.User{}}, "secret", 60)
	room := &stubGameRoomWithSubmitError{
		submitErr: errors.New("phase_mismatch"),
	}
	router := NewRouter(lobby.NewService(store, transport, authSvc, 4, false), &stubGameRoomRegistry{
		room: room,
		ok:   true,
	})

	sender := &captureSender{}
	router.Route(sender, "player-1", &pb.Envelope{Type: "MsgSubmitTurn", Payload: "{}"})

	if len(sender.payloads) != 1 {
		t.Fatalf("sender payload count = %d", len(sender.payloads))
	}

	envelope := &pb.Envelope{}
	if err := protojson.Unmarshal(sender.payloads[0], envelope); err != nil {
		t.Fatalf("Unmarshal envelope error = %v", err)
	}
	if envelope.GetType() != "ErrorResponse" {
		t.Fatalf("envelope type = %q", envelope.GetType())
	}

	msg := &pb.ErrorResponse{}
	if err := protojson.Unmarshal([]byte(envelope.GetPayload()), msg); err != nil {
		t.Fatalf("Unmarshal payload error = %v", err)
	}
	if msg.GetCode() != "phase_mismatch" {
		t.Fatalf("error code = %q", msg.GetCode())
	}
}
