// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载后端测试/调试场景的构造与布置辅助逻辑。

package scenario

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

func placeBuildingAtNode(state *domain.GameState, buildingType string, playerID string, cityID string, nodeID string) (*donburi.Entry, error) {
	if state == nil {
		return nil, fmt.Errorf("state is nil")
	}
	nodeEntry, ok := state.GetNode(nodeID)
	if !ok {
		return nil, fmt.Errorf("missing node %s", nodeID)
	}
	ecs.CreateBuilding(state.World, buildingType, playerID, cityID, nodeEntry)
	return nodeEntry, nil
}

func placeUnitWithID(state *domain.GameState, unitType string, playerID string, position domain.Position, unitID string) *donburi.Entry {
	if state == nil || state.World == nil {
		return nil
	}
	if domain.HasUnitAtNode(state.World, position) {
		// 调试场景也必须遵守“不堆叠”规则；fixture 写错时直接失败，避免旧模型继续扩散。
		panic(fmt.Sprintf("unit placement collision at (%d,%d)", position.Q, position.R))
	}
	entry := state.World.Entry(ecs.CreateUnit(state.World, unitType, playerID, position))
	stats := ecs.UnitStatsC.Get(entry)
	stats.ID = unitID
	return entry
}
