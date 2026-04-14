// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现单位结算引擎的修正器计算逻辑。

package combat

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
)

// 战斗层通过这组 helper 读取“科技修正后的单位属性”，
// 而不是把修正后的值写回单位组件。这样战斗、寻路、预览能共享同一套读时语义。
func effectiveUnitAttack(state *domain.GameState, faction string, unitType domain.UnitType, base int) int {
	if state == nil {
		return base
	}
	return state.ApplyScalarModifier(faction, string(staticdata.ModifierTriggerUnitAttack), string(unitType), "", base)
}

func effectiveUnitMoveRange(state *domain.GameState, faction string, unitType domain.UnitType, base int) int {
	if state == nil {
		return base
	}
	return state.ApplyScalarModifier(faction, string(staticdata.ModifierTriggerUnitMoveRange), string(unitType), "", base)
}

func effectiveUnitSiegeMultiplier(state *domain.GameState, faction string, unitType domain.UnitType, base float64) float64 {
	if state == nil {
		return base
	}
	return state.ApplyFloatModifier(faction, string(staticdata.ModifierTriggerUnitSiegeMultiplier), string(unitType), "", base)
}

func effectiveUnitDestroyMultiplier(state *domain.GameState, faction string, unitType domain.UnitType, base float64) float64 {
	if state == nil {
		return base
	}
	return state.ApplyFloatModifier(faction, string(staticdata.ModifierTriggerUnitDestroyMult), string(unitType), "", base)
}
