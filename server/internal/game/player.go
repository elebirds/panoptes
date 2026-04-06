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
	Send(msg proto.Message) error
	NotifyTurn(ctx context.Context, room *Room, phase string)
}
