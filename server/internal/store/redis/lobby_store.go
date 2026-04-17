// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现Redis 存储层的大厅存储实现。

package redis

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"time"

	"github.com/elebirds/panoptes/internal/lobby"
	goredis "github.com/redis/go-redis/v9"
)

const lobbyTTL = 2 * time.Hour

type LobbyStore struct {
	client *Client
}

var _ lobby.LobbyStore = (*LobbyStore)(nil)

func NewLobbyStore(client *Client) *LobbyStore {
	return &LobbyStore{client: client}
}

func (s *LobbyStore) CreateRoom(ctx context.Context, room *lobby.Room) error {
	if err := s.ensureClient(); err != nil {
		return err
	}
	return s.persistRoom(ctx, room)
}

func (s *LobbyStore) GetRoom(ctx context.Context, roomID string) (*lobby.Room, error) {
	if err := s.ensureClient(); err != nil {
		return nil, err
	}

	raw, err := s.client.Get(ctx, roomKey(roomID)).Result()
	if errors.Is(err, goredis.Nil) {
		return nil, lobby.ErrRoomNotFound
	}
	if err != nil {
		return nil, fmt.Errorf("get room %s: %w", roomID, err)
	}

	var room lobby.Room
	if err := json.Unmarshal([]byte(raw), &room); err != nil {
		return nil, fmt.Errorf("unmarshal room %s: %w", roomID, err)
	}

	return &room, nil
}

func (s *LobbyStore) GetRoomByCode(ctx context.Context, code string) (*lobby.Room, error) {
	if err := s.ensureClient(); err != nil {
		return nil, err
	}

	roomID, err := s.client.Get(ctx, codeKey(code)).Result()
	if errors.Is(err, goredis.Nil) {
		return nil, lobby.ErrRoomNotFound
	}
	if err != nil {
		return nil, fmt.Errorf("get room by code %s: %w", code, err)
	}

	room, err := s.GetRoom(ctx, roomID)
	if errors.Is(err, lobby.ErrRoomNotFound) {
		if delErr := s.client.Del(ctx, codeKey(code)).Err(); delErr != nil {
			return nil, fmt.Errorf("delete stale code key %s: %w", code, delErr)
		}
		return nil, lobby.ErrRoomNotFound
	}
	if err != nil {
		return nil, err
	}
	return room, nil
}

func (s *LobbyStore) UpdateRoom(ctx context.Context, room *lobby.Room) error {
	if err := s.ensureClient(); err != nil {
		return err
	}

	existing, err := s.GetRoom(ctx, room.ID)
	if err != nil && !errors.Is(err, lobby.ErrRoomNotFound) {
		return err
	}
	if existing != nil {
		if existing.Code != room.Code {
			if err := s.client.Del(ctx, codeKey(existing.Code)).Err(); err != nil {
				return fmt.Errorf("delete old code key: %w", err)
			}
		}
		existingPlayers := make(map[string]struct{}, len(existing.Players))
		for _, player := range existing.Players {
			existingPlayers[player.PlayerID] = struct{}{}
		}
		for _, player := range room.Players {
			delete(existingPlayers, player.PlayerID)
		}
		for playerID := range existingPlayers {
			if err := s.client.Del(ctx, playerKey(playerID)).Err(); err != nil {
				return fmt.Errorf("delete player key %s: %w", playerID, err)
			}
		}
	}

	return s.persistRoom(ctx, room)
}

func (s *LobbyStore) DeleteRoom(ctx context.Context, roomID string) error {
	if err := s.ensureClient(); err != nil {
		return err
	}

	room, err := s.GetRoom(ctx, roomID)
	if err != nil {
		return err
	}

	keys := []string{roomKey(roomID), codeKey(room.Code)}
	for _, player := range room.Players {
		keys = append(keys, playerKey(player.PlayerID))
	}

	if err := s.client.Del(ctx, keys...).Err(); err != nil {
		return fmt.Errorf("delete room %s: %w", roomID, err)
	}
	return nil
}

func (s *LobbyStore) GetRoomByPlayerID(ctx context.Context, playerID string) (*lobby.Room, error) {
	if err := s.ensureClient(); err != nil {
		return nil, err
	}

	roomID, err := s.client.Get(ctx, playerKey(playerID)).Result()
	if errors.Is(err, goredis.Nil) {
		return nil, lobby.ErrRoomNotFound
	}
	if err != nil {
		return nil, fmt.Errorf("get room by player %s: %w", playerID, err)
	}

	room, err := s.GetRoom(ctx, roomID)
	if errors.Is(err, lobby.ErrRoomNotFound) {
		if delErr := s.client.Del(ctx, playerKey(playerID)).Err(); delErr != nil {
			return nil, fmt.Errorf("delete stale player key %s: %w", playerID, delErr)
		}
		return nil, lobby.ErrRoomNotFound
	}
	if err != nil {
		return nil, err
	}
	if _, ok := room.GetPlayer(playerID); !ok {
		if delErr := s.client.Del(ctx, playerKey(playerID)).Err(); delErr != nil {
			return nil, fmt.Errorf("delete mismatched player key %s: %w", playerID, delErr)
		}
		return nil, lobby.ErrRoomNotFound
	}
	return room, nil
}

func (s *LobbyStore) ensureClient() error {
	if s == nil || s.client == nil || s.client.Client == nil {
		return errors.New("redis client is not configured")
	}
	return nil
}

func (s *LobbyStore) persistRoom(ctx context.Context, room *lobby.Room) error {
	payload, err := json.Marshal(room)
	if err != nil {
		return fmt.Errorf("marshal room %s: %w", room.ID, err)
	}

	pipe := s.client.TxPipeline()
	pipe.Set(ctx, roomKey(room.ID), payload, lobbyTTL)
	pipe.Set(ctx, codeKey(room.Code), room.ID, lobbyTTL)
	for _, player := range room.Players {
		pipe.Set(ctx, playerKey(player.PlayerID), room.ID, lobbyTTL)
	}
	if _, err := pipe.Exec(ctx); err != nil {
		return fmt.Errorf("persist room %s: %w", room.ID, err)
	}
	return nil
}

func roomKey(roomID string) string {
	return "lobby:room:" + roomID
}

func codeKey(code string) string {
	return "lobby:code:" + code
}

func playerKey(playerID string) string {
	return "lobby:player:" + playerID
}
