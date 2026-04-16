// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的注册表与索引管理。

package game

import (
	"log/slog"
	"sync"

	"github.com/elebirds/panoptes/internal/transport"
)

type GameRoomRegistry struct {
	rooms   map[string]*GameRoom
	players map[string]string
	mu      sync.RWMutex
}

var Registry = NewGameRoomRegistry()

var _ transport.GameRoomRegistry = (*GameRoomRegistry)(nil)

func NewGameRoomRegistry() *GameRoomRegistry {
	return &GameRoomRegistry{
		rooms:   make(map[string]*GameRoom),
		players: make(map[string]string),
	}
}

func (r *GameRoomRegistry) Register(room *GameRoom) {
	if room == nil {
		return
	}
	r.InvalidateRoomsForPlayersExcept(room.ID, room.PlayerIDs())

	r.mu.Lock()
	defer r.mu.Unlock()

	r.rooms[room.ID] = room
	for _, player := range room.Players {
		r.players[player.PlayerID()] = room.ID
	}
}

func (r *GameRoomRegistry) InvalidateRoomsForPlayersExcept(keepRoomID string, playerIDs []string) {
	if r == nil || len(playerIDs) == 0 {
		return
	}

	uniquePlayerIDs := make(map[string]struct{}, len(playerIDs))
	for _, playerID := range playerIDs {
		if playerID == "" {
			continue
		}
		uniquePlayerIDs[playerID] = struct{}{}
	}
	if len(uniquePlayerIDs) == 0 {
		return
	}

	roomsToCancel := make(map[string]*GameRoom)
	overlappingPlayers := make(map[string][]string)

	r.mu.Lock()
	for playerID := range uniquePlayerIDs {
		roomID, ok := r.players[playerID]
		if !ok || roomID == "" || roomID == keepRoomID {
			continue
		}
		room, ok := r.rooms[roomID]
		if !ok || room == nil {
			delete(r.players, playerID)
			continue
		}
		roomsToCancel[roomID] = room
		overlappingPlayers[roomID] = append(overlappingPlayers[roomID], playerID)
	}
	for roomID, room := range roomsToCancel {
		delete(r.rooms, roomID)
		for _, player := range room.Players {
			delete(r.players, player.PlayerID())
		}
	}
	r.mu.Unlock()

	for roomID, room := range roomsToCancel {
		slog.Info("旧对局已失效",
			"old_room_id", roomID,
			"new_room_id", keepRoomID,
			"player_ids", overlappingPlayers[roomID],
			"reason", "player_joined_new_game_session",
		)
		if room.runtime != nil {
			room.runtime.Cancel()
		}
	}
}

func (r *GameRoomRegistry) Unregister(roomID string) {
	r.mu.Lock()
	defer r.mu.Unlock()

	room, ok := r.rooms[roomID]
	if !ok {
		return
	}
	delete(r.rooms, roomID)
	for _, player := range room.Players {
		delete(r.players, player.PlayerID())
	}
}

func (r *GameRoomRegistry) GetRoomByPlayerID(playerID string) (transport.GameRoom, bool) {
	r.mu.RLock()
	defer r.mu.RUnlock()

	roomID, ok := r.players[playerID]
	if !ok {
		return nil, false
	}
	room, ok := r.rooms[roomID]
	if !ok {
		return nil, false
	}
	return room, true
}
