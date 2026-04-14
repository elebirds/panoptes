// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现大厅模块的房间模型与编排逻辑。

package lobby

import (
	"crypto/rand"
	"fmt"
	"time"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

type RoomStatus string

const (
	RoomStatusWaiting  RoomStatus = "waiting"
	RoomStatusReady    RoomStatus = "ready"
	RoomStatusStarting RoomStatus = "starting"
	RoomStatusInGame   RoomStatus = "in_game"
)

type Room struct {
	ID         string
	Code       string
	Name       string
	HostID     string
	Players    []*RoomPlayer
	MaxPlayers int
	Status     RoomStatus
	CreatedAt  time.Time
	devMode    bool
}

type RoomPlayer struct {
	PlayerID string
	Username string
	IsReady  bool
	IsBot    bool
	JoinedAt time.Time
}

func NewRoom(id, code, name, hostID, hostUsername string, maxPlayers int, devMode bool) *Room {
	now := time.Now()
	return &Room{
		ID:         id,
		Code:       code,
		Name:       name,
		HostID:     hostID,
		MaxPlayers: maxPlayers,
		Status:     RoomStatusWaiting,
		CreatedAt:  now,
		devMode:    devMode,
		Players: []*RoomPlayer{
			{
				PlayerID: hostID,
				Username: hostUsername,
				JoinedAt: now,
			},
		},
	}
}

func (r *Room) AddPlayer(playerID, username string) error {
	if _, ok := r.GetPlayer(playerID); ok {
		return ErrAlreadyInRoom
	}
	if len(r.Players) >= r.MaxPlayers {
		return ErrRoomFull
	}

	r.Players = append(r.Players, &RoomPlayer{
		PlayerID: playerID,
		Username: username,
		JoinedAt: time.Now(),
	})
	return nil
}

func (r *Room) AddBot() (*RoomPlayer, error) {
	if r.Status != RoomStatusWaiting {
		return nil, ErrInvalidStatus
	}
	if len(r.Players) >= r.MaxPlayers {
		return nil, ErrRoomFull
	}
	if r.botCount() >= r.MaxPlayers-1 {
		return nil, ErrRoomFull
	}

	playerID, err := randomBotPlayerID()
	if err != nil {
		return nil, fmt.Errorf("generate bot player id: %w", err)
	}
	for {
		if _, exists := r.GetPlayer(playerID); !exists {
			break
		}
		playerID, err = randomBotPlayerID()
		if err != nil {
			return nil, fmt.Errorf("generate bot player id: %w", err)
		}
	}

	bot := &RoomPlayer{
		PlayerID: playerID,
		Username: r.nextBotUsername(),
		IsReady:  true,
		IsBot:    true,
		JoinedAt: time.Now(),
	}
	r.Players = append(r.Players, bot)
	return bot, nil
}

func (r *Room) RemovePlayer(playerID string) {
	for idx, player := range r.Players {
		if player.PlayerID != playerID {
			continue
		}
		r.Players = append(r.Players[:idx], r.Players[idx+1:]...)
		return
	}
}

func (r *Room) KickPlayer(operatorID, targetID string) (*RoomPlayer, error) {
	if operatorID != r.HostID {
		return nil, ErrNotHost
	}
	if operatorID == targetID {
		return nil, ErrInvalidStatus
	}

	player, ok := r.GetPlayer(targetID)
	if !ok {
		return nil, ErrPlayerNotFound
	}
	playerCopy := *player
	r.RemovePlayer(targetID)
	return &playerCopy, nil
}

func (r *Room) SetReady(playerID string, ready bool) error {
	player, ok := r.GetPlayer(playerID)
	if !ok {
		return ErrPlayerNotFound
	}
	player.IsReady = ready
	return nil
}

func (r *Room) IsAllReady() bool {
	minPlayers := 2
	if r.devMode {
		minPlayers = 1
	}
	return len(r.Players) >= minPlayers && r.allPlayersReady()
}

func (r *Room) GetPlayer(playerID string) (*RoomPlayer, bool) {
	for _, player := range r.Players {
		if player.PlayerID == playerID {
			return player, true
		}
	}
	return nil, false
}

func (r *Room) ToProto() *pb.MsgRoomState {
	players := make([]*pb.RoomPlayer, 0, len(r.Players))
	for _, player := range r.Players {
		players = append(players, &pb.RoomPlayer{
			PlayerId: player.PlayerID,
			Username: player.Username,
			IsReady:  player.IsReady,
			IsHost:   player.PlayerID == r.HostID,
			IsBot:    player.IsBot,
		})
	}

	return &pb.MsgRoomState{
		RoomId:     r.ID,
		RoomCode:   r.Code,
		Name:       r.Name,
		Players:    players,
		Status:     string(r.Status),
		MaxPlayers: int32(r.MaxPlayers),
	}
}

func (r *Room) SetDevMode(devMode bool) {
	r.devMode = devMode
}

func (r *Room) allPlayersReady() bool {
	for _, player := range r.Players {
		if !player.IsReady {
			return false
		}
	}
	return true
}

func (r *Room) botCount() int {
	count := 0
	for _, player := range r.Players {
		if player.IsBot {
			count++
		}
	}
	return count
}

func (r *Room) nextBotUsername() string {
	next := r.botCount() + 1
	if next == 1 {
		return "Bot"
	}
	return fmt.Sprintf("Bot %d", next)
}

func randomBotPlayerID() (string, error) {
	const letters = "abcdefghijklmnopqrstuvwxyz"

	buf := make([]byte, 8)
	random := make([]byte, 8)
	if _, err := rand.Read(random); err != nil {
		return "", err
	}
	for idx := range buf {
		buf[idx] = letters[int(random[idx])%len(letters)]
	}
	return "bot_" + string(buf), nil
}
