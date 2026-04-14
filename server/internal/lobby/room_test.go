// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证大厅模块的房间模型与编排逻辑。

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

func TestRoomAddBotAssignsIdentityAndReadyState(t *testing.T) {
	room := NewRoom("room-1", "ABC234", "测试房间", "host-1", "host", 3, false)

	firstBot, err := room.AddBot()
	if err != nil {
		t.Fatalf("AddBot() first error = %v", err)
	}
	secondBot, err := room.AddBot()
	if err != nil {
		t.Fatalf("AddBot() second error = %v", err)
	}

	if firstBot.Username != "Bot" {
		t.Fatalf("first bot username = %q", firstBot.Username)
	}
	if secondBot.Username != "Bot 2" {
		t.Fatalf("second bot username = %q", secondBot.Username)
	}
	if !firstBot.IsBot || !firstBot.IsReady {
		t.Fatalf("first bot flags = %#v", firstBot)
	}
	if !secondBot.IsBot || !secondBot.IsReady {
		t.Fatalf("second bot flags = %#v", secondBot)
	}
	if len(firstBot.PlayerID) != len("bot_")+8 {
		t.Fatalf("first bot id = %q", firstBot.PlayerID)
	}
	if err := room.AddPlayer("guest-1", "guest"); err != ErrRoomFull {
		t.Fatalf("AddPlayer() after full room error = %v", err)
	}

	state := room.ToProto()
	if !state.GetPlayers()[1].GetIsBot() {
		t.Fatalf("first bot proto flag = false")
	}
	if !state.GetPlayers()[2].GetIsBot() {
		t.Fatalf("second bot proto flag = false")
	}
}

func TestRoomKickPlayerValidatesOperatorAndTarget(t *testing.T) {
	room := NewRoom("room-1", "ABC234", "测试房间", "host-1", "host", 4, false)
	if err := room.AddPlayer("guest-1", "guest"); err != nil {
		t.Fatalf("AddPlayer() error = %v", err)
	}
	bot, err := room.AddBot()
	if err != nil {
		t.Fatalf("AddBot() error = %v", err)
	}

	if _, err := room.KickPlayer("guest-1", bot.PlayerID); err != ErrNotHost {
		t.Fatalf("non-host KickPlayer() error = %v", err)
	}
	if _, err := room.KickPlayer("host-1", "host-1"); err != ErrInvalidStatus {
		t.Fatalf("host self KickPlayer() error = %v", err)
	}
	if _, err := room.KickPlayer("host-1", "missing"); err != ErrPlayerNotFound {
		t.Fatalf("missing KickPlayer() error = %v", err)
	}

	kicked, err := room.KickPlayer("host-1", "guest-1")
	if err != nil {
		t.Fatalf("KickPlayer() error = %v", err)
	}
	if kicked == nil || kicked.PlayerID != "guest-1" {
		t.Fatalf("kicked player = %#v", kicked)
	}
	if _, ok := room.GetPlayer("guest-1"); ok {
		t.Fatalf("guest still in room")
	}
}

func TestRoomIsAllReadyHonorsDevMode(t *testing.T) {
	devRoom := NewRoom("room-1", "ABC234", "测试房间", "host-1", "host", 2, true)
	if err := devRoom.SetReady("host-1", true); err != nil {
		t.Fatalf("SetReady() dev room error = %v", err)
	}
	if !devRoom.IsAllReady() {
		t.Fatalf("expected dev room to allow single ready player")
	}

	normalRoom := NewRoom("room-2", "ABC235", "普通房间", "host-2", "host", 2, false)
	if err := normalRoom.SetReady("host-2", true); err != nil {
		t.Fatalf("SetReady() normal room error = %v", err)
	}
	if normalRoom.IsAllReady() {
		t.Fatalf("expected normal room to require at least two players")
	}
}
