// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的机器人策略。

package game

import (
	"context"
	"time"

	"github.com/elebirds/panoptes/internal/domain"
)

type botPlayerIDKey struct{}

// BotStrategy 定义 Bot 的行为策略，可热替换。
type BotStrategy interface {
	DecideAndSubmit(ctx context.Context, room *Room, phase string)
}

// RandomStrategy 随机策略，用于开发调试。
type RandomStrategy struct{}

func (s *RandomStrategy) DecideAndSubmit(ctx context.Context, room *Room, phase string) {
	playerID, _ := ctx.Value(botPlayerIDKey{}).(string)
	if playerID == "" {
		return
	}

	switch phase {
	case domain.PhasePlanning.String():
		select {
		case <-time.After(500 * time.Millisecond):
			room.Submit(playerID)
		case <-ctx.Done():
			room.Submit(playerID)
		}
	}
}
