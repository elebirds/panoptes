// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
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

func (p *ruleBotPlanner) closestEnemyTargetNode(unitPos domain.Position, visibleEnemies []*pb.UnitView, memoryEnemies []*gamequery.RememberedUnitView, enemyStructures []*pb.NodeView) string {
	bestNodeID := ""
	bestDistance := intMax
	for _, enemy := range visibleEnemies {
		if enemy == nil || enemy.GetPos() == nil {
			continue
		}
		distance := unitPos.DistanceTo(protoPosition(enemy.GetPos()))
		if distance < bestDistance {
			bestDistance = distance
			if nodeID := p.nodeIDAtPosition(protoPosition(enemy.GetPos())); nodeID != "" {
				bestNodeID = nodeID
			}
		}
	}
	for _, enemy := range memoryEnemies {
		if enemy == nil || enemy.View == nil || enemy.View.GetPos() == nil {
			continue
		}
		distance := unitPos.DistanceTo(protoPosition(enemy.View.GetPos()))
		if distance < bestDistance {
			bestDistance = distance
			if nodeID := p.nodeIDAtPosition(protoPosition(enemy.View.GetPos())); nodeID != "" {
				bestNodeID = nodeID
			}
		}
	}
	for _, node := range enemyStructures {
		if node == nil || node.GetPos() == nil {
			continue
		}
		distance := unitPos.DistanceTo(protoPosition(node.GetPos()))
		if distance < bestDistance {
			bestDistance = distance
			bestNodeID = node.GetId()
		}
	}
	return bestNodeID
}

func (p *ruleBotPlanner) closestExplorationTargetNode(unitID string, unitPos domain.Position) string {
	if p.req.State == nil || p.req.State.World == nil || p.observation == nil || unitID == "" {
		return ""
	}

	bestNodeID := ""
	bestDistance := intMax
	for _, node := range p.observation.Nodes {
		if node == nil || strings.TrimSpace(node.GetId()) == "" || node.GetIsCurrentlyVisible() {
			continue
		}
		targetEntry, ok := p.req.State.GetNode(node.GetId())
		if !ok || targetEntry == nil {
			continue
		}
		targetPos := ecs.PositionC.Get(targetEntry)
		distance := unitPos.DistanceTo(domain.Position{Q: targetPos.Q, R: targetPos.R})
		if distance <= 0 || distance > bestDistance {
			continue
		}
		if distance < bestDistance || bestNodeID == "" || node.GetId() < bestNodeID {
			bestDistance = distance
			bestNodeID = node.GetId()
		}
	}
	return bestNodeID
}

func (p *ruleBotPlanner) closestPressureTargetNode(unitPos domain.Position) string {
	if p.observation == nil {
		return ""
	}

	bestNodeID := ""
	bestDistance := intMax
	bestPriority := intMax
	for _, node := range p.observation.Nodes {
		if node == nil || strings.TrimSpace(node.GetId()) == "" {
			continue
		}
		priority, ok := p.pressurePriority(node)
		if !ok {
			continue
		}
		if node.GetPos() == nil {
			continue
		}
		distance := unitPos.DistanceTo(protoPosition(node.GetPos()))
		if distance <= 0 {
			continue
		}
		if priority > bestPriority {
			continue
		}
		if priority == bestPriority && distance > bestDistance {
			continue
		}
		if priority < bestPriority || distance < bestDistance || bestNodeID == "" || node.GetId() < bestNodeID {
			bestPriority = priority
			bestDistance = distance
			bestNodeID = node.GetId()
		}
	}
	return bestNodeID
}

func (p *ruleBotPlanner) pressurePriority(node *pb.NodeView) (int, bool) {
	if node == nil {
		return 0, false
	}
	switch {
	case node.GetControllerPlayerId() != "" && node.GetControllerPlayerId() != p.playerID:
		return 0, true
	case node.GetTerritoryOwnerPlayerId() != "" && node.GetTerritoryOwnerPlayerId() != p.playerID:
		return 1, true
	case node.GetControllerPlayerId() == "" && node.GetTerritoryOwnerPlayerId() == "":
		return 2, true
	default:
		return 0, false
	}
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
