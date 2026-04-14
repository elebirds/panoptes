// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的机器人玩家适配器。

package game

import (
	"context"

	"google.golang.org/protobuf/proto"
)

type BotPlayer struct {
	playerID string
	username string
	strategy BotStrategy
}

func NewBotPlayer(playerID, username string, strategy BotStrategy) *BotPlayer {
	if strategy == nil {
		strategy = &RandomStrategy{}
	}
	return &BotPlayer{
		playerID: playerID,
		username: username,
		strategy: strategy,
	}
}

func (p *BotPlayer) PlayerID() string {
	return p.playerID
}

func (p *BotPlayer) Username() string {
	return p.username
}

func (p *BotPlayer) IsBot() bool {
	return true
}

func (p *BotPlayer) Send(proto.Message) error {
	return nil
}

func (p *BotPlayer) NotifyTurn(ctx context.Context, room *Room, phase string) {
	go p.strategy.DecideAndSubmit(context.WithValue(ctx, botPlayerIDKey{}, p.playerID), room, phase)
}
