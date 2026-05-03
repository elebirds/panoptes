// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func (p *ruleBotPlanner) computeThreatLevel() int {
	if p.req.State == nil {
		return 0
	}
	threat := 0
	for _, enemy := range p.visibleEnemyUnits() {
		if enemy == nil || enemy.GetPos() == nil {
			continue
		}
		for _, city := range p.player.Cities {
			if city == nil {
				continue
			}
			cityEntry, ok := p.req.State.GetNode(city.CoreNodeID)
			if !ok || cityEntry == nil {
				continue
			}
			cityPos := ecs.PositionC.Get(cityEntry)
			if protoPosition(enemy.GetPos()).DistanceTo(domain.Position{Q: cityPos.Q, R: cityPos.R}) <= 3 {
				threat++
				break
			}
		}
	}
	return threat
}

func (p *ruleBotPlanner) visibleEnemyUnits() []*pb.UnitView {
	if p.observation == nil {
		return nil
	}
	out := make([]*pb.UnitView, 0)
	for _, unit := range p.observation.Units {
		if unit == nil || unit.GetFaction() == p.playerID {
			continue
		}
		out = append(out, unit)
	}
	return out
}

func (p *ruleBotPlanner) memoryEnemyUnits() []*gamequery.RememberedUnitView {
	if p.observation == nil {
		return nil
	}
	out := make([]*gamequery.RememberedUnitView, 0)
	for _, unit := range p.observation.MemoryUnits {
		if unit == nil || unit.View == nil || unit.View.GetFaction() == p.playerID {
			continue
		}
		out = append(out, unit)
	}
	return out
}

func (p *ruleBotPlanner) enemyStructureNodes() []*pb.NodeView {
	if p.observation == nil {
		return nil
	}
	out := make([]*pb.NodeView, 0)
	for _, node := range p.observation.Nodes {
		if node == nil || node.GetBuildingTypeId() == "" || node.GetControllerPlayerId() == "" || node.GetControllerPlayerId() == p.playerID {
			continue
		}
		out = append(out, node)
	}
	return out
}
