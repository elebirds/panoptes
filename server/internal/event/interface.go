// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义事件模型的抽象接口。

package event

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/yohamta/donburi"
)

// Event is the single write entry for all game state mutations.
type Event interface {
	Apply(world donburi.World, state *domain.GameState)
	Kind() string
	String() string
}
