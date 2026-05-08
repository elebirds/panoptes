// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-08 00:00:00 +0800
// Description: 实现工程单位移动铺路事件构造逻辑。

package combat

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func engineerRoadTrailBuiltEvent(ctx *ResolutionContext, unitID string, actual domain.Position) (event.EngineerRoadTrailBuiltEvent, bool) {
	unit, ok := ctx.SnapshotUnit(unitID)
	if !ok || !isEngineerRoadBuilder(unit) {
		return event.EngineerRoadTrailBuiltEvent{}, false
	}
	plan := ctx.Plans[unitID]
	if plan == nil || actual == plan.Start {
		return event.EngineerRoadTrailBuiltEvent{}, false
	}
	actualIndex := indexOfPosition(plan.Path, actual)
	if actualIndex <= 0 {
		return event.EngineerRoadTrailBuiltEvent{}, false
	}

	nodeIDs := roadTrailNodeIDs(ctx, plan.Path[:actualIndex+1])
	if len(nodeIDs) == 0 {
		return event.EngineerRoadTrailBuiltEvent{}, false
	}
	return event.EngineerRoadTrailBuiltEvent{
		UnitID:  unitID,
		Owner:   unit.PlayerID,
		NodeIDs: nodeIDs,
	}, true
}

func isEngineerRoadBuilder(unit SnapshotUnit) bool {
	unitType := strings.ToLower(strings.TrimSpace(string(unit.Type)))
	if strings.Contains(unitType, "engineer") {
		return true
	}
	cfg, ok := staticdata.Default().GetUnit(string(unit.Type))
	if !ok {
		return false
	}
	return cfg.Class == "civilian" && strings.Contains(strings.ToLower(strings.TrimSpace(cfg.ID)), "engineer")
}

func roadTrailNodeIDs(ctx *ResolutionContext, path []domain.Position) []string {
	nodeIDs := make([]string, 0, len(path))
	seen := make(map[string]struct{}, len(path))
	for _, pos := range path {
		entry, ok := domain.GetNodeAt(ctx.World, pos)
		if !ok {
			continue
		}
		nodeID := strings.TrimSpace(ecs.NodeC.Get(entry).ID)
		if nodeID == "" {
			continue
		}
		if _, exists := seen[nodeID]; exists {
			continue
		}
		seen[nodeID] = struct{}{}
		nodeIDs = append(nodeIDs, nodeID)
	}
	return nodeIDs
}
