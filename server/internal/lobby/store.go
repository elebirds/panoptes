// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义大厅模块的存储接口。

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
