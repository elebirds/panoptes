// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现单位结算引擎的移动参数计算逻辑。

package combat

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
)

const moveBudgetScale = 2

func buildMovementProfile(unitType domain.UnitType, caps domain.UnitCapabilities, moveRange int) domain.MovementProfile {
	profile := domain.MovementProfile{
		MoveBudget: maxMovementInt(1, moveRange*moveBudgetScale),
		RoadCost:   1,
		Mounted:    caps.Charge,
	}

	if cfg, ok := staticdata.Default().GetUnit(string(unitType)); ok {
		profile.MoveBudget = maxMovementInt(1, cfg.MoveRange*moveBudgetScale)
		profile.RoadBonus = cfg.RoadSpeedBonus
		profile.Mounted = caps.Charge || cfg.Class == "mobile"
	}

	return profile
}

func maxMovementInt(a, b int) int {
	if a > b {
		return a
	}
	return b
}
