// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现大厅模块的错误类型与错误码映射。

package lobby

import "errors"

var (
	ErrRoomFull       = errors.New("room_full")
	ErrAlreadyInRoom  = errors.New("already_in_room")
	ErrRoomNotFound   = errors.New("room_not_found")
	ErrPlayerNotFound = errors.New("player_not_found")
	ErrNotHost        = errors.New("not_host")
	ErrInvalidStatus  = errors.New("invalid_status")
	ErrInvalidPlayers = errors.New("invalid_player_count")
)
