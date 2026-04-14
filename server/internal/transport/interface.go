// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义传输层的抽象接口。

package transport

import (
	"context"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/transport/dispatch"
	"google.golang.org/protobuf/proto"
)

// GameTransport 是游戏消息推送抽象。
// WebSocket 和 gRPC 都应实现该接口。
type GameTransport interface {
	Send(ctx context.Context, playerID string, msg proto.Message) error
	Broadcast(ctx context.Context, roomID string, msg proto.Message) error
	Stream(ctx context.Context, playerID string, msgs <-chan proto.Message) error
}

// GameRoom 是运行中的对局房间最小提交接口。
type GameRoom interface {
	HandleGameCommand(ctx dispatch.InboundContext, cmd *pb.GameCommand) error
}

// GameRoomRegistry 是运行中对局房间的最小查询接口。
type GameRoomRegistry interface {
	GetRoomByPlayerID(playerID string) (GameRoom, bool)
}
