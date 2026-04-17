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
	rooms        map[string]*GameRoom
	participants map[string]string
	mu           sync.RWMutex
}

var Registry = NewGameRoomRegistry()

var _ transport.GameRoomRegistry = (*GameRoomRegistry)(nil)

func NewGameRoomRegistry() *GameRoomRegistry {
	return &GameRoomRegistry{
		rooms:        make(map[string]*GameRoom),
		participants: make(map[string]string),
	}
}

func (r *GameRoomRegistry) Register(room *GameRoom) {
	if room == nil {
		return
	}
	r.InvalidateRoomsForParticipantsExcept(room.ID, room.ParticipantIDs())

	r.mu.Lock()
	defer r.mu.Unlock()

	r.rooms[room.ID] = room
	for _, participantID := range room.ParticipantIDs() {
		r.participants[participantID] = room.ID
	}
}

func (r *GameRoomRegistry) InvalidateRoomsForParticipantsExcept(keepRoomID string, participantIDs []string) {
	if r == nil || len(participantIDs) == 0 {
		return
	}

	uniqueParticipantIDs := make(map[string]struct{}, len(participantIDs))
	for _, participantID := range participantIDs {
		if participantID == "" {
			continue
		}
		uniqueParticipantIDs[participantID] = struct{}{}
	}
	if len(uniqueParticipantIDs) == 0 {
		return
	}

	roomsToCancel := make(map[string]*GameRoom)
	overlappingParticipants := make(map[string][]string)

	r.mu.Lock()
	for participantID := range uniqueParticipantIDs {
		roomID, ok := r.participants[participantID]
		if !ok || roomID == "" || roomID == keepRoomID {
			continue
		}
		room, ok := r.rooms[roomID]
		if !ok || room == nil {
			delete(r.participants, participantID)
			continue
		}
		roomsToCancel[roomID] = room
		overlappingParticipants[roomID] = append(overlappingParticipants[roomID], participantID)
	}
	for roomID, room := range roomsToCancel {
		delete(r.rooms, roomID)
		for _, participantID := range room.ParticipantIDs() {
			delete(r.participants, participantID)
		}
	}
	r.mu.Unlock()

	for roomID, room := range roomsToCancel {
		slog.Info("旧对局已失效",
			"old_room_id", roomID,
			"new_room_id", keepRoomID,
			"participant_ids", overlappingParticipants[roomID],
			"reason", "participant_joined_new_game_session",
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
	for _, participantID := range room.ParticipantIDs() {
		delete(r.participants, participantID)
	}
}

func (r *GameRoomRegistry) GetRoomByParticipantID(participantID string) (transport.GameRoom, bool) {
	r.mu.RLock()
	defer r.mu.RUnlock()

	roomID, ok := r.participants[participantID]
	if !ok {
		return nil, false
	}
	room, ok := r.rooms[roomID]
	if !ok {
		return nil, false
	}
	return room, true
}

func (r *GameRoomRegistry) HandleParticipantDisconnect(participantID string) {
	if r == nil || participantID == "" {
		return
	}

	r.mu.RLock()
	roomID, ok := r.participants[participantID]
	if !ok {
		r.mu.RUnlock()
		return
	}
	room := r.rooms[roomID]
	r.mu.RUnlock()

	if room == nil {
		return
	}
	if !room.IsHumanParticipant(participantID) {
		return
	}

	if room.forfeitDisconnectedPlayer(participantID) {
		r.Unregister(room.ID)
	}
}
