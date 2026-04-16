// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的资源流动结算逻辑。

package economy

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type FlowSystem struct{}

func (s *FlowSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	// 新版内容集已经把固定产出转移到 recipe 结算，这里先保留空实现，避免继续读
	// 已删除的静态字段。
	// 它当前不承担实际主链逻辑，只作为未来资源流网络的明确挂点保留。
	_ = world
	_ = state
	return nil
}
