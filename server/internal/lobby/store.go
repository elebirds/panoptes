package lobby

import "context"

type LobbyStore interface {
	CreateRoom(ctx context.Context, room *Room) error
	GetRoom(ctx context.Context, roomID string) (*Room, error)
	GetRoomByCode(ctx context.Context, code string) (*Room, error)
	UpdateRoom(ctx context.Context, room *Room) error
	DeleteRoom(ctx context.Context, roomID string) error
	GetRoomByPlayerID(ctx context.Context, playerID string) (*Room, error)
}
