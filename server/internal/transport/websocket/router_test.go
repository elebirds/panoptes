// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证WebSocket 传输层的消息路由逻辑。

package websocket

import (
	"context"
	"sync"
	"testing"

	"github.com/elebirds/panoptes/internal/auth"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/lobby"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/proto"
)

type captureSender struct {
	payloads [][]byte
}

func (s *captureSender) Send(data []byte) error {
	s.payloads = append(s.payloads, data)
	return nil
}

func TestRouterRouteCreateRoom(t *testing.T) {
	store := newRouterStore()
	transport := newRouterTransport()
	authSvc := auth.NewService(&routerUserStore{
		users: map[string]*auth.User{
			"host-1": {ID: "host-1", Username: "host"},
		},
	}, "secret", 60)
	router := NewRouter(lobby.NewService(store, transport, authSvc, 4, false), nil)

	payload, err := protojson.Marshal(&pb.MsgCreateRoom{
		Name:       "房间A",
		MaxPlayers: 3,
	})
	if err != nil {
		t.Fatalf("Marshal() error = %v", err)
	}

	sender := &captureSender{}
	router.Route(sender, "host-1", &pb.Envelope{Type: "MsgCreateRoom", Payload: string(payload)})

	if len(transport.sent["host-1"]) != 2 {
		t.Fatalf("host sent count = %d", len(transport.sent["host-1"]))
	}
}

func TestRouterRouteServiceErrorReturnsLobbyError(t *testing.T) {
	router := NewRouter(lobby.NewService(newRouterStore(), newRouterTransport(), auth.NewService(&routerUserStore{
		users: map[string]*auth.User{},
	}, "secret", 60), 4, false), nil)

	payload, err := protojson.Marshal(&pb.MsgLeaveRoom{})
	if err != nil {
		t.Fatalf("Marshal() error = %v", err)
	}

	sender := &captureSender{}
	router.Route(sender, "missing-player", &pb.Envelope{Type: "MsgLeaveRoom", Payload: string(payload)})

	if len(sender.payloads) != 1 {
		t.Fatalf("sender payload count = %d", len(sender.payloads))
	}

	envelope := &pb.Envelope{}
	if err := protojson.Unmarshal(sender.payloads[0], envelope); err != nil {
		t.Fatalf("Unmarshal envelope error = %v", err)
	}
	if envelope.GetType() != "MsgLobbyError" {
		t.Fatalf("envelope type = %q", envelope.GetType())
	}

	msg := &pb.MsgLobbyError{}
	if err := protojson.Unmarshal([]byte(envelope.GetPayload()), msg); err != nil {
		t.Fatalf("Unmarshal payload error = %v", err)
	}
	if msg.GetCode() != "room_not_found" {
		t.Fatalf("error code = %q", msg.GetCode())
	}
}

type routerStore struct {
	mu           sync.Mutex
	rooms        map[string]*lobby.Room
	roomByCode   map[string]string
	roomByPlayer map[string]string
}

func newRouterStore() *routerStore {
	return &routerStore{
		rooms:        make(map[string]*lobby.Room),
		roomByCode:   make(map[string]string),
		roomByPlayer: make(map[string]string),
	}
}

func (s *routerStore) CreateRoom(_ context.Context, room *lobby.Room) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.rooms[room.ID] = cloneLobbyRoom(room)
	s.roomByCode[room.Code] = room.ID
	for _, player := range room.Players {
		s.roomByPlayer[player.PlayerID] = room.ID
	}
	return nil
}

func (s *routerStore) GetRoom(_ context.Context, roomID string) (*lobby.Room, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	room, ok := s.rooms[roomID]
	if !ok {
		return nil, lobby.ErrRoomNotFound
	}
	return cloneLobbyRoom(room), nil
}

func (s *routerStore) GetRoomByCode(_ context.Context, code string) (*lobby.Room, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	roomID, ok := s.roomByCode[code]
	if !ok {
		return nil, lobby.ErrRoomNotFound
	}
	return cloneLobbyRoom(s.rooms[roomID]), nil
}

func (s *routerStore) UpdateRoom(_ context.Context, room *lobby.Room) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.rooms[room.ID] = cloneLobbyRoom(room)
	s.roomByCode[room.Code] = room.ID
	for playerID, mappedRoomID := range s.roomByPlayer {
		if mappedRoomID == room.ID {
			delete(s.roomByPlayer, playerID)
		}
	}
	for _, player := range room.Players {
		s.roomByPlayer[player.PlayerID] = room.ID
	}
	return nil
}

func (s *routerStore) DeleteRoom(_ context.Context, roomID string) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	room, ok := s.rooms[roomID]
	if !ok {
		return lobby.ErrRoomNotFound
	}
	delete(s.rooms, roomID)
	delete(s.roomByCode, room.Code)
	for _, player := range room.Players {
		delete(s.roomByPlayer, player.PlayerID)
	}
	return nil
}

func (s *routerStore) GetRoomByPlayerID(_ context.Context, playerID string) (*lobby.Room, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	roomID, ok := s.roomByPlayer[playerID]
	if !ok {
		return nil, lobby.ErrRoomNotFound
	}
	return cloneLobbyRoom(s.rooms[roomID]), nil
}

type routerTransport struct {
	mu         sync.Mutex
	sent       map[string][]proto.Message
	broadcasts map[string][]proto.Message
}

func newRouterTransport() *routerTransport {
	return &routerTransport{
		sent:       make(map[string][]proto.Message),
		broadcasts: make(map[string][]proto.Message),
	}
}

func (t *routerTransport) Send(playerID string, msg proto.Message) error {
	t.mu.Lock()
	defer t.mu.Unlock()
	t.sent[playerID] = append(t.sent[playerID], msg)
	return nil
}

func (t *routerTransport) Broadcast(roomID string, msg proto.Message) error {
	t.mu.Lock()
	defer t.mu.Unlock()
	t.broadcasts[roomID] = append(t.broadcasts[roomID], msg)
	return nil
}

func (t *routerTransport) Stream(playerID string, msgs <-chan proto.Message) error {
	for msg := range msgs {
		if err := t.Send(playerID, msg); err != nil {
			return err
		}
	}
	return nil
}

type routerUserStore struct {
	users map[string]*auth.User
}

func (s *routerUserStore) Create(context.Context, *auth.User) error { return nil }
func (s *routerUserStore) GetByUsername(context.Context, string) (*auth.User, error) {
	return nil, auth.ErrUserNotFound
}
func (s *routerUserStore) GetByID(_ context.Context, id string) (*auth.User, error) {
	user, ok := s.users[id]
	if !ok {
		return nil, auth.ErrUserNotFound
	}
	return user, nil
}

func cloneLobbyRoom(room *lobby.Room) *lobby.Room {
	if room == nil {
		return nil
	}

	cloned := *room
	cloned.Players = make([]*lobby.RoomPlayer, 0, len(room.Players))
	for _, player := range room.Players {
		playerCopy := *player
		cloned.Players = append(cloned.Players, &playerCopy)
	}
	return &cloned
}
