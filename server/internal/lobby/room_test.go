package lobby

import (
	"testing"
	"time"
)

func TestRoomAddPlayerAndToProto(t *testing.T) {
	room := &Room{
		ID:         "room-1",
		Code:       "ABC234",
		Name:       "测试房间",
		HostID:     "host-1",
		MaxPlayers: 2,
		Status:     RoomStatusWaiting,
		Players: []*RoomPlayer{
			{
				PlayerID: "host-1",
				Username: "host",
				JoinedAt: time.Unix(1, 0),
			},
		},
	}

	if err := room.AddPlayer("guest-1", "guest"); err != nil {
		t.Fatalf("AddPlayer() error = %v", err)
	}

	if got, ok := room.GetPlayer("guest-1"); !ok || got.Username != "guest" {
		t.Fatalf("GetPlayer() = %#v, %v", got, ok)
	}

	state := room.ToProto()
	if state.GetRoomId() != "room-1" {
		t.Fatalf("room id = %q", state.GetRoomId())
	}
	if state.GetName() != "测试房间" {
		t.Fatalf("name = %q", state.GetName())
	}
	if state.GetMaxPlayers() != 2 {
		t.Fatalf("max players = %d", state.GetMaxPlayers())
	}
	if len(state.GetPlayers()) != 2 {
		t.Fatalf("players len = %d", len(state.GetPlayers()))
	}
	if !state.GetPlayers()[0].GetIsHost() {
		t.Fatalf("host flag = false")
	}
}

func TestRoomAddPlayerRejectsDuplicateAndFull(t *testing.T) {
	room := &Room{
		HostID:     "host-1",
		MaxPlayers: 2,
		Players: []*RoomPlayer{
			{PlayerID: "host-1", Username: "host"},
		},
	}

	if err := room.AddPlayer("host-1", "host"); err != ErrAlreadyInRoom {
		t.Fatalf("duplicate error = %v", err)
	}

	if err := room.AddPlayer("guest-1", "guest"); err != nil {
		t.Fatalf("AddPlayer() error = %v", err)
	}

	if err := room.AddPlayer("guest-2", "guest2"); err != ErrRoomFull {
		t.Fatalf("full error = %v", err)
	}
}

func TestRoomReadyState(t *testing.T) {
	room := &Room{
		HostID: "host-1",
		Players: []*RoomPlayer{
			{PlayerID: "host-1", Username: "host"},
			{PlayerID: "guest-1", Username: "guest"},
		},
	}

	if room.IsAllReady() {
		t.Fatalf("expected not all ready")
	}

	if err := room.SetReady("host-1", true); err != nil {
		t.Fatalf("SetReady(host) error = %v", err)
	}
	if room.IsAllReady() {
		t.Fatalf("expected not all ready with one player ready")
	}

	if err := room.SetReady("guest-1", true); err != nil {
		t.Fatalf("SetReady(guest) error = %v", err)
	}
	if !room.IsAllReady() {
		t.Fatalf("expected all ready")
	}

	room.RemovePlayer("guest-1")
	if room.IsAllReady() {
		t.Fatalf("expected false after player removal")
	}
}

func TestRoomSetReadyMissingPlayer(t *testing.T) {
	room := &Room{}
	if err := room.SetReady("missing", true); err != ErrPlayerNotFound {
		t.Fatalf("SetReady() error = %v", err)
	}
}
