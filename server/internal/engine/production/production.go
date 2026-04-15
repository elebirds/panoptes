// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的生产结算逻辑。

package production

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type ProductionSystem struct{}

func (s *ProductionSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	// 新版生产统一走 recipe 系统，旧的固定军事生产链先清空，避免继续依赖已删除
	// 的 ProducesUnits/Production.Input 结构。
	_ = world
	_ = state
	return nil
}

func isMilitaryProducer(buildingType string) bool {
	switch buildingType {
	case "barracks", "stable", "engineer_camp":
		return true
	default:
		return false
	}
}
