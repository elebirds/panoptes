// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"context"

	"google.golang.org/protobuf/proto"
)

// commandDelivery 统一 planning 命令的出站顺序与元数据传播。
// 约定：命令结果先发，planning snapshot 后发；所有消息共享调用方传入的 context/request meta。
type commandDelivery struct {
	ctx      context.Context
	playerID string
	port     planningDeliveryPort
}

func newCommandDelivery(ctx context.Context, playerID string, port planningDeliveryPort) commandDelivery {
	return commandDelivery{ctx: ctx, playerID: playerID, port: port}
}

func (d commandDelivery) send(msg proto.Message) {
	if d.port == nil || msg == nil {
		return
	}
	_ = d.port.SendToPlayer(d.ctx, d.playerID, msg)
}

func (d commandDelivery) snapshot() {
	if d.port == nil {
		return
	}
	_ = d.port.SendPlanningSnapshot(d.ctx, d.playerID)
}

func (d commandDelivery) sendWithSnapshot(msg proto.Message) {
	d.send(msg)
	d.snapshot()
}

func (d commandDelivery) sendAllWithSnapshot(msgs ...proto.Message) {
	for _, msg := range msgs {
		d.send(msg)
	}
	d.snapshot()
}
