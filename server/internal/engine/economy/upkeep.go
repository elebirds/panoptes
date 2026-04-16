// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的补给与维护结算逻辑。

package economy

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type UpkeepSystem struct{}

func (s *UpkeepSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	// MVP 新版基线暂不包含建筑 upkeep；保留空实现以避免继续依赖旧静态结构。
	// 若后续重新引入建筑维护成本，这里就是最自然的回归位置。
	_ = world
	_ = state
	return nil
}
