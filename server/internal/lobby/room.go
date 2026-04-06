package lobby

import (
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
}

type RoomPlayer struct {
	PlayerID string
	Username string
	IsReady  bool
	JoinedAt time.Time
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

func (r *Room) RemovePlayer(playerID string) {
	for idx, player := range r.Players {
		if player.PlayerID != playerID {
			continue
		}
		r.Players = append(r.Players[:idx], r.Players[idx+1:]...)
		return
	}
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
	if len(r.Players) < 2 {
		return false
	}
	for _, player := range r.Players {
		if !player.IsReady {
			return false
		}
	}
	return true
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
