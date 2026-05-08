// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
)

func (p *ruleBotPlanner) closestCityDistance(nodeID string) int {
	targetEntry, ok := p.req.State.GetNode(nodeID)
	if !ok || targetEntry == nil {
		return intMax
	}
	targetPos := ecs.PositionC.Get(targetEntry)
	best := intMax
	for _, city := range p.player.Cities {
		if city == nil {
			continue
		}
		cityEntry, ok := p.req.State.GetNode(city.CoreNodeID)
		if !ok || cityEntry == nil {
			continue
		}
		cityPos := ecs.PositionC.Get(cityEntry)
		distance := (domain.Position{Q: targetPos.Q, R: targetPos.R}).DistanceTo(domain.Position{Q: cityPos.Q, R: cityPos.R})
		if distance < best {
			best = distance
		}
	}
	return best
}

func (p *ruleBotPlanner) closestEnemyDistance(nodeID string) int {
	targetEntry, ok := p.req.State.GetNode(nodeID)
	if !ok || targetEntry == nil {
		return intMax
	}
	targetPos := ecs.PositionC.Get(targetEntry)
	best := intMax
	for _, enemy := range p.visibleEnemyUnits() {
		if enemy == nil || enemy.GetPos() == nil {
			continue
		}
		distance := (domain.Position{Q: targetPos.Q, R: targetPos.R}).DistanceTo(protoPosition(enemy.GetPos()))
		if distance < best {
			best = distance
		}
	}
	return best
}

func (p *ruleBotPlanner) nodeIDAtPosition(pos domain.Position) string {
	if p.req.State == nil || p.req.State.World == nil {
		return ""
	}
	entry, ok := domain.GetNodeAt(p.req.State.World, pos)
	if !ok || entry == nil {
		return ""
	}
	return ecs.NodeC.Get(entry).ID
}
