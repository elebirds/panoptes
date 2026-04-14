// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义对局模块的玩家抽象与行为约定。

package game

import (
	"context"

	"google.golang.org/protobuf/proto"
)

// Player 是游戏内玩家的抽象。
type Player interface {
	PlayerID() string
	Username() string
	IsBot() bool
	Send(ctx context.Context, msg proto.Message) error
	NotifyTurn(ctx context.Context, room *Room, phase string)
}
