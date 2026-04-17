// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证大厅模块的服务编排逻辑。

package lobby

import (
	"context"
	"errors"
	"sync"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/auth"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"google.golang.org/protobuf/proto"
)

type memoryLobbyStore struct {
	mu            sync.Mutex
	rooms         map[string]*Room
	roomByCode    map[string]string
	roomByPlayer  map[string]string
	updateInvoked int
}

func newMemoryLobbyStore() *memoryLobbyStore {
	return &memoryLobbyStore{
		rooms:        make(map[string]*Room),
		roomByCode:   make(map[string]string),
		roomByPlayer: make(map[string]string),
	}
}

func (s *memoryLobbyStore) CreateRoom(_ context.Context, room *Room) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.rooms[room.ID] = cloneRoom(room)
	s.roomByCode[room.Code] = room.ID
	for _, player := range room.Players {
		s.roomByPlayer[player.PlayerID] = room.ID
	}
	return nil
}

func (s *memoryLobbyStore) GetRoom(_ context.Context, roomID string) (*Room, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	room, ok := s.rooms[roomID]
	if !ok {
		return nil, ErrRoomNotFound
	}
	return cloneRoom(room), nil
}

func (s *memoryLobbyStore) GetRoomByCode(_ context.Context, code string) (*Room, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	roomID, ok := s.roomByCode[code]
	if !ok {
		return nil, ErrRoomNotFound
	}
	return cloneRoom(s.rooms[roomID]), nil
}

func (s *memoryLobbyStore) UpdateRoom(_ context.Context, room *Room) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.updateInvoked++
	s.rooms[room.ID] = cloneRoom(room)
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

func (s *memoryLobbyStore) DeleteRoom(_ context.Context, roomID string) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	room, ok := s.rooms[roomID]
	if !ok {
		return ErrRoomNotFound
	}
	delete(s.rooms, roomID)
	delete(s.roomByCode, room.Code)
	for _, player := range room.Players {
		delete(s.roomByPlayer, player.PlayerID)
	}
	return nil
}

func (s *memoryLobbyStore) GetRoomByPlayerID(_ context.Context, playerID string) (*Room, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	roomID, ok := s.roomByPlayer[playerID]
	if !ok {
		return nil, ErrRoomNotFound
	}
	return cloneRoom(s.rooms[roomID]), nil
}

type stubTransport struct {
	mu         sync.Mutex
	sent       map[string][]proto.Message
	broadcasts map[string][]proto.Message
}

type botFailingTransport struct {
	*stubTransport
}

func newStubTransport() *stubTransport {
	return &stubTransport{
		sent:       make(map[string][]proto.Message),
		broadcasts: make(map[string][]proto.Message),
	}
}

func (t *stubTransport) Send(_ context.Context, playerID string, msg proto.Message) error {
	t.mu.Lock()
	defer t.mu.Unlock()
	t.sent[playerID] = append(t.sent[playerID], msg)
	return nil
}

func (t *stubTransport) Broadcast(_ context.Context, roomID string, msg proto.Message) error {
	t.mu.Lock()
	defer t.mu.Unlock()
	t.broadcasts[roomID] = append(t.broadcasts[roomID], msg)
	return nil
}

func (t *stubTransport) Stream(ctx context.Context, playerID string, msgs <-chan proto.Message) error {
	for msg := range msgs {
		if err := t.Send(ctx, playerID, msg); err != nil {
			return err
		}
	}
	return nil
}

func (t *botFailingTransport) Send(ctx context.Context, playerID string, msg proto.Message) error {
	if len(playerID) >= 4 && playerID[:4] == "bot_" {
		return errors.New("client not connected")
	}
	return t.stubTransport.Send(ctx, playerID, msg)
}

type stubUserStore struct {
	users map[string]*auth.User
}

func (s *stubUserStore) Create(context.Context, *auth.User) error {
	return errors.New("not implemented")
}

func (s *stubUserStore) GetByUsername(_ context.Context, username string) (*auth.User, error) {
	for _, user := range s.users {
		if user.Username == username {
			return user, nil
		}
	}
	return nil, auth.ErrUserNotFound
}

func (s *stubUserStore) GetByID(_ context.Context, id string) (*auth.User, error) {
	user, ok := s.users[id]
	if !ok {
		return nil, auth.ErrUserNotFound
	}
	return user, nil
}

func TestLobbyServiceCreateRoomSendsInitialMessages(t *testing.T) {
	store := newMemoryLobbyStore()
	transport := newStubTransport()
	authSvc := auth.NewService(&stubUserStore{
		users: map[string]*auth.User{
			"host-1": {ID: "host-1", Username: "host"},
		},
	}, "secret", 60)

	svc := NewService(store, transport, authSvc, 4, false)

	if err := svc.CreateRoom(context.Background(), "host-1", "第一房间", 0); err != nil {
		t.Fatalf("CreateRoom() error = %v", err)
	}

	if len(transport.sent["host-1"]) != 2 {
		t.Fatalf("host sent count = %d", len(transport.sent["host-1"]))
	}

	created, ok := transport.sent["host-1"][0].(*pb.MsgRoomCreated)
	if !ok {
		t.Fatalf("first message type = %T", transport.sent["host-1"][0])
	}
	if len(created.GetRoomCode()) != 6 {
		t.Fatalf("room code = %q", created.GetRoomCode())
	}

	state, ok := transport.sent["host-1"][1].(*pb.MsgRoomState)
	if !ok {
		t.Fatalf("second message type = %T", transport.sent["host-1"][1])
	}
	if state.GetName() != "第一房间" {
		t.Fatalf("state name = %q", state.GetName())
	}
	if state.GetMaxPlayers() != 4 {
		t.Fatalf("max players = %d", state.GetMaxPlayers())
	}
}

func TestLobbyServiceStartGameStartsCountdownAfterReadyUp(t *testing.T) {
	store := newMemoryLobbyStore()
	transport := newStubTransport()
	authSvc := auth.NewService(&stubUserStore{
		users: map[string]*auth.User{
			"host-1":  {ID: "host-1", Username: "host"},
			"guest-1": {ID: "guest-1", Username: "guest"},
		},
	}, "secret", 60)

	svc := NewService(store, transport, authSvc, 4, false)
	svc.countdownDelay = 10 * time.Millisecond

	room := &Room{
		ID:         "room-1",
		Code:       "ABCD23",
		Name:       "ready-room",
		HostID:     "host-1",
		MaxPlayers: 4,
		Status:     RoomStatusWaiting,
		Players: []*RoomPlayer{
			{PlayerID: "host-1", Username: "host", IsReady: true},
			{PlayerID: "guest-1", Username: "guest"},
		},
	}
	if err := store.CreateRoom(context.Background(), room); err != nil {
		t.Fatalf("CreateRoom() error = %v", err)
	}

	started := make(chan *Room, 1)
	svc.SetGameStartCallback(func(room *Room) {
		started <- room
	})

	if err := svc.ReadyUp(context.Background(), "guest-1"); err != nil {
		t.Fatalf("ReadyUp() error = %v", err)
	}
	if err := svc.StartGame(context.Background(), "host-1"); err != nil {
		t.Fatalf("StartGame() error = %v", err)
	}

	select {
	case startedRoom := <-started:
		if startedRoom.Status != RoomStatusInGame {
			t.Fatalf("started room status = %q", startedRoom.Status)
		}
	case <-time.After(time.Second):
		t.Fatalf("game start callback not called")
	}

	if _, err := store.GetRoom(context.Background(), "room-1"); !errors.Is(err, ErrRoomNotFound) {
		t.Fatalf("GetRoom() error = %v, want %v", err, ErrRoomNotFound)
	}

	msgs := transport.sent["guest-1"]
	if len(msgs) < 3 {
		t.Fatalf("send count = %d", len(msgs))
	}
	if _, ok := msgs[len(msgs)-1].(*pb.MsgGameStarting); !ok {
		t.Fatalf("last send type = %T", msgs[len(msgs)-1])
	}
}

func TestLobbyServiceStartGameWithBotSkipsBotPushAndStartsCountdown(t *testing.T) {
	store := newMemoryLobbyStore()
	transport := &botFailingTransport{stubTransport: newStubTransport()}
	authSvc := auth.NewService(&stubUserStore{
		users: map[string]*auth.User{
			"host-1": {ID: "host-1", Username: "host"},
		},
	}, "secret", 60)

	svc := NewService(store, transport, authSvc, 4, true)
	svc.countdownDelay = 10 * time.Millisecond

	room := NewRoom("room-1", "ABCD23", "bot-ready-room", "host-1", "host", 4, true)
	if _, err := room.AddBot(); err != nil {
		t.Fatalf("AddBot() error = %v", err)
	}
	if err := store.CreateRoom(context.Background(), room); err != nil {
		t.Fatalf("CreateRoom() error = %v", err)
	}

	started := make(chan *Room, 1)
	svc.SetGameStartCallback(func(room *Room) {
		started <- room
	})

	if err := svc.ReadyUp(context.Background(), "host-1"); err != nil {
		t.Fatalf("ReadyUp() error = %v", err)
	}
	if err := svc.StartGame(context.Background(), "host-1"); err != nil {
		t.Fatalf("StartGame() error = %v", err)
	}

	select {
	case startedRoom := <-started:
		if startedRoom.Status != RoomStatusInGame {
			t.Fatalf("started room status = %q", startedRoom.Status)
		}
	case <-time.After(time.Second):
		t.Fatalf("game start callback not called")
	}

	msgs := transport.sent["host-1"]
	if len(msgs) < 3 {
		t.Fatalf("host send count = %d", len(msgs))
	}
	if _, ok := msgs[len(msgs)-1].(*pb.MsgGameStarting); !ok {
		t.Fatalf("last send type = %T", msgs[len(msgs)-1])
	}
}

func TestLobbyServiceReadyUpAllReadyDoesNotStartAutomatically(t *testing.T) {
	store := newMemoryLobbyStore()
	transport := newStubTransport()
	authSvc := auth.NewService(&stubUserStore{
		users: map[string]*auth.User{
			"host-1":  {ID: "host-1", Username: "host"},
			"guest-1": {ID: "guest-1", Username: "guest"},
		},
	}, "secret", 60)

	svc := NewService(store, transport, authSvc, 4, false)
	svc.countdownDelay = 10 * time.Millisecond

	room := &Room{
		ID:         "room-auto-off",
		Code:       "ABCD24",
		Name:       "manual-room",
		HostID:     "host-1",
		MaxPlayers: 4,
		Status:     RoomStatusWaiting,
		Players: []*RoomPlayer{
			{PlayerID: "host-1", Username: "host", IsReady: true},
			{PlayerID: "guest-1", Username: "guest"},
		},
	}
	if err := store.CreateRoom(context.Background(), room); err != nil {
		t.Fatalf("CreateRoom() error = %v", err)
	}

	started := make(chan *Room, 1)
	svc.SetGameStartCallback(func(room *Room) {
		started <- room
	})

	if err := svc.ReadyUp(context.Background(), "guest-1"); err != nil {
		t.Fatalf("ReadyUp() error = %v", err)
	}

	select {
	case startedRoom := <-started:
		t.Fatalf("game should not auto start, got room status %q", startedRoom.Status)
	case <-time.After(50 * time.Millisecond):
	}

	roomAfter, err := store.GetRoom(context.Background(), "room-auto-off")
	if err != nil {
		t.Fatalf("GetRoom() error = %v", err)
	}
	if roomAfter.Status != RoomStatusReady {
		t.Fatalf("room status = %q", roomAfter.Status)
	}

	for _, msg := range transport.sent["guest-1"] {
		if _, ok := msg.(*pb.MsgGameStarting); ok {
			t.Fatalf("guest should not receive MsgGameStarting")
		}
	}
}

func TestLobbyServiceStartGameStartsCountdownWhenHostConfirms(t *testing.T) {
	store := newMemoryLobbyStore()
	transport := newStubTransport()
	authSvc := auth.NewService(&stubUserStore{
		users: map[string]*auth.User{
			"host-1":  {ID: "host-1", Username: "host"},
			"guest-1": {ID: "guest-1", Username: "guest"},
		},
	}, "secret", 60)

	svc := NewService(store, transport, authSvc, 4, false)
	svc.countdownDelay = 10 * time.Millisecond

	room := &Room{
		ID:         "room-start-btn",
		Code:       "ABCD25",
		Name:       "manual-start-room",
		HostID:     "host-1",
		MaxPlayers: 4,
		Status:     RoomStatusReady,
		Players: []*RoomPlayer{
			{PlayerID: "host-1", Username: "host", IsReady: true},
			{PlayerID: "guest-1", Username: "guest", IsReady: true},
		},
	}
	if err := store.CreateRoom(context.Background(), room); err != nil {
		t.Fatalf("CreateRoom() error = %v", err)
	}

	started := make(chan *Room, 1)
	svc.SetGameStartCallback(func(room *Room) {
		started <- room
	})

	if err := svc.StartGame(context.Background(), "host-1"); err != nil {
		t.Fatalf("StartGame() error = %v", err)
	}

	select {
	case startedRoom := <-started:
		if startedRoom.Status != RoomStatusInGame {
			t.Fatalf("started room status = %q", startedRoom.Status)
		}
	case <-time.After(time.Second):
		t.Fatalf("game start callback not called")
	}

	msgs := transport.sent["guest-1"]
	foundStarting := false
	for _, msg := range msgs {
		if _, ok := msg.(*pb.MsgGameStarting); ok {
			foundStarting = true
			break
		}
	}
	if !foundStarting {
		t.Fatalf("guest should receive MsgGameStarting")
	}
}

func TestLobbyServiceStartGameRemovesLobbyMembershipAfterCountdown(t *testing.T) {
	store := newMemoryLobbyStore()
	transport := newStubTransport()
	authSvc := auth.NewService(&stubUserStore{
		users: map[string]*auth.User{
			"host-1":  {ID: "host-1", Username: "host"},
			"guest-1": {ID: "guest-1", Username: "guest"},
		},
	}, "secret", 60)

	svc := NewService(store, transport, authSvc, 4, false)
	svc.countdownDelay = 10 * time.Millisecond

	room := &Room{
		ID:         "room-start-cleanup",
		Code:       "ABCD26",
		Name:       "cleanup-room",
		HostID:     "host-1",
		MaxPlayers: 4,
		Status:     RoomStatusReady,
		Players: []*RoomPlayer{
			{PlayerID: "host-1", Username: "host", IsReady: true},
			{PlayerID: "guest-1", Username: "guest", IsReady: true},
		},
	}
	if err := store.CreateRoom(context.Background(), room); err != nil {
		t.Fatalf("CreateRoom() error = %v", err)
	}

	started := make(chan *Room, 1)
	svc.SetGameStartCallback(func(room *Room) {
		started <- room
	})

	if err := svc.StartGame(context.Background(), "host-1"); err != nil {
		t.Fatalf("StartGame() error = %v", err)
	}

	select {
	case <-started:
	case <-time.After(time.Second):
		t.Fatalf("game start callback not called")
	}

	if _, err := store.GetRoom(context.Background(), "room-start-cleanup"); !errors.Is(err, ErrRoomNotFound) {
		t.Fatalf("GetRoom() error = %v, want %v", err, ErrRoomNotFound)
	}
	if _, err := store.GetRoomByPlayerID(context.Background(), "host-1"); !errors.Is(err, ErrRoomNotFound) {
		t.Fatalf("GetRoomByPlayerID(host) error = %v, want %v", err, ErrRoomNotFound)
	}
	if _, err := store.GetRoomByPlayerID(context.Background(), "guest-1"); !errors.Is(err, ErrRoomNotFound) {
		t.Fatalf("GetRoomByPlayerID(guest) error = %v, want %v", err, ErrRoomNotFound)
	}
}

func cloneRoom(room *Room) *Room {
	if room == nil {
		return nil
	}

	cloned := *room
	cloned.Players = make([]*RoomPlayer, 0, len(room.Players))
	for _, player := range room.Players {
		playerCopy := *player
		cloned.Players = append(cloned.Players, &playerCopy)
	}
	return &cloned
}
