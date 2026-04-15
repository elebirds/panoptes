// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的人类玩家适配器。

package game

import (
	"context"
	"log/slog"

	"github.com/elebirds/panoptes/internal/domain"
	gamesession "github.com/elebirds/panoptes/internal/game/session"
	"github.com/elebirds/panoptes/internal/transport"
	"google.golang.org/protobuf/proto"
)

type HumanPlayer struct {
	playerID  string
	username  string
	transport transport.GameTransport
}

func NewHumanPlayer(playerID, username string, t transport.GameTransport) *HumanPlayer {
	return &HumanPlayer{
		playerID:  playerID,
		username:  username,
		transport: t,
	}
}

func (p *HumanPlayer) PlayerID() string {
	return p.playerID
}

func (p *HumanPlayer) Username() string {
	return p.username
}

func (p *HumanPlayer) IsBot() bool {
	return false
}

func (p *HumanPlayer) Send(ctx context.Context, msg proto.Message) error {
	return p.transport.Send(ctx, p.playerID, msg)
}

func (p *HumanPlayer) NotifyTurn(_ context.Context, room *Room, phase string) {
	if phase != domain.PhasePlanning.String() {
		slog.Warn("未知阶段通知", "phase", phase, "player_id", p.playerID)
		return
	}
	if room == nil || room.State() == nil {
		return
	}
	msg := gamesession.BuildPlanningStartMessage(room.State(), p.playerID, phase)
	if msg == nil {
		return
	}
	_ = p.Send(context.Background(), msg)
}
