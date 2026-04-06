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

func (t *stubTransport) Send(playerID string, msg proto.Message) error {
	t.mu.Lock()
	defer t.mu.Unlock()
	t.sent[playerID] = append(t.sent[playerID], msg)
	return nil
}

func (t *stubTransport) Broadcast(roomID string, msg proto.Message) error {
	t.mu.Lock()
	defer t.mu.Unlock()
	t.broadcasts[roomID] = append(t.broadcasts[roomID], msg)
	return nil
}

func (t *stubTransport) Stream(playerID string, msgs <-chan proto.Message) error {
	for msg := range msgs {
		if err := t.Send(playerID, msg); err != nil {
			return err
		}
	}
	return nil
}

func (t *botFailingTransport) Send(playerID string, msg proto.Message) error {
	if len(playerID) >= 4 && playerID[:4] == "bot_" {
		return errors.New("client not connected")
	}
	return t.stubTransport.Send(playerID, msg)
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

func TestLobbyServiceReadyUpStartsCountdown(t *testing.T) {
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

	select {
	case startedRoom := <-started:
		if startedRoom.Status != RoomStatusInGame {
			t.Fatalf("started room status = %q", startedRoom.Status)
		}
	case <-time.After(time.Second):
		t.Fatalf("game start callback not called")
	}

	roomAfter, err := store.GetRoom(context.Background(), "room-1")
	if err != nil {
		t.Fatalf("GetRoom() error = %v", err)
	}
	if roomAfter.Status != RoomStatusInGame {
		t.Fatalf("room status = %q", roomAfter.Status)
	}

	msgs := transport.sent["guest-1"]
	if len(msgs) < 3 {
		t.Fatalf("send count = %d", len(msgs))
	}
	if _, ok := msgs[len(msgs)-1].(*pb.MsgGameStarting); !ok {
		t.Fatalf("last send type = %T", msgs[len(msgs)-1])
	}
}

func TestLobbyServiceReadyUpWithBotSkipsBotPushAndStartsCountdown(t *testing.T) {
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
